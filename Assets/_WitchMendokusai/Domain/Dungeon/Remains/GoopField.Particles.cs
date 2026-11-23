using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 액체 위 입자. 타는 칸에서 불길 혀가 서서 일렁이고 불티가 오름, 감전된 칸에서 짧은 불똥이 옆으로 튐. 공유 파티클 둘에 Emit 만
	// 불길 혀는 세운 판 (VerticalBillboard) 이라 기울어진 카메라에서도 위로 선 모양. 불티는 불똥 계에 주황으로 섞어 냄
	public sealed partial class GoopField
	{
		private ParticleSystem flames;
		private ParticleSystem sparks;
		private Material flameMaterial;
		private float emberCarry;
		private float flameCarry;
		private float sparkCarry;
		private readonly List<Vector2Int> burningCells = new();
		private readonly List<Vector2Int> shockedCells = new();

		private void CreateParticles(Transform parent)
		{
			flames = BuildParticles(parent, "GoopFlames", ParticleSystemRenderMode.VerticalBillboard, settings.FlameEndColor);
			sparks = BuildParticles(parent, "GoopSparks", ParticleSystemRenderMode.Stretch, Color.white);
			ShapeFlames();
		}

		// 불길 혀 모양 판, 가로세로 따로 (판 비율), 솟았다가 오르며 가늘어짐
		private void ShapeFlames()
		{
			flameMaterial = new Material(settings.GoopParticleMaterial) { name = "GoopFlames", hideFlags = HideFlags.HideAndDontSave, mainTexture = UltimateFxTextures.FlameTongue };
			flames.GetComponent<ParticleSystemRenderer>().sharedMaterial = flameMaterial;
			ParticleSystem.MainModule main = flames.main;
			main.startSize3D = true;
			ParticleSystem.SizeOverLifetimeModule size = flames.sizeOverLifetime;
			size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
				new Keyframe(0f, settings.FlameGrowFrom), new Keyframe(settings.FlamePeakAt, 1f), new Keyframe(1f, settings.FlameShrinkTo)));
		}

		private ParticleSystem BuildParticles(Transform parent, string systemName, ParticleSystemRenderMode renderMode, Color endTint)
		{
			GameObject host = new(systemName);
			host.transform.SetParent(parent, false);
			ParticleSystem system = host.AddComponent<ParticleSystem>();
			system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			ParticleSystem.MainModule main = system.main;
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.playOnAwake = false;
			main.loop = false;
			main.maxParticles = settings.MaxGoopParticles;
			main.gravityModifier = 0f;
			ParticleSystem.EmissionModule emission = system.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = system.shape;
			shape.enabled = false;
			ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));
			ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
			color.enabled = true;
			Gradient fade = new();
			// 시작 색 (Emit) 에 곱함. 불꽃은 끝으로 갈수록 붉고 어둡게
			fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(endTint, 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.8f, .5f), new GradientAlphaKey(0f, 1f) });
			color.color = fade;
			ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = renderMode;
			renderer.sharedMaterial = settings.GoopParticleMaterial;
			if (renderMode == ParticleSystemRenderMode.Stretch)
			{
				renderer.velocityScale = settings.SparkStretch;
				renderer.lengthScale = 1f;
			}
			system.Play();
			return system;
		}

		// 칸 목록은 Tick 에서 모은 것. 초당 칸마다 몇 개를 소수 몫까지 모아 냄, 프레임 상한
		private void TickParticles(float deltaTime, float groundHeight)
		{
			flameCarry += burningCells.Count * settings.FlamesPerCellPerSecond * deltaTime;
			int flameCount = Mathf.Min(Mathf.FloorToInt(flameCarry), settings.MaxGoopParticlesPerFrame);
			flameCarry -= Mathf.Floor(flameCarry);
			for (int i = 0; i < flameCount && burningCells.Count > 0; i++)
			{
				Vector3 at = CenterOf(burningCells[Random.Range(0, burningCells.Count)], groundHeight) + RandomInCell();
				float width = Random.Range(settings.FlameSize.x, settings.FlameSize.y);
				ParticleSystem.EmitParams flame = new()
				{
					// 판 가운데가 기준이라 밑동이 바닥에 닿게 나올 때 높이의 반만큼 올림
					position = at + Vector3.up * width * settings.FlameAspect * .5f * settings.FlameGrowFrom,
					velocity = new Vector3(Random.Range(-settings.FlameDrift, settings.FlameDrift), Random.Range(settings.FlameRise.x, settings.FlameRise.y), Random.Range(-settings.FlameDrift, settings.FlameDrift)),
					startSize3D = new Vector3(width, width * settings.FlameAspect, 1f),
					rotation = Random.Range(-settings.FlameTilt, settings.FlameTilt),
					startLifetime = Random.Range(settings.FlameLife.x, settings.FlameLife.y),
					startColor = settings.FlameColor,
				};
				flames.Emit(flame, 1);
			}

			emberCarry += burningCells.Count * settings.EmbersPerCellPerSecond * deltaTime;
			int emberCount = Mathf.Min(Mathf.FloorToInt(emberCarry), settings.MaxGoopParticlesPerFrame);
			emberCarry -= Mathf.Floor(emberCarry);
			for (int i = 0; i < emberCount && burningCells.Count > 0; i++)
			{
				Vector3 at = CenterOf(burningCells[Random.Range(0, burningCells.Count)], groundHeight) + RandomInCell() + Vector3.up * settings.EmberStartHeight;
				ParticleSystem.EmitParams ember = new()
				{
					position = at,
					velocity = new Vector3(Random.Range(-settings.EmberDrift, settings.EmberDrift), Random.Range(settings.EmberRise.x, settings.EmberRise.y), Random.Range(-settings.EmberDrift, settings.EmberDrift)),
					startSize = settings.EmberSize,
					startLifetime = Random.Range(settings.EmberLife.x, settings.EmberLife.y),
					startColor = settings.EmberColor,
				};
				sparks.Emit(ember, 1);
			}

			sparkCarry += shockedCells.Count * settings.SparksPerCellPerSecond * deltaTime;
			int sparkCount = Mathf.Min(Mathf.FloorToInt(sparkCarry), settings.MaxGoopParticlesPerFrame);
			sparkCarry -= Mathf.Floor(sparkCarry);
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

		private Vector3 RandomInCell()
		{
			float half = settings.GoopCellSize * .5f;
			return new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
		}

		private void ClearParticles()
		{
			flames.Clear();
			sparks.Clear();
			flameCarry = 0f;
			sparkCarry = 0f;
			emberCarry = 0f;
		}

		private void DisposeParticles() => Object.Destroy(flameMaterial);
	}
}
