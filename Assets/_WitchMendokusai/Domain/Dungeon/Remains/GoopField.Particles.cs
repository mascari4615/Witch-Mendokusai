using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 액체 위 입자. 감전된 칸에서 짧은 불똥이 옆으로 튐. 공유 파티클에 Emit 만. 불은 GoopFire (VFX 그래프)
	public sealed partial class GoopField
	{
		private ParticleSystem sparks;
		private float sparkCarry;
		private readonly List<Vector2Int> shockedCells = new();

		private void CreateParticles(Transform parent)
		{
			GameObject host = new("GoopSparks");
			host.transform.SetParent(parent, false);
			sparks = host.AddComponent<ParticleSystem>();
			sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ParticleSystem.MainModule main = sparks.main;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.playOnAwake = false;
			main.loop = false;
			main.maxParticles = settings.MaxGoopParticles;
			main.gravityModifier = 0f;
			ParticleSystem.EmissionModule emission = sparks.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = sparks.shape;
			shape.enabled = false;
			ParticleSystem.SizeOverLifetimeModule size = sparks.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
			ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = ParticleSystemRenderMode.Stretch;
			renderer.sharedMaterial = settings.GoopParticleMaterial;
			renderer.velocityScale = settings.SparkStretch;
			renderer.lengthScale = 1f;
			FadeOverLifetime(sparks);
			sparks.Play();
		}

		// 불똥은 색 알파로 사라짐
		private static void FadeOverLifetime(ParticleSystem system)
		{
			ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
			color.enabled = true;
			Gradient fade = new();
			fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0f, 1f) });
			color.color = fade;
		}

		// 칸 목록은 Tick 에서 모은 것. 초당 칸마다 몇 개를 소수 몫까지 모아 냄, 프레임 상한
		private void TickParticles(float deltaTime, float groundHeight)
		{
			int sparkCount = Take(ref sparkCarry, shockedCells.Count * settings.SparksPerCellPerSecond * deltaTime);
			for (int i = 0; i < sparkCount && shockedCells.Count > 0; i++)
			{
				Vector3 at = CenterOf(shockedCells[Random.Range(0, shockedCells.Count)], groundHeight) + RandomInCell() + Vector3.up * .05f;
				Vector2 direction = Random.insideUnitCircle.normalized;
				ParticleSystem.EmitParams spark = new()
				{
					position = at,
					velocity = new Vector3(direction.x, Random.Range(0f, settings.SparkLift), direction.y) * settings.SparkSpeed,
					startSize = settings.SparkSize,
					startLifetime = settings.SparkLife,
					startColor = settings.SparkColor,
				};
				sparks.Emit(spark, 1);
			}
		}

		// 소수 몫을 모아 이번 프레임에 낼 정수 개수. 프레임 상한
		private int Take(ref float carry, float amount)
		{
			carry += amount;
			int count = Mathf.Min(Mathf.FloorToInt(carry), settings.MaxGoopParticlesPerFrame);
			carry -= Mathf.Floor(carry);
			return count;
		}

		private Vector3 RandomInCell()
		{
			float half = settings.GoopCellSize * .5f;
			return new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
		}

		private void ClearParticles()
		{
			sparks.Clear();
			sparkCarry = 0f;
		}
	}
}
