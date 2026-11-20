using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 창 왕관 강한 판 (강력 스킬용). 하늘에서 분홍 창 수십 자루가 꽂히고, 꽂힌 자리마다 결정이 솟아 가운데가 높은 왕관이 됨
	/// - 흐름: 하늘 스포트 예고 (크기 고정, 밝기만 0.5초) -> 창 비 (0.12초 낙하, 0.6초에 걸쳐) -> 왕관 (흑백 1장, 흰 실루엣 1장, 심 점광, 흔들림 5회, 적 들어올림)
	///   -> 유지 -> 분해 (결정이 조각 메시로 깨져 땅에 남음) -> 장면 빛 복귀
	/// - 약한 판과 같은 공통 부품 (SpearCrownWorldFx) 위에 고유 단계 셋: 창 비가 씨앗, 적 들어올림, 분해. 왕관 배열과 거대 결정은 값 확대
	/// - 원 레퍼런스 m02 (15fps): 창 비 0.6s, 흑백 1장, 흰 실루엣 1장, 유지와 축소 0.4s, 분해 0.33s
	/// - 재생: 개발 창 fx ultimate SpearCrownHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_SpearCrownHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(SpearCrownHeavyUltimateFx))]
	public sealed class SpearCrownHeavyUltimateFx : SpearCrownWorldFx
	{
		[Header("창")]
		[SerializeField] private Color spearBlade = new(1f, 0.42f, 0.78f, 1f);
		[SerializeField] private Color spearShaft = new(0.16f, 0.13f, 0.16f, 1f);
		[SerializeField] private Color spearGuard = new(0.9f, 0.75f, 0.5f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color spearGlow = new(1.9f, 0.35f, 1.1f);
		[SerializeField, Min(0.5f)] private float spearLength = 2.6f;
		[SerializeField, Min(0.5f), Tooltip("창 굵기 배율 (쿼터뷰에서 칼날이 읽히게)")] private float spearWidth = 2.2f;
		[SerializeField, Min(2f)] private float spearHeight = 14f;
		[SerializeField, Range(0f, 40f), Tooltip("가운데 쪽으로 기우는 각도")] private float spearLean = 16f;
		[SerializeField, Range(0f, 0.6f), Tooltip("박히는 깊이 (길이 비율)")] private float spearSink = 0.25f;
		[SerializeField, Min(0.05f), Tooltip("왕관 직전 꽂힌 창이 달아오르는 시간")] private float spearChargeSeconds = 0.2f;
		[SerializeField, Min(0f)] private float spearChargeGain = 2.5f;

		[Header("왕관 배열")]
		[SerializeField, Min(0)] private int innerCount = 6;
		[SerializeField, Min(0f)] private float innerRadius = 1.1f;
		[SerializeField] private Vector2 innerHeight = new(2.4f, 3.4f);
		[SerializeField, Min(0)] private int middleCount = 10;
		[SerializeField, Min(0f)] private float middleRadius = 2.3f;
		[SerializeField] private Vector2 middleHeight = new(1.5f, 2.3f);
		[SerializeField, Min(0)] private int outerCount = 14;
		[SerializeField, Min(0f)] private float outerRadius = 3.7f;
		[SerializeField] private Vector2 outerHeight = new(0.9f, 1.5f);
		[SerializeField] private Vector2 crystalWidth = new(0.9f, 1.3f);
		[SerializeField, Min(1f), Tooltip("가운데 거대 결정 높이. 시야 위쪽 7.5 안")] private float giantHeight = 5.6f;
		[SerializeField, Min(0.3f)] private float giantWidth = 3.4f;
		[SerializeField, Min(1), Tooltip("가운데 거대 결정 무리 수 (가장 높은 하나와 기울어진 나머지)")] private int giantCount = 3;
		[SerializeField, Range(0f, 1f), Tooltip("거대 결정 발광 배율. 높으면 빛 번짐에 묻혀 면이 사라지고 평평한 빛 띠로 읽힘")] private float giantEmissionScale = 0.3f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("실측 450~500ms. 스포트가 크기 그대로 밝아짐")] private float telegraphSeconds = 0.5f;
		[SerializeField, Min(0f)] private float rainStart = 0.08f;
		[SerializeField, Min(0.1f), Tooltip("m02 창 비 0.6s")] private float rainSeconds = 0.55f;
		[SerializeField, Min(0.03f), Tooltip("실측 100~250ms")] private float spearFallSeconds = 0.12f;
		[SerializeField, Min(0.02f)] private float crystalRiseSeconds = 0.1f;
		[SerializeField, Min(0.02f)] private float giantRiseSeconds = 0.16f;
		[SerializeField, Min(0f), Tooltip("고리마다 솟는 시차 (안쪽부터)")] private float ringDelay = 0.03f;
		[SerializeField, Min(0.1f), Tooltip("왕관 유지 뒤 분해 시각 (왕관 기준)")] private float holdSeconds = 0.85f;
		[SerializeField, Min(0.05f)] private float breakSeconds = 0.22f;
		[SerializeField, Min(0.1f)] private float shardLinger = 1.2f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.55f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.65f;
		[SerializeField, Min(0f)] private float spotIntensity = 30f;
		[SerializeField, Range(5f, 90f)] private float spotAngle = 44f;
		[SerializeField, Min(1f)] private float spotHeight = 12f;
		[SerializeField, Min(0f)] private float coreIntensity = 28f;
		[SerializeField, Min(1f)] private float coreRange = 12f;
		[SerializeField, Min(0f)] private float coreHeight = 2.6f;
		[SerializeField, Min(0f), Tooltip("심 점광을 거대 결정 앞 (카메라 쪽) 으로 빼는 거리. 안쪽에 두면 바깥 면이 빛을 못 받아 하얀 기둥으로 읽힘")] private float coreFront = 1.6f;
		[SerializeField, Min(0.01f), Tooltip("실측 접지 뒤 100~150ms 최대")] private float coreRise = 0.12f;
		[SerializeField, Min(0.05f), Tooltip("실측 반감 0.5~1.8s")] private float coreHalfLife = 0.6f;

		[Header("후처리와 카메라")]
		[SerializeField, Min(0f)] private float bloomCrown = 1.5f;
		[SerializeField, Min(0f)] private float bloomHold = 0.7f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.25f;
		[SerializeField, Min(0f), Tooltip("흰 실루엣 노출 (EV, 한 장)")] private float silhouetteExposure = 1.6f;
		[SerializeField, Tooltip("흑백 임팩트 노출 (EV, 한 장)")] private float impactExposure = -0.5f;
		[SerializeField, Range(-100f, 100f)] private float impactContrast = 60f;
		[SerializeField, Min(0f)] private float shakeStrength = 1.4f;
		[SerializeField, Min(1)] private int shakeCount = 5;
		[SerializeField, Min(0.01f)] private float shakeInterval = 0.07f;
		[SerializeField, Range(0f, 1f)] private float shakeDecay = 0.7f;

		[Header("반응")]
		[SerializeField, Min(0f), Tooltip("시전자 둘레 이 반지름 안에는 결정을 세우지 않음 (인물이 결정에 묻히지 않게)")] private float casterClearance = 1.2f;
		[SerializeField, Min(0f), Tooltip("왕관 정점에 결정 면 발광이 치솟는 배율")] private float emissionPeak = 0.8f;
		[SerializeField, Min(0f), Tooltip("결정 안 적을 띄우는 높이 (연출만)")] private float liftHeight = 0.4f;
		[SerializeField, Min(0.05f)] private float liftSeconds = 0.3f;
		[SerializeField, Min(0)] private int rootRockCount = 12;
		[SerializeField, Min(0)] private int sparkleCount = 90;
		[SerializeField, Min(0)] private int dustCount = 48;
		[SerializeField, Min(0)] private int rayCount = 60;
		[SerializeField, Min(0)] private int shardsPerUnitHeight = 3;
		[SerializeField, Min(0f)] private float shardSpeed = 6f;
		[SerializeField, Min(0)] private int gatherCount = 50;
		[SerializeField, Min(1f)] private float windSpeed = 14f;
		[SerializeField, Min(1f)] private float windReach = 10f;
		[SerializeField, Min(1f)] private float crackSize = 14f;
		[SerializeField, Min(0.5f), Tooltip("분해 뒤 균열이 남는 시간. 실측 2.5초 이상 잔류")] private float crackLinger = 1.1f;

		private float CrownTime => rainStart + rainSeconds + spearFallSeconds;
		private float BreakTime => CrownTime + holdSeconds;

		public override float DurationSeconds => BreakTime + breakSeconds + shardLinger;

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Vector3 center = stage.Target;
			Mesh[] meshes = CrystalMeshes(run);
			Material material = CrystalMaterial(run);

			// 창이 꽂히는 자리에서 결정이 솟음. 안쪽부터 고리 셋
			List<(Vector3 position, int ring)> spots = new();
			AddRing(spots, center, innerCount, innerRadius, 0);
			AddRing(spots, center, middleCount, middleRadius, 1);
			AddRing(spots, center, outerCount, outerRadius, 2);
			Vector3 caster = run.Caster;
			spots.RemoveAll(spot => new Vector2(spot.position.x - caster.x, spot.position.z - caster.z).magnitude < casterClearance);

			List<CrystalPiece> pieces = new();
			for (int i = 0; i < spots.Count; i++)
			{
				(Vector3 position, int ring) = spots[i];
				Vector2 heightRange = ring == 0 ? innerHeight : ring == 1 ? middleHeight : outerHeight;
				float height = Random.Range(heightRange.x, heightRange.y);
				CrystalPiece piece = PlaceCrystal(run, meshes[i % meshes.Length], material, position, position - center, height, Random.Range(crystalWidth.x, crystalWidth.y), ring == 0);
				pieces.Add(piece);
				Rise(run, piece, CrownTime + ring * ringDelay + Random.Range(0f, 0.02f), crystalRiseSeconds);
			}
			Material giantMaterial = CrystalMaterial(run, giantEmissionScale);
			// 가운데 거대 결정 무리: 가장 높은 하나와 기울어진 둘. 한 기둥이면 쿼터뷰에서 빛 기둥으로 읽힘 (독립 검토 지적)
			float cluster = Random.value * 360f;
			for (int i = 0; i < giantCount; i++)
			{
				float ratio = i == 0 ? 1f : Mathf.Lerp(0.62f, 0.78f, Random.value);
				Vector3 lean = i == 0 ? Vector3.zero : Quaternion.Euler(0f, cluster + i * 360f / Mathf.Max(1, giantCount - 1), 0f) * Vector3.forward;
				CrystalPiece giant = PlaceCrystal(run, meshes[i % meshes.Length], giantMaterial, center + lean * giantWidth * 0.12f, lean, giantHeight * ratio, giantWidth * Mathf.Lerp(0.75f, 1f, ratio), true);
				if (i == 0)
				{
					giant.Transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
					giant.Buried = center - Vector3.up * giantHeight * 1.05f;
					giant.Transform.position = giant.Buried;
				}
				pieces.Add(giant);
				Rise(run, giant, CrownTime + i * 0.03f, giantRiseSeconds);
			}

			ComposeTelegraph(stage, center);
			ComposeRain(stage, center, spots);
			ComposeCrown(stage, center, material, pieces);
			Shatter(run, material, meshes[1 % meshes.Length], pieces, BreakTime, shardsPerUnitHeight, shardSpeed, shardLinger, breakSeconds);
			run.Track(BreakTime, breakSeconds, progress =>
			{
				giantMaterial.SetFloat(DISSOLVE_DIRECTION_ID, 1f);
				giantMaterial.SetFloat(DISSOLVE_ID, UltimateFxRun.EaseOut(progress));
			});
			run.At(BreakTime, () =>
			{
				run.Shake(shakeStrength * 0.5f);
				Sparkle(run, center, sparkleCount / 2, outerRadius, 1.2f);
			});
			ComposeGrade(stage);
		}

		private static void AddRing(List<(Vector3 position, int ring)> spots, Vector3 center, int count, float radius, int ring)
		{
			float offset = Random.value * Mathf.PI * 2f;
			for (int i = 0; i < count; i++)
			{
				float angle = offset + (i + Random.Range(-0.25f, 0.25f)) / Mathf.Max(1, count) * Mathf.PI * 2f;
				float distance = radius * Random.Range(0.88f, 1.12f);
				spots.Add((center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance, ring));
			}
		}

		// 하늘 스포트가 표적에 분홍 빛 (크기 고정, 밝기만). 땅에서 분홍 알갱이가 모여 오름
		private void ComposeTelegraph(Stage stage, Vector3 center)
		{
			UltimateFxRun run = stage.Run;
			Light spot = WorldLight(stage, "CrownSpot", LightType.Spot, CoreLightColor, spotHeight + 6f);
			spot.spotAngle = spotAngle;
			spot.innerSpotAngle = spotAngle * 0.6f;
			spot.transform.position = center + Vector3.up * spotHeight;
			spot.transform.rotation = Quaternion.LookRotation(Vector3.down);
			run.Track(0f, CrownTime, progress =>
			{
				float now = run.Now;
				float ramp = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, CrownTime - telegraphSeconds, CrownTime));
				spot.intensity = spotIntensity * Mathf.Lerp(0.2f * Mathf.Clamp01(now / 0.15f), 1f, ramp);
			}, () => spot.enabled = false);

			ParticleSystem gather = Motes(run, "CrownGather", gatherCount + 8, -0.3f, 0.6f);
			for (int i = 0; i < gatherCount; i++)
			{
				float time = Mathf.Lerp(0f, CrownTime - 0.1f, i / (float)Mathf.Max(1, gatherCount - 1));
				run.At(time, () =>
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(1f, outerRadius);
					UltimateFxStage.Emit(gather, center + offset + Vector3.up * 0.1f, -offset * 0.6f + Vector3.up * Random.Range(1.5f, 3f), Random.Range(0.08f, 0.16f), Random.Range(0.6f, 0.9f), SparkleColor);
				});
			}
		}

		// 창 비: 하늘에서 가운데 쪽으로 기울어 꽂힘. 낙하 빛 줄과 꽂힐 때 먼지와 구멍
		private void ComposeRain(Stage stage, Vector3 center, List<(Vector3 position, int ring)> spots)
		{
			UltimateFxRun run = stage.Run;
			Mesh spearMesh = Keep(run, UltimateMeshFactory.Spear(spearBlade, spearShaft, spearGuard));
			Material spearMaterial = Glow(run, Color.white, spearGlow, spearGlow * 0.35f, true);
			spearMaterial.SetFloat(SPECULAR_ID, 0.7f);
			spearMaterial.SetColor(DISSOLVE_EDGE_ID, SparkleColor);
			ParticleSystem trails = Streaks(run, "SpearTrails", spots.Count * 4 + 8, 0.05f);
			ParticleSystem hits = Motes(run, "SpearHits", spots.Count * 6 + 8, 0.6f, 0f);
			Texture2D hole = CrackTexture(run, 100, 0.45f);
			List<Transform> spears = new();
			// 바깥 고리부터 떨어져 안쪽으로 조여 듦 (왕관이 안쪽부터 솟는 것과 반대 방향으로 긴장)
			float[] shuffle = new float[spots.Count];
			List<int> order = new();
			for (int i = 0; i < spots.Count; i++)
			{
				order.Add(i);
				shuffle[i] = Random.value;
			}
			order.Sort((left, right) => spots[right].ring != spots[left].ring ? spots[right].ring.CompareTo(spots[left].ring) : shuffle[left].CompareTo(shuffle[right]));
			for (int rank = 0; rank < order.Count; rank++)
			{
				Vector3 landing = spots[order[rank]].position;
				landing.y = UltimateFxStage.GroundHeight(landing);
				Vector3 inward = center - landing;
				inward.y = 0f;
				Vector3 leanAxis = inward.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, inward.normalized) : Vector3.right;
				// 칼끝이 원점, 자루가 위. 꼭대기가 바깥으로 누워 가운데 쪽으로 꽂힘
				Quaternion rotation = Quaternion.AngleAxis(-spearLean, leanAxis) * Quaternion.Euler(0f, Random.value * 360f, 0f);
				Vector3 axis = rotation * Vector3.up;
				Vector3 sky = landing + axis * spearHeight;
				Vector3 planted = landing - axis * spearLength * spearSink;
				GameObject spear = UltimateFxStage.CreateMeshObject("Spear", run.Root, spearMesh, spearMaterial);
				Solid(spear, false);
				spear.transform.rotation = rotation;
				spear.transform.localScale = new Vector3(spearLength * spearWidth, spearLength, spearLength * spearWidth);
				spear.transform.position = sky;
				spear.SetActive(false);
				spears.Add(spear.transform);
				float start = rainStart + rank / (float)Mathf.Max(1, order.Count - 1) * rainSeconds;
				run.At(start, () =>
				{
					spear.SetActive(true);
					for (int streak = 0; streak < 3; streak++)
						UltimateFxStage.Emit(trails, Vector3.Lerp(sky, landing, Random.Range(0.2f, 0.8f)) + Random.insideUnitSphere * 0.15f, -axis * Random.Range(60f, 110f), Random.Range(0.1f, 0.18f), Random.Range(0.12f, 0.22f), SparkleColor);
				});
				run.Track(start, spearFallSeconds, progress => spear.transform.position = Vector3.Lerp(sky, planted, UltimateFxRun.EaseIn(progress)));
				run.At(start + spearFallSeconds, () =>
				{
					Puff(run, landing, 5, 0.7f);
					for (int spark = 0; spark < 5; spark++)
						UltimateFxStage.Emit(hits, landing + Vector3.up * 0.15f, (Random.onUnitSphere + Vector3.up * 1.4f) * Random.Range(1.5f, 3.5f), Random.Range(0.07f, 0.14f), Random.Range(0.25f, 0.5f), SparkleColor);
				});
				Crack(run, hole, landing, 1.1f, start + spearFallSeconds, BreakTime + crackLinger, 0.6f);
			}
			// 꽂힌 창의 칼날이 왕관 직전에 달아올라 결정이 솟을 자리를 예고 (창이 씨앗이라는 인과)
			run.Track(CrownTime - spearChargeSeconds, spearChargeSeconds, progress => spearMaterial.SetColor(EMISSION_ID, spearGlow * (1f + spearChargeGain * UltimateFxRun.EaseIn(progress))));
			// 왕관이 솟을 때 창이 결정에 삼켜짐
			run.Track(CrownTime, 0.14f, progress =>
			{
				spearMaterial.SetFloat(DISSOLVE_DIRECTION_ID, 1f);
				spearMaterial.SetFloat(DISSOLVE_ID, progress);
			}, () =>
			{
				foreach (Transform spear in spears)
					run.Destroy(spear.gameObject);
			});
		}

		// 왕관 정점: 심 점광, 흔들림, 결정 가루, 먼지, 빛 줄, 뿌리 바위, 균열, 풀 바람, 적 들어올림
		private void ComposeCrown(Stage stage, Vector3 center, Material material, List<CrystalPiece> pieces)
		{
			UltimateFxRun run = stage.Run;
			Light core = WorldLight(stage, "CrownCore", LightType.Point, CoreLightColor, coreRange);
			core.transform.position = center + Vector3.up * coreHeight + stage.ToCamera * coreFront;
			run.Track(CrownTime, DurationSeconds - CrownTime, _ =>
			{
				float shimmer = 1f + 0.08f * Mathf.Sin(run.Now * 31f);
				core.intensity = coreIntensity * Spike(run.Now, CrownTime, coreRise, coreHalfLife) * shimmer * (1f - UltimateFxRun.Span(run.Now, BreakTime, BreakTime + breakSeconds));
			}, () => core.enabled = false);
			GroundGlow(run, center, outerRadius * 3f, CoreLightColor * 0.9f, CrownTime, coreRise, coreHalfLife * 1.2f, DurationSeconds - CrownTime);

			// 결정 면 발광이 왕관 정점에 한 번 치솟았다 가라앉음
			Color baseEmission = material.GetColor(EMISSION_ID);
			run.Track(CrownTime, BreakTime - CrownTime, _ => material.SetColor(EMISSION_ID, baseEmission * (1f + emissionPeak * Spike(run.Now, CrownTime, 0.06f, 0.25f))));

			run.At(CrownTime, () =>
			{
				run.ShakeSeries(run.Now, shakeCount, shakeInterval, shakeStrength, shakeDecay);
				run.React(center, windSpeed, windReach, false);
				Sparkle(run, center, sparkleCount, outerRadius, 1.6f);
				DustRing(run, center, dustCount, outerRadius * 0.8f, DustColor, 1.6f);
				run.SparkBurst(center + Vector3.up * 0.4f, rayCount, new Vector2(9f, 20f), new Vector2(0.2f, 0.5f), new Vector2(0.1f, 0.22f), SparkleColor, Color.white, 1f);
			});
			RootRocks(run, center, rootRockCount, new Vector2(innerRadius, outerRadius * 1.1f), new Vector2(0.5f, 0.95f), CrownTime + 0.02f, 0.12f, BreakTime + 0.3f, 0.6f);
			Crack(run, CrackTexture(run, 0, 0.55f), center, crackSize, CrownTime, BreakTime + crackLinger, 0.8f);
			LiftSprites(run, center, outerRadius * 1.1f, liftHeight, CrownTime + 0.02f, liftSeconds);
		}

		private void ComposeGrade(Stage stage)
		{
			float crown = CrownTime;
			float restore = BreakTime;
			stage.BloomTint = CoreLightColor;
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Plateau(now, 0f, 0.4f, restore, restoreSeconds));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Plateau(now, 0f, 0.4f, restore, restoreSeconds));
			stage.Vignette = now => vignette * Plateau(now, 0f, 0.4f, restore, restoreSeconds);
			// 흑백 임팩트 1장 (정점 프레임), 그 다음 장 흰 실루엣 1장 (m02 #11, #17)
			stage.Saturation = now => -100f * Window(now, crown, FRAME);
			stage.Contrast = now => impactContrast * Window(now, crown, FRAME);
			stage.Exposure = now => impactExposure * Window(now, crown, FRAME) + silhouetteExposure * Window(now, crown + FRAME * 2f, FRAME);
			stage.Chromatic = now => 0.15f * Spike(now, crown, 0.02f, 0.12f);
			stage.Bloom = now =>
			{
				float build = 0.5f * UltimateFxRun.Span(now, 0f, crown);
				float peak = Mathf.Lerp(bloomHold, bloomCrown, Spike(now, crown, 0.04f, 0.35f)) * Mathf.Clamp01((now - crown) * 1000f);
				return Mathf.Max(build, peak) * (1f - UltimateFxRun.Span(now, restore, restore + restoreSeconds));
			};
		}
	}
}
