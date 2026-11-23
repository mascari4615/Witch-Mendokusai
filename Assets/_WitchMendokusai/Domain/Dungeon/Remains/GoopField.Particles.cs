using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 액체 위 입자. 타는 칸에서 불길이 서서 일렁이고 연기와 불티가 오름, 감전된 칸에서 짧은 불똥이 옆으로 튐. 공유 파티클에 Emit 만
	// 불길과 연기는 절차적 셰이더 (FireFlame) 를 세운 판 (VerticalBillboard) 에 그림. 모양과 일렁임과 색은 셰이더, 개수와 크기와 수명은 여기
	public sealed partial class GoopField
	{
		private static readonly int SMOKE_ID = Shader.PropertyToID("_Smoke");

		private ParticleSystem flames;
		private ParticleSystem smoke;
		private ParticleSystem sparks;
		private Material flameMaterial;
		private Material smokeMaterial;
		private float flameCarry;
		private float smokeCarry;
		private float emberCarry;
		private float sparkCarry;
		private readonly List<Vector2Int> burningCells = new();
		private readonly List<Vector2Int> shockedCells = new();

		private void CreateParticles(Transform parent)
		{
			flameMaterial = new Material(settings.FireFlameShader) { name = "GoopFlames", hideFlags = HideFlags.HideAndDontSave };
			smokeMaterial = new Material(settings.FireFlameShader) { name = "GoopSmoke", hideFlags = HideFlags.HideAndDontSave };
			smokeMaterial.SetFloat(SMOKE_ID, 1f);
			// 연기를 먼저 그려 불길이 연기 앞에 옴
			smoke = BuildParticles(parent, "GoopSmoke", ParticleSystemRenderMode.VerticalBillboard, smokeMaterial, settings.SmokeGrowTo);
			flames = BuildParticles(parent, "GoopFlames", ParticleSystemRenderMode.VerticalBillboard, flameMaterial, settings.FlameShrinkTo);
			sparks = BuildParticles(parent, "GoopSparks", ParticleSystemRenderMode.Stretch, settings.GoopParticleMaterial, 0f);
			smoke.GetComponent<ParticleSystemRenderer>().sortingFudge = settings.SmokeSortingFudge;
			UseShaderStreams(smoke);
			UseShaderStreams(flames);
			FadeOverLifetime(sparks);
		}

		private ParticleSystem BuildParticles(Transform parent, string systemName, ParticleSystemRenderMode renderMode, Material material, float endSize)
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
			main.startSize3D = true;
			ParticleSystem.EmissionModule emission = system.emission;
			emission.enabled = false;
			ParticleSystem.ShapeModule shape = system.shape;
			shape.enabled = false;
			ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
			size.enabled = true;
			size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize));
			ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
			renderer.renderMode = renderMode;
			renderer.sharedMaterial = material;
			if (renderMode == ParticleSystemRenderMode.Stretch)
			{
				renderer.velocityScale = settings.SparkStretch;
				renderer.lengthScale = 1f;
			}
			system.Play();
			return system;
		}

		// 셰이더가 판마다 다른 잡음 자리 (StableRandom) 와 나이 (AgePercent) 로 일렁임과 페이드를 그림. TEXCOORD0 하나에 xy, z, w 로 붙음
		private static void UseShaderStreams(ParticleSystem system)
		{
			system.GetComponent<ParticleSystemRenderer>().SetActiveVertexStreams(new List<ParticleSystemVertexStream>
			{
				ParticleSystemVertexStream.Position,
				ParticleSystemVertexStream.Color,
				ParticleSystemVertexStream.UV,
				ParticleSystemVertexStream.StableRandomX,
				ParticleSystemVertexStream.AgePercent,
			});
		}

		// 불똥과 불티는 셰이더 페이드가 없어 색 알파로 사라짐
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
			int flameCount = Take(ref flameCarry, burningCells.Count * settings.FlamesPerCellPerSecond * deltaTime);
			for (int i = 0; i < flameCount && burningCells.Count > 0; i++)
			{
				Vector3 at = CenterOf(burningCells[Random.Range(0, burningCells.Count)], groundHeight) + RandomInCell();
				float width = Random.Range(settings.FlameSize.x, settings.FlameSize.y);
				float height = width * settings.FlameAspect;
				ParticleSystem.EmitParams flame = new()
				{
					// 판 가운데가 기준이라 밑동이 바닥에 닿게 높이의 반만큼 올림
					position = at + Vector3.up * height * .5f,
					velocity = new Vector3(Random.Range(-settings.FlameDrift, settings.FlameDrift), Random.Range(settings.FlameRise.x, settings.FlameRise.y), Random.Range(-settings.FlameDrift, settings.FlameDrift)),
					startSize3D = new Vector3(width, height, 1f),
					rotation = Random.Range(-settings.FlameTilt, settings.FlameTilt),
					startLifetime = Random.Range(settings.FlameLife.x, settings.FlameLife.y),
					startColor = settings.FlameColor,
				};
				flames.Emit(flame, 1);
			}

			int smokeCount = Take(ref smokeCarry, burningCells.Count * settings.SmokePerCellPerSecond * deltaTime);
			for (int i = 0; i < smokeCount && burningCells.Count > 0; i++)
			{
				Vector3 at = CenterOf(burningCells[Random.Range(0, burningCells.Count)], groundHeight) + RandomInCell() + Vector3.up * settings.SmokeStartHeight;
				float size = Random.Range(settings.SmokeSize.x, settings.SmokeSize.y);
				ParticleSystem.EmitParams puff = new()
				{
					position = at,
					velocity = new Vector3(Random.Range(-settings.SmokeDrift, settings.SmokeDrift), Random.Range(settings.SmokeRise.x, settings.SmokeRise.y), Random.Range(-settings.SmokeDrift, settings.SmokeDrift)),
					startSize3D = new Vector3(size, size, 1f),
					rotation = Random.Range(0f, 360f),
					startLifetime = Random.Range(settings.SmokeLife.x, settings.SmokeLife.y),
					startColor = Color.white,
				};
				smoke.Emit(puff, 1);
			}

			int emberCount = Take(ref emberCarry, burningCells.Count * settings.EmbersPerCellPerSecond * deltaTime);
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
			flames.Clear();
			smoke.Clear();
			sparks.Clear();
			flameCarry = 0f;
			smokeCarry = 0f;
			emberCarry = 0f;
			sparkCarry = 0f;
		}

		private void DisposeParticles()
		{
			Object.Destroy(flameMaterial);
			Object.Destroy(smokeMaterial);
		}
	}
}
