using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 블랙홀 강한 판 (강력 스킬용). 혜성 셋이 땅에 마법진을 그리면, 원반을 두른 검은 핵이 하늘에서 내려와 바위와 흙을 나선으로 빨아들이고, 원반이 핵 속으로 무너진 뒤 폭발해 바위를 다시 토해 냄
	/// - 흐름: 마법진 그리기 (혜성과 타들어 가는 마법진) -> 낙하 (커지는 어두운 원) -> 흡입 (바위 메시가 들려 나선으로) -> 붕괴 (원반이 핵으로) -> 폭발 (바위 재방출, 청록 불덩이) -> 잔류
	/// - 핵은 빛 안 받는 검정 구 (화면에서 구멍처럼 가림), 원반은 카메라 쪽으로 기운 고리판 (회전하는 잡음 무늬), 광자 고리는 카메라를 향한 얇은 원환
	/// - 연출 광원은 마법진 점광과 원반 점광 둘. 흔들림, 정지, 섬광은 화면 연출 박자표 (FeelUltimateFx) 몫
	/// - 설계 memo wm/design/ult3d-all-20261120/09_black-hole.md, 제작 기록 ult3d-batch2-20261121
	/// - 재생: 개발 창 fx ultimate BlackHoleHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_BlackHoleHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(BlackHoleHeavyUltimateFx))]
	public sealed class BlackHoleHeavyUltimateFx : WorldUltimateFx
	{
		private sealed class Captive
		{
			public Transform Transform;
			public float Angle;
			public float Radius;
			public float Height;
			public float Size;
			public float Start;
			public Vector3 Spin;
		}

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color circleColor = new(0.4f, 1.6f, 1.8f);
		[SerializeField, ColorUsage(false, true)] private Color discColor = new(0.45f, 1.9f, 2.6f);
		[SerializeField, ColorUsage(false, true)] private Color photonColor = new(2.6f, 3f, 3f);
		[SerializeField] private Color lightColor = new(0.55f, 0.95f, 1f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color blastColor = new(0.7f, 2.2f, 2.4f);
		[SerializeField] private Color rockColor = new(0.24f, 0.22f, 0.21f, 1f);
		[SerializeField] private Color clodColor = new(0.16f, 0.13f, 0.11f, 0.9f);

		[Header("크기")]
		[SerializeField, Min(0.3f)] private float coreDiameter = 2.2f;
		[SerializeField, Min(0.5f), Tooltip("원반 바깥 반지름")] private float discRadius = 2.8f;
		[SerializeField, Range(0.05f, 0.9f), Tooltip("원반 안 반지름 / 바깥")] private float discInner = 0.36f;
		[SerializeField, Range(0f, 80f), Tooltip("원반을 카메라 쪽으로 기울이는 각도 (15도면 바닥 고리처럼 납작, 크면 앞 가장자리가 땅에 묻힘)")] private float discTilt = 32f;
		[SerializeField, Min(0.5f), Tooltip("멈춘 높이 (표적 위). 원반 앞 가장자리 (반지름 x sin 기울기) 보다 높게")] private float hoverHeight = 1.9f;
		[SerializeField, Min(1f), Tooltip("원반 둘레 아지랑이 반지름 / 원반 반지름")] private float hazeRatio = 1.1f;
		[SerializeField, Range(0f, 0.5f), Tooltip("아지랑이 밝기. 높으면 원반이 희뿌연 안개 고리로 읽힘 (1회차 촬영)")] private float hazeBrightness = 0.05f;
		[SerializeField, Min(2f)] private float fallHeight = 14f;
		[SerializeField, Min(0.5f)] private float circleRadius = 4.5f;

		[Header("시간 (초)")]
		[SerializeField, Min(0.2f), Tooltip("마법진 그리기 (글 2 1.3s)")] private float drawSeconds = 1.3f;
		[SerializeField, Min(0.1f), Tooltip("낙하 (글 2 0.6s)")] private float fallSeconds = 0.6f;
		[SerializeField, Min(0.3f)] private float suckSeconds = 1.4f;
		[SerializeField, Min(0.05f), Tooltip("원반이 핵으로 무너지는 시간 (폭발 직전)")] private float collapseSeconds = 0.16f;
		[SerializeField, Min(0.1f)] private float debrisLinger = 1.4f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.2f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.35f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.45f;
		[SerializeField, Min(0f)] private float circleIntensity = 14f;
		[SerializeField, Min(0f)] private float discIntensity = 18f;
		[SerializeField, Min(0f)] private float blastIntensity = 40f;
		[SerializeField, Min(1f)] private float lightRange = 12f;

		[Header("후처리")]
		[SerializeField, Min(0f)] private float bloom = 1.5f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.3f;
		[SerializeField, Range(-100f, 0f)] private float suckSaturation = 0f;

		[Header("반응")]
		[SerializeField, Min(0)] private int rockCount = 14;
		[SerializeField, Min(10f), Tooltip("혜성 하나가 초당 남기는 꼬리 점")] private float cometTrailPerSecond = 240f;
		[SerializeField] private Vector2 rockSize = new(0.5f, 1.1f);
		[SerializeField, Min(0)] private int clodsPerSecond = 32;
		[SerializeField, Min(0)] private int blastPuffs = 40;
		[SerializeField, Min(0)] private int ejectCount = 30;
		[SerializeField, Min(1f)] private float ejectSpeed = 11f;
		[SerializeField, Min(1f)] private float windSpeed = 12f;

		// ScaleWorld 배율. 실행용 복제본에서만 1 이 아님 (리터럴 크기 값에 곱함)
		[System.NonSerialized] private float sizeScale = 1f;

		private float FallStart => drawSeconds;
		private float ArriveTime => drawSeconds + fallSeconds;
		private float BlastTime => ArriveTime + suckSeconds + collapseSeconds;
		private float RestoreTime => BlastTime + 0.5f;

		public override float DurationSeconds => BlastTime + debrisLinger;

		public override void ScaleWorld(float scale)
		{
			coreDiameter *= scale;
			discRadius *= scale;
			sizeScale = scale;
			hoverHeight *= Mathf.Pow(scale, 0.8f);
			circleRadius *= scale;
			lightRange *= scale;
			rockSize *= scale;
			ejectSpeed *= Mathf.Sqrt(scale);
			rockCount = Mathf.RoundToInt(rockCount * scale);
			clodsPerSecond = Mathf.RoundToInt(clodsPerSecond * scale);
			blastPuffs = Mathf.RoundToInt(blastPuffs * scale);
			ejectCount = Mathf.RoundToInt(ejectCount * scale);
		}

		protected override void ComposeWorld(Stage stage)
		{
			UltimateFxRun run = stage.Run;
			Vector3 center = stage.Target;
			Vector3 hover = center + Vector3.up * hoverHeight;
			Light circleLight = WorldLight(stage, "BlackHoleCircleLight", LightType.Point, lightColor, lightRange);
			Light discLight = WorldLight(stage, "BlackHoleDiscLight", LightType.Point, lightColor, lightRange);
			ComposeCircle(stage, center, circleLight);
			ComposeHole(stage, center, hover, discLight);
			ComposeSuction(stage, center, hover);
			ComposeBlast(stage, center, hover);
			ComposeGrade(stage);
		}

		// 마법진 그리기: 혜성 셋이 원을 따라 돌며 빛 꼬리를 남기고, 그 뒤로 마법진이 타들어 가듯 밝아짐. 마법진 점광
		private void ComposeCircle(Stage stage, Vector3 center, Light light)
		{
			UltimateFxRun run = stage.Run;
			Material outerMaterial = run.Additive(UltimateFxTextures.Rune, Color.clear);
			Material innerMaterial = run.Additive(UltimateFxTextures.Rhombus, Color.clear);
			GameObject outer = run.Plate("BlackHoleRune", outerMaterial, center + Vector3.up * 0.04f, circleRadius * 2f, 4);
			GameObject inner = run.Plate("BlackHoleRhombus", innerMaterial, center + Vector3.up * 0.05f, circleRadius * 1.3f, 5);
			run.Track(0f, DurationSeconds, _ =>
			{
				float now = run.Now;
				float reveal = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, 0.15f, drawSeconds));
				float flare = 1f + 1.5f * Spike(now, drawSeconds, 0.05f, 0.25f) + 2f * Spike(now, BlastTime, 0.02f, 0.3f);
				float cool = 1f - UltimateFxRun.Span(now, BlastTime + 0.2f, DurationSeconds);
				float dim = Mathf.Lerp(1f, 0.55f, UltimateFxRun.Span(now, ArriveTime, ArriveTime + 0.3f));
				outer.transform.rotation = Quaternion.Euler(0f, now * 12f, 0f);
				inner.transform.rotation = Quaternion.Euler(0f, -now * 20f, 0f);
				outerMaterial.SetColor("_Color", circleColor * reveal * flare * cool * dim);
				innerMaterial.SetColor("_Color", circleColor * 0.8f * UltimateFxRun.Span(now, drawSeconds * 0.6f, drawSeconds) * flare * cool * dim);
			});

			ParticleSystem trail = Motes(run, "BlackHoleCometTrail", Mathf.CeilToInt(cometTrailPerSecond * 3f * 0.8f) + 40, 0f, 0f);
			ParticleSystem heads = Motes(run, "BlackHoleCometHead", Mathf.CeilToInt(cometTrailPerSecond * 0.5f) + 20, 0f, 0f);
			float offset = Random.value * Mathf.PI * 2f;
			float carry = 0f;
			// 혜성마다 초당 개수로 내보내 프레임 수와 무관한 꼬리
			run.Track(0f, drawSeconds, progress =>
			{
				carry += cometTrailPerSecond * run.Delta;
				int count = Mathf.FloorToInt(carry);
				carry -= count;
				for (int comet = 0; comet < 3; comet++)
				{
					// 셋이 같은 원을 서로 120도 떨어져 한 바퀴 돎. 셋째는 안쪽 마름모 반지름
					float radius = comet == 2 ? circleRadius * 0.62f : circleRadius * 0.94f;
					float angle = offset + comet * Mathf.PI * 2f / 3f + UltimateFxRun.EaseInOut(progress) * Mathf.PI * 2f * (comet == 2 ? -1f : 1f);
					Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.up * 0.3f;
					for (int dot = 0; dot < count; dot++)
						UltimateFxStage.Emit(trail, position + Random.insideUnitSphere * 0.06f, Vector3.up * 0.2f, Random.Range(0.1f, 0.2f) * sizeScale, Random.Range(0.4f, 0.7f), new Color(0.6f, 1f, 1f, 1f));
					if (count > 0)
						UltimateFxStage.Emit(heads, position, Vector3.zero, 0.55f * sizeScale, 0.12f, Color.white);
				}
			});

			light.transform.position = center + Vector3.up * 1f;
			run.Track(0f, BlastTime + 0.2f, _ =>
			{
				float now = run.Now;
				float level = Mathf.Lerp(0.2f, 1f, UltimateFxRun.Span(now, 0f, drawSeconds)) * Mathf.Lerp(1f, 0.4f, UltimateFxRun.Span(now, ArriveTime, ArriveTime + 0.3f));
				light.intensity = circleIntensity * level * (1f - UltimateFxRun.Span(now, BlastTime, BlastTime + 0.2f));
			}, () => light.enabled = false);
		}

		// 핵, 원반, 광자 고리: 하늘에서 감속 하강해 멈추고, 흡입 동안 원반이 빨라지며, 폭발 직전 원반이 핵 속으로 무너짐. 아래 땅의 어두운 원이 커짐
		private void ComposeHole(Stage stage, Vector3 center, Vector3 hover, Light light)
		{
			UltimateFxRun run = stage.Run;
			GameObject holeRoot = new("BlackHole");
			holeRoot.transform.SetParent(run.Root, false);
			holeRoot.SetActive(false);
			Vector3 sky = hover + Vector3.up * (fallHeight - hoverHeight) - stage.ToCamera * 2f;

			Material coreMaterial = Glow(run, Color.black, Color.black, discColor * 0.15f, false);
			coreMaterial.SetFloat(SPECULAR_ID, 0f);
			coreMaterial.SetFloat(RIM_POWER_ID, 5f);
			GameObject core = UltimateFxStage.CreateMeshObject("BlackHoleCore", holeRoot.transform, Keep(run, UltimateMeshFactory.Sphere(24, 36)), coreMaterial);
			Solid(core, true);

			Vector3 discNormal = (Vector3.up * Mathf.Cos(discTilt * Mathf.Deg2Rad) + stage.ToCamera * Mathf.Sin(discTilt * Mathf.Deg2Rad)).normalized;
			Quaternion discRotation = Quaternion.FromToRotation(Vector3.up, discNormal);
			// 반복 잡음 무늬 (둘레 방향 줄무늬). 가장자리가 투명한 Smoke, Soft 는 둘레 한쪽에만 보임 (독립 검토)
			Material discMaterial = run.Additive(UltimateFxTextures.TiledStreaks, Color.clear);
			GameObject disc = UltimateFxStage.CreateMeshObject("BlackHoleDisc", holeRoot.transform, Keep(run, UltimateMeshFactory.Annulus(96, 8, discInner, 0.9f, 1f)), discMaterial);
			Material hazeMaterial = run.Additive(UltimateFxTextures.TiledStreaks, Color.clear);
			hazeMaterial.mainTextureOffset = new Vector2(0.37f, 0.5f);
			GameObject haze = UltimateFxStage.CreateMeshObject("BlackHoleHaze", holeRoot.transform, Keep(run, UltimateMeshFactory.Annulus(64, 4, discInner * 0.9f, 0.6f, 1f)), hazeMaterial);

			Material photonMaterial = run.Additive(UltimateFxTextures.White, Color.clear);
			GameObject photon = UltimateFxStage.CreateMeshObject("BlackHolePhoton", holeRoot.transform, Keep(run, UltimateMeshFactory.Torus(72, 6, 0.035f)), photonMaterial);

			// 어두운 원 (곱하기 대신 알파 검정): 내려오는 동안 커짐
			Material shadowMaterial = run.Alpha(UltimateFxTextures.Soft, new Color(0f, 0f, 0f, 0f));
			GameObject shadow = run.Plate("BlackHoleShadow", shadowMaterial, center + Vector3.up * 0.07f, 1f, 7);

			float spinAngle = 0f;
			run.At(FallStart, () => holeRoot.SetActive(true));
			// 착지 바람 (한 번). 흡입 동안 풀 안쪽 바람은 없음: React 는 바깥으로만 휘고, 연달아 부르면 풀이 멈춤 (독립 검토)
			run.At(ArriveTime, () => run.React(center, windSpeed, circleRadius * 1.5f, false));
			run.Track(FallStart, BlastTime - FallStart, _ =>
			{
				float now = run.Now;
				float fall = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, FallStart, ArriveTime));
				float suck = UltimateFxRun.Span(now, ArriveTime, ArriveTime + suckSeconds);
				float collapse = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, BlastTime - collapseSeconds, BlastTime));
				Vector3 position = Vector3.LerpUnclamped(sky, hover, fall) + Vector3.up * 0.08f * Mathf.Sin(now * 5f) * suck;
				holeRoot.transform.position = position;
				float coreSize = coreDiameter * Mathf.Lerp(0.75f, 1f, fall) * (1f + 0.12f * suck) * Mathf.Lerp(1f, 0.55f, collapse);
				core.transform.localScale = Vector3.one * coreSize;
				spinAngle += run.Delta * Mathf.Lerp(80f, 420f, suck * suck + collapse);
				float discScale = discRadius * Mathf.Lerp(0.8f, 1f, fall) * (1f - collapse * 0.92f);
				disc.transform.localRotation = discRotation * Quaternion.Euler(0f, -spinAngle, 0f);
				disc.transform.localScale = Vector3.one * discScale;
				haze.transform.localRotation = discRotation * Quaternion.Euler(0f, -spinAngle * 0.6f, 0f);
				haze.transform.localScale = Vector3.one * discScale * hazeRatio;
				float glow = Mathf.Lerp(0.5f, 1f, fall) * (1f + 0.8f * suck + 2.5f * collapse);
				discMaterial.SetColor("_Color", discColor * 0.75f * glow);
				hazeMaterial.SetColor("_Color", discColor * hazeBrightness * glow);
				Vector3 toCamera = run.MainCamera != null ? (run.MainCamera.transform.position - position).normalized : stage.ToCamera;
				photon.transform.rotation = Quaternion.FromToRotation(Vector3.up, toCamera);
				photon.transform.localScale = Vector3.one * coreSize * 0.56f;
				photonMaterial.SetColor("_Color", photonColor * Mathf.Lerp(0.3f, 1f, fall) * (1f + collapse * 2f));
				float shadowSize = Mathf.Lerp(1f, 5f, fall) * (1f + 0.3f * suck) * (coreDiameter / 2.2f);
				shadow.transform.localScale = new Vector3(shadowSize, 1f, shadowSize);
				shadowMaterial.SetColor("_Color", new Color(0f, 0f, 0f, 0.75f * fall));
			}, () =>
			{
				holeRoot.SetActive(false);
				run.Destroy(shadow);
			});

			run.Track(FallStart, BlastTime + 1.2f - FallStart, _ =>
			{
				float now = run.Now;
				float fall = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, FallStart, ArriveTime));
				light.transform.position = holeRoot.transform.position + stage.ToCamera * 1.2f;
				float suck = UltimateFxRun.Span(now, ArriveTime, BlastTime);
				float hold = now < BlastTime ? discIntensity * fall * (1f + 0.5f * suck) : 0f;
				light.intensity = Mathf.Max(hold, blastIntensity * Spike(now, BlastTime, 0.02f, 0.6f));
			}, () => light.enabled = false);
		}

		// 흡입: 땅의 바위가 들려 나선으로 핵에 빨려들고, 흙 덩이가 소용돌이치며 오름. 풀은 안쪽으로
		private void ComposeSuction(Stage stage, Vector3 center, Vector3 hover)
		{
			UltimateFxRun run = stage.Run;
			Material rockMaterial = Glow(run, Color.white, Color.black, discColor * 0.05f, true);
			rockMaterial.SetFloat(SPECULAR_ID, 0.1f);
			Mesh[] meshes = { Keep(run, UltimateMeshFactory.Rock(901, rockColor, 0.5f)), Keep(run, UltimateMeshFactory.Rock(907, rockColor * 0.85f, 0.6f)) };
			List<Captive> captives = new();
			for (int i = 0; i < rockCount; i++)
			{
				float angle = (i + Random.Range(-0.3f, 0.3f)) / Mathf.Max(1, rockCount) * Mathf.PI * 2f;
				float radius = Random.Range(circleRadius * 0.45f, circleRadius * 1.15f);
				Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
				position.y = UltimateFxStage.GroundHeight(position);
				GameObject rock = UltimateFxStage.CreateMeshObject("BlackHoleRock", run.Root, meshes[i % meshes.Length], rockMaterial);
				Solid(rock, true);
				float size = Random.Range(rockSize.x, rockSize.y);
				rock.transform.position = position + Vector3.down * size;
				rock.transform.localScale = Vector3.one * size;
				rock.transform.rotation = Random.rotation;
				captives.Add(new Captive
				{
					Transform = rock.transform,
					Angle = angle,
					Radius = radius,
					Height = position.y,
					Size = size,
					Start = ArriveTime + Random.Range(0f, suckSeconds * 0.45f),
					Spin = Random.onUnitSphere * Random.Range(90f, 240f),
				});
			}
			run.Track(ArriveTime, suckSeconds + collapseSeconds, _ =>
			{
				float now = run.Now;
				foreach (Captive captive in captives)
				{
					if (captive.Transform == null)
						continue;
					float age = UltimateFxRun.Span(now, captive.Start, BlastTime - collapseSeconds * 0.5f);
					float pull = UltimateFxRun.EaseIn(age);
					float radius = Mathf.Lerp(captive.Radius, 0.1f, pull);
					// 가까울수록 빨리 도는 나선
					float angle = captive.Angle + age * Mathf.PI * 2f * Mathf.Lerp(0.6f, 2.2f, pull);
					float lift = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, captive.Start, captive.Start + 0.25f));
					Vector3 ground = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
					ground.y = captive.Height + captive.Size * 0.5f * lift - captive.Size * (1f - lift);
					captive.Transform.position = Vector3.Lerp(ground, hover, pull * pull);
					captive.Transform.Rotate(captive.Spin * run.Delta, Space.World);
					captive.Transform.localScale = Vector3.one * captive.Size * Mathf.Lerp(1f, 0.1f, pull * pull);
				}
			}, () =>
			{
				foreach (Captive captive in captives)
				{
					if (captive.Transform != null)
						run.Destroy(captive.Transform.gameObject);
				}
			});
			foreach (Captive captive in captives)
			{
				Vector3 spot = center + new Vector3(Mathf.Cos(captive.Angle), 0f, Mathf.Sin(captive.Angle)) * captive.Radius;
				run.At(captive.Start, () => run.Dust(spot, 0.6f, 5, clodColor));
			}

			// 빛 안 받는 어두운 흙 덩이. 빛 받는 연기로 하면 원반 광원에 하얗게 뭉쳐 원반을 덮는 안개가 됨 (2회차 촬영)
			ParticleSystem clods = run.Particles("BlackHoleClods", run.Alpha(UltimateFxTextures.Smoke, Color.white), clodsPerSecond * 2 + 40, 0f);
			UltimateFxStage.SetLifetimeLook(clods, UltimateFxStage.FadeGradient(Color.white, 0.1f, 0.7f), AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
			ParticleSystem streaks = Streaks(run, "BlackHoleInflow", clodsPerSecond + 40, 0.08f);
			float carry = 0f;
			run.Track(ArriveTime, suckSeconds, _ =>
			{
				carry += clodsPerSecond * run.Delta;
				for (; carry >= 1f; carry -= 1f)
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(circleRadius * 0.5f, circleRadius * 1.2f);
					Vector3 from = center + offset + Vector3.up * 0.2f;
					Vector3 tangent = Vector3.Cross(Vector3.up, offset).normalized;
					float life = Random.Range(0.55f, 0.85f);
					Vector3 velocity = (hover - from) / life + tangent * Random.Range(3f, 5f) * sizeScale;
					UltimateFxStage.Emit(clods, from, velocity, Random.Range(0.15f, 0.3f) * sizeScale, life * 0.85f, clodColor, Random.value * 360f, Random.Range(-200f, 200f));
					if (Random.value < 0.5f)
						UltimateFxStage.Emit(streaks, from + Vector3.up * Random.Range(0.3f, 1.5f), velocity * 1.2f, Random.Range(0.06f, 0.12f), life * 0.8f, new Color(0.6f, 1f, 1f, 1f));
				}
			});
		}

		// 폭발: 핵이 사라지며 청록 불덩이, 빨아들인 바위가 사방으로 다시 쏟아져 땅에 남음, 구덩이와 먼지, 풀 바람
		private void ComposeBlast(Stage stage, Vector3 center, Vector3 hover)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem fire = run.Particles("BlackHoleBlast", run.Additive(UltimateFxTextures.FlamePuff, blastColor), blastPuffs + 10, -0.1f);
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.5f, 1f, 1f), 0.35f), new GradientColorKey(new Color(0.1f, 0.3f, 0.45f), 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.7f, 0.4f), new GradientAlphaKey(0f, 1f) });
			UltimateFxStage.SetLifetimeLook(fire, gradient, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.6f));
			// 입자 메시는 꼭짓점 색 자리에 입자 색이 들어가 보석빛으로 보임 (2회차 촬영). 바탕색으로 칠함
			Material ejectMaterial = Glow(run, rockColor, Color.black, Color.black, false);
			ejectMaterial.SetFloat(SPECULAR_ID, 0.1f);
			ParticleSystem eject = MeshParticles(run, "BlackHoleEject", Keep(run, UltimateMeshFactory.Rock(913, rockColor, 0.55f)), ejectMaterial, ejectCount + 8, 1.4f, true, 0.25f, 0.6f, true);
			run.At(BlastTime, () =>
			{
				for (int i = 0; i < blastPuffs; i++)
				{
					Vector3 velocity = Random.onUnitSphere * Random.Range(3f, 7f) * (discRadius / 3.6f);
					UltimateFxStage.Emit(fire, hover + Random.insideUnitSphere * 0.4f, velocity, Random.Range(1f, 1.8f) * (discRadius / 3.6f), Random.Range(0.45f, 0.8f), Color.white, Random.value * 360f, Random.Range(-150f, 150f));
				}
				for (int i = 0; i < ejectCount; i++)
				{
					Vector3 velocity = (Random.onUnitSphere + Vector3.up * 0.4f).normalized * ejectSpeed * Random.Range(0.5f, 1f);
					EmitMesh(eject, hover + Random.insideUnitSphere * 0.3f, velocity, Random.Range(rockSize.x, rockSize.y) * 0.7f, debrisLinger + Random.Range(0f, 0.4f), Color.white, Random.rotation.eulerAngles, Random.onUnitSphere * 300f);
				}
				run.SparkBurst(hover, 110, new Vector2(8f, 20f), new Vector2(0.3f, 0.7f), new Vector2(0.08f, 0.18f), new Color(0.6f, 1f, 1f, 1f), Color.white, 0.6f);
				DustRing(run, center, 44, circleRadius * 0.9f, new Color(0.4f, 0.38f, 0.4f, 0.8f), 1.8f);
				run.React(center, windSpeed, circleRadius * 2.5f, true);
			});
			Texture2D crater = ScorchTexture(run, 77, new Color(0.08f, 0.07f, 0.06f, 0.85f), new Color(0.4f, 1f, 1f, 1f), 0.7f);
			GroundDecal(run, crater, center, circleRadius * 1.6f, BlastTime, 0.05f, DurationSeconds - 0.8f, 0.8f, 6);
			GroundGlow(run, center, circleRadius * 2.6f, blastColor * 0.7f, BlastTime, 0.02f, 0.35f, 1.4f);
		}

		private void ComposeGrade(Stage stage)
		{
			float restore = RestoreTime;
			stage.BloomTint = new Color(0.6f, 1f, 1f);
			stage.SunScale = now => Mathf.Lerp(1f, sunDim, Plateau(now, 0.2f, drawSeconds, restore, restoreSeconds));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Plateau(now, 0.2f, drawSeconds, restore, restoreSeconds));
			stage.Vignette = now => vignette * Plateau(now, 0.2f, drawSeconds, restore, restoreSeconds);
			stage.Filter = now => Color.Lerp(Color.white, new Color(0.82f, 0.92f, 1f), Plateau(now, 0.2f, drawSeconds, restore, restoreSeconds));
			stage.Saturation = now => suckSaturation * Plateau(now, ArriveTime, suckSeconds * 0.5f, BlastTime, 0.2f);
			stage.Chromatic = now => 0.25f * Plateau(now, ArriveTime, suckSeconds, BlastTime, 0.3f);
			stage.Bloom = now => Mathf.Max(0.5f * UltimateFxRun.Span(now, 0f, drawSeconds), bloom * Plateau(now, ArriveTime, 0.2f, restore, restoreSeconds)) * (1f - UltimateFxRun.Span(now, restore, restore + restoreSeconds));
		}
	}
}
