using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 번개 강한 판 (강력 스킬용). 머리 위를 먹구름이 덮고, 첫 낙뢰 하나가 가장 크게 떨어진 뒤 둘레에 여섯 줄기가 빨라지며 내리꽂힘
	/// - 흐름: 먹구름 (빛 받는 구름 입자, 속 번쩍임, 빗줄기) -> 착지 자리 예고 빛 -> 첫 낙뢰 (가장 굵음) -> 연쇄 여섯 (간격이 줄어듦) -> 잔류 (그을림과 먼지)
	/// - 번개 줄기는 꺾인 관 메시 (가산 심과 빛 겉) 이고 표시 동안 두 번 모양을 바꿈. 접지 광원 둘을 번갈아 씀 (같은 시각 2개 이하)
	/// - 흔들림, 정지, 섬광은 화면 연출 박자표 (FeelUltimateFx) 몫. 여기는 월드 안 물체와 빛만
	/// - 바닥 고리와 고리 안 감전 아크는 뺌 (사용자 판정 2026-11-21: 뜻을 알 수 없고 어울리지 않음). 낙뢰 줄기만
	/// - 설계 memo wm/design/ult3d-all-20261120/10_thunder.md, 제작 기록 ult3d-batch2-20261121
	/// - 재생: 개발 창 fx ultimate ThunderHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_ThunderHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(ThunderHeavyUltimateFx))]
	public sealed class ThunderHeavyUltimateFx : WorldUltimateFx
	{
		private sealed class Bolt
		{
			public Mesh Core;
			public Mesh Glow;
			public GameObject CoreObject;
			public GameObject GlowObject;
			public Vector3 Top;
			public Vector3 Ground;
			public float Thickness;
		}

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color boltCore = new(3.2f, 3.2f, 4f);
		[SerializeField, ColorUsage(false, true)] private Color boltGlow = new(0.9f, 1.1f, 2.6f);
		[SerializeField] private Color groundLightColor = new(0.85f, 0.9f, 1f, 1f);
		[SerializeField] private Color cloudColor = new(0.42f, 0.45f, 0.55f, 0.55f);
		[SerializeField, ColorUsage(false, true)] private Color cloudFlashColor = new(0.55f, 0.6f, 1f);
		[SerializeField] private Color sparkColor = new(1f, 0.95f, 0.6f, 1f);
		[SerializeField] private Color rainColor = new(0.7f, 0.75f, 0.85f, 0.45f);
		[SerializeField] private Color dustColor = new(0.42f, 0.4f, 0.38f, 0.8f);

		[Header("구름")]
		[SerializeField, Min(0)] private int cloudCount = 46;
		[SerializeField, Min(1f), Tooltip("구름 천장 반지름 (표적 중심)")] private float cloudRadius = 8f;
		[SerializeField, Tooltip("구름 높이 (앞 가장자리, 뒤 가장자리). 시야 위쪽 7.5 안에 앞쪽이 보이게")] private Vector2 cloudHeight = new(4.6f, 7.4f);
		[SerializeField] private Vector2 cloudSize = new(4f, 6.5f);
		[SerializeField, Min(0.1f)] private float cloudGatherSeconds = 0.8f;
		[SerializeField, Min(0)] private int rainPerSecond = 260;
		[SerializeField, Range(0.1f, 1f), Tooltip("구름 속 번쩍임 판 크기 / 구름 크기")] private float cloudFlashSize = 0.45f;

		[Header("줄기")]
		[SerializeField, Min(1f), Tooltip("구름에서 땅까지. 높으면 줄기 위가 화면 위로 잘림 (독립 검토 3회차)")] private float boltHeight = 6f;
		[SerializeField, Min(2)] private int boltNodes = 14;
		[SerializeField, Min(0f)] private float boltJitter = 0.55f;
		[SerializeField, Min(0.01f), Tooltip("심 반지름 (첫 낙뢰는 firstScale 배)")] private float coreRadius = 0.16f;
		[SerializeField, Min(1f), Tooltip("빛 겉 반지름 / 심 반지름. 크면 빛 번짐에 묻혀 흰 기둥으로 읽힘 (1회차 촬영)")] private float glowRatio = 2f;
		[SerializeField, Range(0f, 1f), Tooltip("빛 겉 밝기 배율")] private float glowAlpha = 0.2f;
		[SerializeField, Min(1f)] private float firstScale = 2.2f;
		[SerializeField, Min(0)] private int firstBranches = 6;
		[SerializeField] private Vector2Int chainBranches = new(2, 4);
		[SerializeField, Min(0.05f), Tooltip("줄기 하나 표시 시간 (v2 0.26)")] private float boltShowSeconds = 0.26f;
		[SerializeField, Min(1f), Tooltip("연쇄 줄기가 꽂히는 시전자 둘레 반지름")] private float chainRadius = 2.6f;
		[SerializeField, Tooltip("연쇄 여섯의 첫 낙뢰 뒤 시각. 간격이 줄어들게")] private float[] chainOffsets = { 0.24f, 0.4f, 0.53f, 0.63f, 0.71f, 0.78f };

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("첫 낙뢰 시각. 앞은 구름과 예고")] private float firstStrike = 1f;
		[SerializeField, Min(0.1f), Tooltip("마지막 연쇄 뒤 남는 시간 (그을림, 먼지, 장면 빛 복귀 시작)")] private float lingerSeconds = 1.4f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.4f;
		[SerializeField, Min(0.05f), Tooltip("첫 낙뢰 자리 땅이 달아오르는 시간")] private float telegraphSeconds = 0.7f;
		[SerializeField, Min(0.02f), Tooltip("연쇄 줄기 자리 땅이 달아오르는 시간")] private float chainTelegraphSeconds = 0.12f;
		[SerializeField, Range(0f, 2f), Tooltip("착지 자리 예고 빛 세기. 낮으면 예고가 안 보임 (독립 검토 3회차)")] private float telegraphGlow = 0.7f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.45f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.45f;
		[SerializeField, Min(0f)] private float firstLightIntensity = 40f;
		[SerializeField, Min(0f)] private float chainLightIntensity = 18f;
		[SerializeField, Min(1f)] private float lightRange = 13f;
		[SerializeField, Min(0.01f), Tooltip("다음 줄기 전에 거의 꺼지게")] private float lightHalfLife = 0.06f;

		[Header("후처리")]
		[SerializeField, Min(0f)] private float bloom = 1f;
		[SerializeField, Range(-100f, 0f)] private float stormSaturation = -12f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.3f;

		[Header("바닥")]
		[SerializeField, Min(0.5f)] private float scorchSize = 2.6f;
		[SerializeField, Min(0.5f)] private float firstScorchSize = 5.5f;
		[SerializeField, Min(0)] private int sparksPerStrike = 40;
		[SerializeField, Min(1f)] private float windSpeed = 12f;
		[SerializeField, Min(1f)] private float windReach = 5f;

		// ScaleWorld 배율. 실행용 복제본에서만 1 이 아님 (리터럴 크기 값에 곱함)
		[System.NonSerialized] private float sizeScale = 1f;

		private float LastStrike => firstStrike + chainOffsets[chainOffsets.Length - 1];
		private float RestoreTime => LastStrike + lingerSeconds * 0.5f;

		public override float DurationSeconds => LastStrike + lingerSeconds;

		public override void ScaleWorld(float scale)
		{
			sizeScale = scale;
			cloudRadius *= scale;
			cloudSize *= scale;
			cloudCount = Mathf.RoundToInt(cloudCount * scale);
			coreRadius *= scale;
			boltJitter *= scale;
			chainRadius *= scale;
			lightRange *= scale;
			scorchSize *= scale;
			firstScorchSize *= scale;
			windReach *= scale;
			sparksPerStrike = Mathf.RoundToInt(sparksPerStrike * scale);
			// 구름 높이와 줄기 길이는 덜 키움 (시야 위쪽 7.5)
			boltHeight *= Mathf.Pow(scale, 0.3f);
		}

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Vector3 center = stage.Target;
			Light[] lights = { WorldLight(stage, "ThunderLightA", LightType.Point, groundLightColor, lightRange), WorldLight(stage, "ThunderLightB", LightType.Point, groundLightColor, lightRange) };
			Material coreMaterial = run.Additive(UltimateFxTextures.White, boltCore);
			Material glowMaterial = run.Additive(UltimateFxTextures.White, boltGlow * glowAlpha);
			Texture2D scorch = ScorchTexture(run, 31, new Color(0.05f, 0.05f, 0.07f, 0.85f), new Color(0.5f, 0.75f, 1f, 1f), 0.75f);
			ParticleSystem sparks = run.Particles("ThunderSparks", run.Additive(UltimateFxTextures.Spark, Color.white), (sparksPerStrike + 10) * 8, 1.4f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

			// 착지 자리: 첫 낙뢰는 표적, 연쇄는 둘레를 돌며 (화면 안에 고르게)
			List<Vector3> spots = new() { center };
			float offset = Random.value * 360f;
			for (int i = 0; i < chainOffsets.Length; i++)
			{
				float angle = (offset + i * 360f / chainOffsets.Length * 2.5f) * Mathf.Deg2Rad;
				Vector3 spot = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * chainRadius * Random.Range(0.85f, 1.1f);
				spot.y = UltimateFxStage.GroundHeight(spot);
				spots.Add(spot);
			}

			ComposeClouds(stage, center, spots);
			for (int i = 0; i < spots.Count; i++)
			{
				bool first = i == 0;
				float time = first ? firstStrike : firstStrike + chainOffsets[i - 1];
				ComposeStrike(stage, spots[i], time, first, lights[i % 2], coreMaterial, glowMaterial, scorch, sparks);
				// 착지 자리 예고: 줄기마다 직전에 땅이 푸르게 달아오름 (첫 낙뢰는 길게)
				float lead = first ? telegraphSeconds : chainTelegraphSeconds;
				float glowStart = Mathf.Max(0f, time - lead);
				GroundGlow(run, spots[i], (first ? 3.6f : 2.2f) * sizeScale, boltGlow * telegraphGlow, glowStart, time - glowStart, 0.05f, time - glowStart + 0.05f);
			}
			// 연쇄 광원은 트랙 끝에 세기 0 만 됨. 마지막 줄기가 꺼진 뒤 둘 다 끔
			run.At(LastStrike + 0.3f, () =>
			{
				lights[0].enabled = false;
				lights[1].enabled = false;
			});
			ComposeGrade(stage);
		}

		// 먹구름 천장: 빛 받는 큰 구름 입자가 모여 짙어짐. 줄기 직전 그 둘레 구름 속이 번쩍임 (광원 아님). 빗줄기는 번개 순간 밝아짐 (빛 받는 입자)
		private void ComposeClouds(Stage stage, Vector3 center, List<Vector3> spots)
		{
			UltimateFxRun run = stage.Run;
			float total = DurationSeconds;
			ParticleSystem clouds = run.Particles("ThunderClouds", LitParticle(run, UltimateFxTextures.Smoke, 1.2f), cloudCount + 4, 0f);
			UltimateFxStage.SetLifetimeLook(clouds, UltimateFxStage.FadeGradient(Color.white, 0.18f, 0.82f), AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.1f));
			ParticleSystem.NoiseModule drift = clouds.noise;
			drift.enabled = true;
			drift.strength = 0.25f;
			drift.frequency = 0.2f;
			for (int i = 0; i < cloudCount; i++)
			{
				Vector3 offset = UltimateFxStage.RandomRingPoint(0f, cloudRadius);
				// 카메라 쪽 (앞) 은 낮게, 뒤는 높게 기운 천장
				float front = Mathf.Clamp01(0.5f + Vector3.Dot(offset, stage.ToCamera) / (cloudRadius * 2f));
				Vector3 position = center + offset + Vector3.up * Mathf.Lerp(cloudHeight.y, cloudHeight.x, front);
				Color tone = cloudColor * Random.Range(0.8f, 1.1f);
				tone.a = cloudColor.a;
				float delay = Random.Range(0f, cloudGatherSeconds * 0.6f);
				run.At(delay, () => UltimateFxStage.Emit(clouds, position + Random.insideUnitSphere * 0.8f, -offset.normalized * 0.4f, Random.Range(cloudSize.x, cloudSize.y), total - delay + 0.6f, tone, Random.value * 360f, Random.Range(-6f, 6f)));
			}

			// 입자 시작 색은 1 에서 잘려 HDR 세기는 재질 색에
			Material flashMaterial = run.Additive(UltimateFxTextures.Soft, cloudFlashColor);
			ParticleSystem cloudFlash = run.Particles("ThunderCloudFlash", flashMaterial, 64, 0f);
			UltimateFxStage.SetLifetimeLook(cloudFlash, UltimateFxStage.FadeGradient(Color.white, 0.05f, 0.3f), null);
			// 줄기마다 직전 0.06초 구름 속 번쩍임. 구름 모이는 동안에도 몇 번 (예고)
			List<float> flashTimes = new() { cloudGatherSeconds * 0.5f, cloudGatherSeconds * 0.8f, firstStrike - 0.18f };
			List<Vector3> flashSpots = new() { spots[1 % spots.Count], spots[2 % spots.Count], spots[0] };
			for (int i = 0; i < spots.Count; i++)
			{
				flashTimes.Add((i == 0 ? firstStrike : firstStrike + chainOffsets[i - 1]) - 0.06f);
				flashSpots.Add(spots[i]);
			}
			for (int i = 0; i < flashTimes.Count; i++)
			{
				Vector3 spot = flashSpots[i];
				float time = Mathf.Max(0f, flashTimes[i]);
				run.At(time, () =>
				{
					for (int puff = 0; puff < 3; puff++)
					{
						Vector3 position = spot + UltimateFxStage.RandomRingPoint(0f, 2f * sizeScale) + Vector3.up * Mathf.Lerp(cloudHeight.x, cloudHeight.y, 0.4f);
						UltimateFxStage.Emit(cloudFlash, position, Vector3.zero, Random.Range(cloudSize.x, cloudSize.y) * cloudFlashSize, 0.16f, Color.white);
					}
				});
			}

			ParticleSystem rain = run.Particles("ThunderRain", LitParticle(run, UltimateFxTextures.Line, 0.3f), rainPerSecond * 2 + 20, 0f, true);
			ParticleSystemRenderer rainRenderer = rain.GetComponent<ParticleSystemRenderer>();
			rainRenderer.velocityScale = 0.035f;
			rainRenderer.lengthScale = 1f;
			float carry = 0f;
			run.Track(cloudGatherSeconds * 0.5f, RestoreTime - cloudGatherSeconds * 0.5f, _ =>
			{
				carry += rainPerSecond * run.Delta;
				int count = Mathf.FloorToInt(carry);
				carry -= count;
				for (int i = 0; i < count; i++)
				{
					Vector3 position = center + UltimateFxStage.RandomRingPoint(0f, cloudRadius) + Vector3.up * cloudHeight.x;
					UltimateFxStage.Emit(rain, position, new Vector3(0.6f, -22f, 0.2f), Random.Range(0.03f, 0.05f), 0.32f, rainColor);
				}
			});
		}

		// 낙뢰 한 줄기: 관 메시 둘 (심, 빛 겉) 이 표시 동안 두 번 모양을 바꿈. 접지 광원, 그을림, 불꽃, 먼지, 풀 바람
		private void ComposeStrike(Stage stage, Vector3 ground, float time, bool first, Light light, Material coreMaterial, Material glowMaterial, Texture2D scorch, ParticleSystem sparks)
		{
			UltimateFxRun run = stage.Run;
			float scale = first ? firstScale : 1f;
			Bolt bolt = new()
			{
				Core = Keep(run, new Mesh { name = "ThunderBoltCore" }),
				Glow = Keep(run, new Mesh { name = "ThunderBoltGlow" }),
				Ground = ground,
				Top = ground + Vector3.up * boltHeight + Random.insideUnitSphere * 1.2f * sizeScale,
				Thickness = coreRadius * scale,
			};
			bolt.CoreObject = UltimateFxStage.CreateMeshObject("ThunderBoltCore", run.Root, bolt.Core, coreMaterial);
			bolt.GlowObject = UltimateFxStage.CreateMeshObject("ThunderBoltGlow", run.Root, bolt.Glow, glowMaterial);
			bolt.CoreObject.SetActive(false);
			bolt.GlowObject.SetActive(false);
			int branches = first ? firstBranches : Random.Range(chainBranches.x, chainBranches.y + 1);
			for (int shape = 0; shape < 3; shape++)
			{
				float shapeTime = time + shape * boltShowSeconds / 3f;
				run.At(shapeTime, () =>
				{
					Reshape(bolt, branches);
					bolt.CoreObject.SetActive(true);
					bolt.GlowObject.SetActive(true);
				});
			}
			run.At(time + boltShowSeconds, () =>
			{
				bolt.CoreObject.SetActive(false);
				bolt.GlowObject.SetActive(false);
			});

			float intensity = first ? firstLightIntensity : chainLightIntensity;
			run.At(time, () =>
			{
				light.enabled = true;
				light.transform.position = ground + Vector3.up * 1.4f + stage.ToCamera * 0.6f;
			});
			// 같은 광원을 다음에 쓰는 줄기 전에 트랙이 끝나게 (첫 낙뢰 0.38초, 연쇄 0.2초)
			run.Track(time, first ? 0.38f : 0.2f, _ => light.intensity = intensity * Spike(run.Now, time, 0f, lightHalfLife * (first ? 2.5f : 1f)), () => light.intensity = 0f);

			run.At(time, () =>
			{
				int count = first ? sparksPerStrike * 3 : sparksPerStrike;
				for (int i = 0; i < count; i++)
				{
					Vector3 direction = (Random.onUnitSphere + Vector3.up * 1.2f).normalized;
					UltimateFxStage.Emit(sparks, ground + Vector3.up * 0.15f, direction * Random.Range(3f, 9f) * (first ? 1.4f : 1f) * sizeScale, Random.Range(0.05f, 0.11f), Random.Range(0.25f, 0.6f), sparkColor);
				}
				DustRing(run, ground, first ? 30 : 12, (first ? 3f : 1.6f) * sizeScale, dustColor, (first ? 1.8f : 1.1f) * sizeScale);
				// 풀 반응은 첫 낙뢰만. React 는 모든 풀의 시작 시각을 되돌려 연달아 부르면 풀이 멈춤 (독립 검토)
				if (first)
					run.React(ground, windSpeed, windReach * 1.6f, true);
				if (first)
					run.Debris(ground, 1.2f * sizeScale, 14, 9f * sizeScale, new Color(0.3f, 0.29f, 0.28f, 1f), new Color(1f, 0.85f, 0.5f, 1f), 0.4f);
			});
			GroundGlow(run, ground, (first ? 8f : 4.5f) * sizeScale, boltGlow * (first ? 1.2f : 0.8f), time, 0.01f, first ? 0.25f : 0.12f, 1.5f);
			GroundDecal(run, scorch, ground, first ? firstScorchSize : scorchSize, time, 0.05f, RestoreTime + 0.6f, 1.2f, first ? 1 : 0);
		}

		private void Reshape(Bolt bolt, int branches)
		{
			List<List<Vector3>> strands = UltimateMeshFactory.BoltStrands(bolt.Top, bolt.Ground, boltNodes, boltJitter, branches, boltHeight * 0.32f);
			List<IReadOnlyList<float>> coreRadii = new();
			List<IReadOnlyList<float>> glowRadii = new();
			for (int strand = 0; strand < strands.Count; strand++)
			{
				List<float> core = new();
				List<float> glow = new();
				float width = strand == 0 ? bolt.Thickness : bolt.Thickness * 0.45f;
				for (int point = 0; point < strands[strand].Count; point++)
				{
					float along = point / (float)Mathf.Max(1, strands[strand].Count - 1);
					// 줄기는 땅 쪽이 굵고, 가지는 끝으로 가늘게
					float taper = strand == 0 ? Mathf.Lerp(0.6f, 1.1f, along) : Mathf.Lerp(1f, 0.3f, along);
					core.Add(width * taper);
					glow.Add(width * taper * glowRatio);
				}
				coreRadii.Add(core);
				glowRadii.Add(glow);
			}
			List<IReadOnlyList<Vector3>> paths = new(strands);
			// 줄기는 땅 쪽이 밝게 (끝 알파 1), 가지만 끝으로 옅게
			UltimateMeshFactory.FillTube(bolt.Core, paths, coreRadii, 6, 0.3f, 1f);
			UltimateMeshFactory.FillTube(bolt.Glow, paths, glowRadii, 8, 0f, 0.8f);
		}

		private void ComposeGrade(Stage stage)
		{
			float restore = RestoreTime;
			stage.BloomTint = new Color(0.75f, 0.85f, 1f);
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Plateau(now, 0f, cloudGatherSeconds, restore, restoreSeconds));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Plateau(now, 0f, cloudGatherSeconds, restore, restoreSeconds));
			stage.Saturation = now => stormSaturation * Plateau(now, 0f, cloudGatherSeconds, restore, restoreSeconds);
			stage.Vignette = now => vignette * Plateau(now, 0f, cloudGatherSeconds, restore, restoreSeconds);
			stage.Filter = now => Color.Lerp(Color.white, new Color(0.86f, 0.9f, 1f), Plateau(now, 0f, cloudGatherSeconds, restore, restoreSeconds));
			stage.Bloom = now => bloom * Plateau(now, firstStrike - 0.2f, 0.2f, restore, restoreSeconds);
		}
	}
}
