using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 별자리 강한 판 (강력 스킬용). 하늘에서 별이 하나씩 떨어져 땅에 박히고 땅 위 빛줄로 이어져 별자리가 된 뒤, 별빛 기둥이 내리꽂히고 별들이 부서져 기둥을 따라 쏟아지다 마지막에 땅에서 네 갈래로 터짐
	/// - 흐름: 별 박힘 (하나씩 떨어져 꽂힘, 빛줄이 따라 이어짐) -> 숨 멎음 (가장 어두움) -> 별빛 기둥 낙하 -> 쏟아짐 (별이 차례로 부서져 기둥으로) -> 네 갈래 터짐 -> 잔류
	/// - 별은 땅에 비스듬히 박힌 발광 결정, 빛줄은 땅 위에 누운 관 메시. 모양은 pattern (기본 국자 꼴 일곱 별)
	/// - 사용자 판정 2026-11-22: 공중에 뜬 별과 불규칙한 꺾인 줄이 어색함 -> 땅에 박힌 별, 알아볼 수 있는 모양
	/// - 연출 광원은 별자리 점광 (새로 켜진 별을 따라감) 과 접지 점광 둘. 접지 순간 별자리 점광을 끔
	/// - 흔들림, 정지, 섬광은 화면 연출 박자표 (FeelUltimateFx) 몫. 여기는 월드 안 물체와 빛만
	/// - 설계 memo wm/design/ult3d-all-20261120/07_constellation.md, 제작 기록 ult3d-batch3-20261121
	/// - 재생: 개발 창 fx ultimate ConstellationHeavy
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_ConstellationHeavy", menuName = "WM/CombatFx/Ultimate/" + nameof(ConstellationHeavyUltimateFx))]
	public sealed class ConstellationHeavyUltimateFx : WorldUltimateFx
	{
		private sealed class Star
		{
			public Vector3 Position;
			public Vector3 Ground;
			public float LightTime;
			public float BreakTime;
			public GameObject Body;
			public Material BodyMaterial;
			public Material GlintMaterial;
			public Material DotMaterial;
			public Quaternion Lean;
		}

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color starColor = new(1.5f, 1.9f, 3f);
		[SerializeField, ColorUsage(false, true)] private Color lineColor = new(0.45f, 0.75f, 1.7f);
		[SerializeField, ColorUsage(false, true)] private Color pillarCore = new(2.6f, 2.8f, 3.4f);
		[SerializeField, ColorUsage(false, true)] private Color pillarOuter = new(0.5f, 0.9f, 2.4f);
		[SerializeField] private Color lightColor = new(0.75f, 0.85f, 1f, 1f);
		[SerializeField] private Color tintColor = new(0.72f, 0.8f, 1f);
		[SerializeField] private Color dustColor = new(0.4f, 0.42f, 0.5f, 0.8f);

		[Header("별자리")]
		[SerializeField, Tooltip("별 자리 (화면 오른쪽, 화면 위쪽). -1~1 을 patternRadius 배. 적힌 순서대로 박히고 이어짐. 기본은 국자 꼴 일곱 별")] private Vector2[] pattern =
		{
			new(-1f, 0.42f), new(-0.6f, 0.56f), new(-0.24f, 0.4f), new(0.08f, 0.2f), new(0.16f, -0.36f), new(0.78f, -0.42f), new(0.86f, 0.18f),
		};
		[SerializeField, Tooltip("마지막 별에서 이 번호의 별로 줄을 하나 더 이음 (국자 닫기). -1 이면 없음")] private int closeTo = 3;
		[SerializeField, Min(1f), Tooltip("별자리 반지름 (표적 중심)")] private float patternRadius = 4.2f;
		[SerializeField, Min(0.1f), Tooltip("박힌 별 결정 높이")] private float starSize = 1.5f;
		[SerializeField, Range(0f, 50f), Tooltip("결정이 기우는 최대 각도")] private float starLean = 22f;
		[SerializeField, Min(0.5f), Tooltip("별 반짝임 판 크기 / 별 크기")] private float glintRatio = 1.6f;
		[SerializeField, Min(0.005f)] private float lineRadius = 0.05f;
		[SerializeField, Min(0.1f), Tooltip("별 아래 땅의 빛 점 지름")] private float groundDotSize = 2.2f;
		[SerializeField, Min(1f), Tooltip("별이 떨어지기 시작하는 높이")] private float fallHeight = 9f;
		[SerializeField, Min(0.02f), Tooltip("별 하나가 떨어지는 시간. 짧을수록 꽂히는 느낌")] private float fallSeconds = 0.09f;

		[Header("기둥")]
		[SerializeField, Min(2f)] private float pillarHeight = 9f;
		[SerializeField, Tooltip("기둥 반지름 (흰 심, 푸른 겉)")] private Vector2 pillarRadius = new(0.22f, 0.8f);
		[SerializeField, Tooltip("기둥 밝기 (흰 심, 푸른 겉)")] private Vector2 pillarBrightness = new(0.5f, 0.7f);

		[Header("시간 (초)")]
		[SerializeField, Min(0f)] private float firstStarTime = 0.3f;
		[SerializeField, Min(0.03f), Tooltip("별 하나가 박히는 평균 간격")] private float starInterval = 0.15f;
		[SerializeField, Range(0.3f, 1f), Tooltip("1 이면 같은 간격, 작을수록 뒤로 갈수록 빨리 박힘 (같은 간격은 강약 없이 읽힘, 2차 독립 검토)")] private float starPace = 0.6f;
		[SerializeField, Min(0.05f), Tooltip("다 이어진 뒤 기둥 전의 숨 멎음 (가장 어두움)")] private float hushSeconds = 0.22f;
		[SerializeField, Min(0.03f), Tooltip("기둥이 땅에 닿기까지")] private float pillarFallSeconds = 0.1f;
		[SerializeField, Min(0.3f), Tooltip("별이 부서져 기둥으로 쏟아지는 시간 (m07 1.4s)")] private float pourSeconds = 1.2f;
		[SerializeField, Min(0.1f)] private float lingerSeconds = 1.8f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.2f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f)] private float sunDim = 0.5f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.55f;
		[SerializeField, Range(0f, 1f), Tooltip("숨 멎음 동안 해 배율")] private float hushSunDim = 0.3f;
		[SerializeField, Min(0f)] private float starLightIntensity = 11f;
		[SerializeField, Min(1f)] private float starLightRange = 9f;
		[SerializeField, Min(0f)] private float strikeLightIntensity = 30f;
		[SerializeField, Min(0f)] private float pourLightIntensity = 13f;
		[SerializeField, Min(0f)] private float burstLightIntensity = 42f;
		[SerializeField, Min(1f)] private float strikeLightRange = 13f;

		[Header("후처리")]
		[SerializeField, Min(0f)] private float bloom = 1.5f;
		[SerializeField, Range(0f, 1f)] private float vignette = 0.3f;
		[SerializeField, Range(-100f, 0f)] private float nightSaturation = -15f;

		[Header("반응")]
		[SerializeField, Min(0), Tooltip("별 하나가 부서질 때 기둥으로 향하는 빛 알갱이")] private int shardsPerStar = 30;
		[SerializeField, Min(0), Tooltip("쏟아지는 동안 기둥을 따라 떨어지는 빛 알갱이 (초당)")] private int flowPerSecond = 240;
		[SerializeField, Min(0), Tooltip("쏟아지는 동안 접지점 불티 (초당)")] private int sparkPerSecond = 110;
		[SerializeField, Min(0), Tooltip("터질 때 땅에 떨어지는 별 조각 메시")] private int fragmentCount = 30;
		[SerializeField, Min(1f), Tooltip("네 갈래 터짐이 땅을 따라 뻗는 길이")] private float crossLength = 6f;
		[SerializeField, Min(0.5f)] private float scorchSize = 5f;
		[SerializeField, Min(1f)] private float windSpeed = 12f;
		[SerializeField, Min(1f)] private float windReach = 8f;

		// ScaleWorld 배율. 실행용 복제본에서만 1 이 아님 (리터럴 크기 값에 곱함)
		[System.NonSerialized] private float sizeScale = 1f;

		private int StarCount => pattern.Length;
		private float StarSpan => starInterval * (StarCount - 1);
		private float LinkedTime => firstStarTime + StarSpan + starInterval;

		private float StarTime(int order) => firstStarTime + StarSpan * Mathf.Pow(order / (float)Mathf.Max(1, StarCount - 1), starPace);
		private float PillarTime => LinkedTime + hushSeconds;
		private float StrikeTime => PillarTime + pillarFallSeconds;
		private float BurstTime => StrikeTime + pourSeconds;
		private float RestoreTime => BurstTime + 0.5f;

		public override float DurationSeconds => BurstTime + lingerSeconds;

		public override void ScaleWorld(float scale)
		{
			sizeScale = scale;
			patternRadius *= scale;
			starSize *= scale;
			lineRadius *= scale;
			groundDotSize *= scale;
			pillarRadius *= scale;
			starLightRange *= scale;
			strikeLightRange *= scale;
			crossLength *= scale;
			scorchSize *= scale;
			windReach *= scale;
			shardsPerStar = Mathf.RoundToInt(shardsPerStar * scale);
			fragmentCount = Mathf.RoundToInt(fragmentCount * scale);
		}

		protected override void ComposeWorld(Stage stage)
		{
			Vector3 center = stage.Target;
			List<Star> stars = LayOut(stage, center);
			Light starLight = WorldLight(stage, "ConstellationStarLight", LightType.Point, lightColor, starLightRange);
			Light strikeLight = WorldLight(stage, "ConstellationStrikeLight", LightType.Point, lightColor, strikeLightRange);
			ComposeStars(stage, stars, starLight);
			ComposeLinks(stage, stars);
			// 기둥은 별자리가 닫은 자리 (국자 안) 한가운데로. 닫는 줄이 없으면 표적
			Vector3 strike = center;
			if (closeTo >= 0 && closeTo < stars.Count - 1)
			{
				strike = Vector3.zero;
				for (int i = closeTo; i < stars.Count; i++)
					strike += stars[i].Ground;
				strike /= stars.Count - closeTo;
				strike.y = UltimateFxStage.GroundHeight(strike);
			}
			ComposePillar(stage, strike, strikeLight);
			ComposePour(stage, strike, stars);
			ComposeBurst(stage, strike);
			ComposeGrade(stage);
		}

		// 별 자리: pattern 을 화면 축에 맞춰 땅에 놓음 (어느 방향에서 시전해도 화면에서 같은 모양)
		private List<Star> LayOut(Stage stage, Vector3 center)
		{
			UltimateFxRun run = stage.Run;
			List<Star> stars = new();
			for (int order = 0; order < pattern.Length; order++)
			{
				Vector3 ground = center + (run.ScreenRight * pattern[order].x + run.ScreenForward * pattern[order].y) * patternRadius;
				ground.y = UltimateFxStage.GroundHeight(ground);
				stars.Add(new Star
				{
					Ground = ground,
					Position = ground + Vector3.up * starSize * 0.55f,
					LightTime = StarTime(order),
					BreakTime = StrikeTime + 0.06f + order * pourSeconds * 0.72f / pattern.Length,
					Lean = Quaternion.Euler(Random.Range(-starLean, starLean), Random.value * 360f, Random.Range(-starLean, starLean)),
				});
			}
			return stars;
		}

		// 별: 하늘에서 빛줄기로 떨어져 땅에 꽂힘. 꽂히는 순간 결정이 솟고 불티와 먼지, 땅의 빛 점. 별자리 점광이 새 별로 옮겨 감
		private void ComposeStars(Stage stage, List<Star> stars, Light light)
		{
			UltimateFxRun run = stage.Run;
			Mesh[] meshes = { Keep(run, UltimateMeshFactory.Crystal(1201, 5)), Keep(run, UltimateMeshFactory.Crystal(1207, 6)) };
			Mesh fallMesh = Keep(run, UltimateMeshFactory.OpenCylinder(8, 1f, 0f));
			ParticleSystem sparks = run.Particles("ConstellationStarSparks", run.Additive(UltimateFxTextures.Spark, Color.white), stars.Count * 22 + 20, 1.2f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
			for (int i = 0; i < stars.Count; i++)
			{
				Star star = stars[i];
				star.BodyMaterial = Glow(run, new Color(0.7f, 0.8f, 1f, 1f), Color.black, starColor * 0.5f, true);
				star.BodyMaterial.SetFloat(SPECULAR_ID, 0.6f);
				star.Body = UltimateFxStage.CreateMeshObject("ConstellationStar", run.Root, meshes[i % meshes.Length], star.BodyMaterial);
				Solid(star.Body, true);
				// 결정 메시는 바닥 중심 원점, 높이 1. 뿌리를 땅에 조금 묻음
				star.Body.transform.SetPositionAndRotation(star.Ground - Vector3.up * starSize * 0.12f, star.Lean);
				star.Body.SetActive(false);

				star.GlintMaterial = run.Additive(UltimateFxTextures.CrossGlint, Color.clear);
				GameObject glint = run.Billboard("ConstellationGlint", star.GlintMaterial, star.Position, starSize * glintRatio, starSize * glintRatio, false, false, 3);
				star.DotMaterial = run.Additive(UltimateFxTextures.Soft, Color.clear);
				run.Plate("ConstellationGroundDot", star.DotMaterial, star.Ground + Vector3.up * 0.05f, groundDotSize, 5);

				// 낙하 빛줄기: 위가 옅은 가는 원기둥이 하늘에서 내려와 꽂히는 순간 사라짐
				Material fallMaterial = run.Additive(UltimateFxTextures.White, Color.clear);
				GameObject fall = UltimateFxStage.CreateMeshObject("ConstellationStarFall", run.Root, fallMesh, fallMaterial);
				fall.SetActive(false);
				float fallStart = Mathf.Max(0f, star.LightTime - fallSeconds);
				run.At(fallStart, () => fall.SetActive(true));
				run.Track(fallStart, star.LightTime - fallStart + 0.1f, _ =>
				{
					float now = run.Now;
					float drop = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, fallStart, star.LightTime));
					float fade = 1f - UltimateFxRun.Span(now, star.LightTime, star.LightTime + 0.1f);
					float height = Mathf.Lerp(fallHeight, 0f, drop);
					fall.transform.position = star.Ground + Vector3.up * height;
					fall.transform.localScale = new Vector3(starSize * 0.35f * fade, fallHeight * 0.6f, starSize * 0.35f * fade);
					fallMaterial.SetColor("_Color", starColor * 1.4f * fade);
				}, () => run.Destroy(fall));

				run.At(star.LightTime, () =>
				{
					star.Body.SetActive(true);
					for (int spark = 0; spark < 20; spark++)
					{
						Vector3 direction = (Random.onUnitSphere + Vector3.up * 1.3f).normalized;
						UltimateFxStage.Emit(sparks, star.Ground + Vector3.up * 0.15f, direction * Random.Range(3f, 8f) * sizeScale, Random.Range(0.05f, 0.1f), Random.Range(0.25f, 0.55f), new Color(0.8f, 0.9f, 1f, 1f));
					}
					run.Dust(star.Ground, 1.1f * sizeScale, 5, dustColor);
				});
				run.Track(star.LightTime, star.BreakTime - star.LightTime, _ =>
				{
					float now = run.Now;
					// 꽂히며 눌렸다 솟음 (짧게 넘쳤다 자리 잡음)
					float pop = OverShoot((now - star.LightTime) / 0.12f, 0.6f);
					float flare = 1f + 3f * Spike(now, star.LightTime, 0.01f, 0.1f);
					float twinkle = 0.85f + 0.15f * Mathf.Sin(now * 9f + star.LightTime * 31f);
					star.Body.transform.localScale = new Vector3(starSize, starSize * pop, starSize);
					star.BodyMaterial.SetColor(EMISSION_ID, starColor * flare * twinkle);
					star.GlintMaterial.SetColor("_Color", starColor * 0.3f * flare * twinkle);
					glint.transform.localScale = new Vector3(starSize * glintRatio, starSize * glintRatio, 1f) * (0.8f + 0.9f * Spike(now, star.LightTime, 0.01f, 0.12f));
					float settle = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, star.LightTime, star.LightTime + 0.1f));
					star.DotMaterial.SetColor("_Color", lineColor * 0.6f * settle * twinkle * (1f + 2f * Spike(now, star.LightTime, 0.01f, 0.12f)));
				}, () =>
				{
					star.Body.SetActive(false);
					star.GlintMaterial.SetColor("_Color", Color.clear);
					run.Destroy(glint);
				});
				// 땅의 빛 점은 별이 부서진 뒤 잠깐 남았다 꺼짐
				run.Track(star.BreakTime, 0.4f, progress => star.DotMaterial.SetColor("_Color", lineColor * 0.6f * (1f - progress)));
			}

			run.Track(firstStarTime, StrikeTime - firstStarTime, _ =>
			{
				float now = run.Now;
				int newest = 0;
				for (int i = 1; i < stars.Count; i++)
				{
					if (now >= stars[i].LightTime)
						newest = i;
				}
				// 새 별로 바로 옮김 (꽂힘마다 빛이 튀게)
				light.transform.position = stars[newest].Ground + Vector3.up * 1.2f + stage.ToCamera * 0.6f;
				float rise = Mathf.Lerp(0.5f, 1f, UltimateFxRun.Span(now, firstStarTime, LinkedTime));
				float hush = 1f - 0.5f * UltimateFxRun.Span(now, LinkedTime, PillarTime);
				light.intensity = starLightIntensity * rise * hush * (1f + 1.5f * Spike(now, stars[newest].LightTime, 0.01f, 0.08f));
			}, () => light.enabled = false);
		}

		// 빛줄: 별이 박히면 앞 별에서 그 별까지 땅 위로 관이 달려 이어짐. 앞끝에 빛 알갱이. 잇는 두 별 중 하나가 부서지면 꺼짐
		private void ComposeLinks(Stage stage, List<Star> stars)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem tips = Motes(run, "ConstellationLinkTip", stars.Count * 30 + 40, 0f, 0.1f);
			for (int i = 1; i < stars.Count; i++)
				ComposeLink(run, tips, stars[i - 1], stars[i], Mathf.Lerp(stars[i - 1].LightTime, stars[i].LightTime, 0.3f), stars[i].LightTime);
			// 닫는 줄: 마지막 별이 박힌 직후 한 번 더
			if (closeTo >= 0 && closeTo < stars.Count - 1)
			{
				Star last = stars[stars.Count - 1];
				ComposeLink(run, tips, last, stars[closeTo], last.LightTime + 0.02f, last.LightTime + starInterval * 0.6f);
			}
		}

		private void ComposeLink(UltimateFxRun run, ParticleSystem tips, Star from, Star to, float start, float arrive)
		{
			Vector3 lift = Vector3.up * (0.1f * sizeScale);
			Vector3 origin = from.Ground + lift;
			Vector3 target = to.Ground + lift;
			Mesh mesh = Keep(run, new Mesh { name = "ConstellationLink" });
			Material material = run.Additive(UltimateFxTextures.White, lineColor);
			GameObject link = UltimateFxStage.CreateMeshObject("ConstellationLink", run.Root, mesh, material);
			link.SetActive(false);
			run.At(start, () => link.SetActive(true));
			run.Track(start, arrive - start, progress =>
			{
				// 느리게 출발해 끝에서 가장 빠름 (다음 별이 꽂히는 순간에 닿음)
				Vector3 head = Vector3.Lerp(origin, target, UltimateFxRun.EaseIn(progress));
				FillLink(mesh, origin, head);
				UltimateFxStage.Emit(tips, head, Random.onUnitSphere * 0.5f * sizeScale, Random.Range(0.08f, 0.16f) * sizeScale, Random.Range(0.15f, 0.3f), Color.white);
			}, () => FillLink(mesh, origin, target));
			float breakTime = Mathf.Min(from.BreakTime, to.BreakTime);
			run.Track(arrive, breakTime - arrive, _ => material.SetColor("_Color", lineColor * (1f + 2f * Spike(run.Now, arrive, 0.01f, 0.12f)) * (0.85f + 0.15f * Mathf.Sin(run.Now * 13f + start * 17f))));
			run.Track(breakTime, 0.15f, progress => material.SetColor("_Color", lineColor * (1f - progress) * 2f), () => run.Destroy(link));
		}

		private void FillLink(Mesh mesh, Vector3 from, Vector3 to)
		{
			List<IReadOnlyList<Vector3>> strands = new() { new List<Vector3> { from, Vector3.Lerp(from, to, 0.5f), to } };
			List<IReadOnlyList<float>> radii = new() { new List<float> { lineRadius, lineRadius, lineRadius } };
			UltimateMeshFactory.FillTube(mesh, strands, radii, 6, 1f, 1f);
		}

		// 별빛 기둥: 하늘에서 표적으로 곧게 내리꽂힘 (흰 심, 푸른 겉). 쏟아지는 동안 굵기가 박동하고 터질 때 가늘어지며 사라짐. 접지 점광, 그을림
		private void ComposePillar(Stage stage, Vector3 center, Light light)
		{
			UltimateFxRun run = stage.Run;
			Mesh cylinder = Keep(run, UltimateMeshFactory.OpenCylinder(20, 1f, 0.2f));
			Material coreMaterial = run.Additive(UltimateFxTextures.White, Color.clear);
			// Beam 그림은 색 띠가 있어 청록 줄무늬로 보임 (3회차 촬영). 흑백 잡음 줄무늬에 색을 입힘
			Material outerMaterial = run.Additive(UltimateFxTextures.TiledStreaks, Color.clear);
			GameObject core = UltimateFxStage.CreateMeshObject("ConstellationPillarCore", run.Root, cylinder, coreMaterial);
			GameObject outer = UltimateFxStage.CreateMeshObject("ConstellationPillarOuter", run.Root, cylinder, outerMaterial);
			core.SetActive(false);
			outer.SetActive(false);
			Vector3 top = center + Vector3.up * pillarHeight;
			run.At(PillarTime, () =>
			{
				core.SetActive(true);
				outer.SetActive(true);
			});
			run.Track(PillarTime, BurstTime + 0.25f - PillarTime, _ =>
			{
				float now = run.Now;
				float fall = UltimateFxRun.EaseIn(UltimateFxRun.Span(now, PillarTime, StrikeTime));
				float pour = UltimateFxRun.Span(now, StrikeTime, BurstTime);
				float close = 1f - UltimateFxRun.EaseIn(UltimateFxRun.Span(now, BurstTime, BurstTime + 0.25f));
				float pulse = 1f + 0.18f * Mathf.Sin(now / 0.14f * Mathf.PI * 2f) * pour;
				// 닿는 순간 굵고, 쏟아지는 동안 조금 더 굵어짐
				float swell = (1f + 0.8f * Spike(now, StrikeTime, 0.02f, 0.15f)) * Mathf.Lerp(0.8f, 1.15f, pour);
				float length = pillarHeight * fall;
				// 기둥 메시는 바닥 원점에서 위로. 꼭대기에 두고 아래로 뻗게 뒤집음
				Quaternion down = Quaternion.FromToRotation(Vector3.up, Vector3.down);
				core.transform.SetPositionAndRotation(top, down);
				outer.transform.SetPositionAndRotation(top, down);
				core.transform.localScale = new Vector3(pillarRadius.x * 2f * swell * close, length, pillarRadius.x * 2f * swell * close);
				outer.transform.localScale = new Vector3(pillarRadius.y * 2f * swell * pulse * close, length, pillarRadius.y * 2f * swell * pulse * close);
				outerMaterial.mainTextureOffset = new Vector2(now * 0.4f, now * 5f);
				coreMaterial.SetColor("_Color", pillarCore * pillarBrightness.x * close);
				outerMaterial.SetColor("_Color", pillarOuter * pillarBrightness.y * pulse * close);
			}, () =>
			{
				run.Destroy(core);
				run.Destroy(outer);
			});

			light.transform.position = center + Vector3.up * 1.3f + stage.ToCamera * 0.6f;
			run.Track(StrikeTime, BurstTime + 1.4f - StrikeTime, _ =>
			{
				float now = run.Now;
				light.enabled = true;
				float strike = strikeLightIntensity * Spike(now, StrikeTime, 0.03f, 0.25f);
				float pour = now < BurstTime ? pourLightIntensity * Mathf.Lerp(0.7f, 1.2f, UltimateFxRun.Span(now, StrikeTime, BurstTime)) * (1f + 0.15f * Mathf.Sin(now / 0.14f * Mathf.PI * 2f)) : 0f;
				float burst = burstLightIntensity * Spike(now, BurstTime, 0.02f, 0.45f);
				light.intensity = Mathf.Max(strike, Mathf.Max(pour, burst));
			}, () => light.enabled = false);

			run.At(StrikeTime, () =>
			{
				run.SparkBurst(center + Vector3.up * 0.2f, 70, new Vector2(5f, 14f), new Vector2(0.25f, 0.6f), new Vector2(0.06f, 0.14f), new Color(0.75f, 0.88f, 1f, 1f), Color.white, 0.8f);
				DustRing(run, center, 26, 2.6f * sizeScale, dustColor, 1.5f * sizeScale);
				run.React(center, windSpeed, windReach, true);
			});
			GroundGlow(run, center, pillarRadius.y * 9f, pillarOuter * 1.4f, StrikeTime, 0.02f, 0.3f, pourSeconds + 0.4f);
			Texture2D scorch = ScorchTexture(run, 707, new Color(0.04f, 0.05f, 0.09f, 0.85f), new Color(0.55f, 0.8f, 1f, 1f), 0.7f);
			GroundDecal(run, scorch, center, scorchSize, StrikeTime, 0.05f, DurationSeconds - 0.9f, 0.9f, 2);
		}

		// 쏟아짐: 별이 차례로 부서지며 빛 알갱이가 기둥 쪽으로 곧게 날아들고, 기둥을 타고 내려와 접지점에서 튐
		private void ComposePour(Stage stage, Vector3 center, List<Star> stars)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem shards = Streaks(run, "ConstellationShards", stars.Count * shardsPerStar + 20, 0.05f);
			ParticleSystem flow = Streaks(run, "ConstellationFlow", Mathf.CeilToInt(flowPerSecond * 0.6f) + 40, 0.04f);
			ParticleSystem sparks = run.Particles("ConstellationSparks", run.Additive(UltimateFxTextures.Spark, Color.white), Mathf.CeilToInt(sparkPerSecond * 0.8f) + 40, 1.1f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
			foreach (Star star in stars)
			{
				run.At(star.BreakTime, () =>
				{
					// 기둥 축의 허리 높이로 모임 (땅을 스치며 기둥으로)
					Vector3 gather = new(center.x, center.y + 1.6f * sizeScale, center.z);
					for (int i = 0; i < shardsPerStar; i++)
					{
						float life = Random.Range(0.14f, 0.26f);
						Vector3 from = star.Position + Random.insideUnitSphere * starSize * 0.4f;
						UltimateFxStage.Emit(shards, from, (gather - from) / life, Random.Range(0.08f, 0.16f) * sizeScale, life, new Color(0.8f, 0.9f, 1f, 1f));
					}
					run.SparkBurst(star.Position, 14, new Vector2(2f, 6f), new Vector2(0.2f, 0.45f), new Vector2(0.05f, 0.1f), Color.white, new Color(0.6f, 0.8f, 1f, 1f), 0.4f);
				});
			}
			float flowCarry = 0f;
			float sparkCarry = 0f;
			run.Track(StrikeTime, pourSeconds, progress =>
			{
				// 쏟아지는 양이 점점 늘어 터짐 직전 가장 많음
				float amount = Mathf.Lerp(0.35f, 1f, progress);
				flowCarry += flowPerSecond * amount * run.Delta;
				sparkCarry += sparkPerSecond * amount * run.Delta;
				for (; flowCarry >= 1f; flowCarry -= 1f)
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(0f, pillarRadius.y);
					float height = Random.Range(pillarHeight * 0.45f, pillarHeight);
					float speed = Random.Range(20f, 30f);
					UltimateFxStage.Emit(flow, center + offset + Vector3.up * height, Vector3.down * speed, Random.Range(0.1f, 0.2f) * sizeScale, height / speed, new Color(0.85f, 0.92f, 1f, 1f));
				}
				for (; sparkCarry >= 1f; sparkCarry -= 1f)
				{
					Vector3 velocity = (Random.onUnitSphere + Vector3.up * 1.2f).normalized * Random.Range(3f, 9f) * sizeScale;
					UltimateFxStage.Emit(sparks, center + Vector3.up * 0.1f, velocity, Random.Range(0.05f, 0.1f), Random.Range(0.25f, 0.6f), new Color(0.75f, 0.88f, 1f, 1f));
				}
			});
		}

		// 네 갈래 터짐: 기둥이 꺼지며 땅을 따라 네 방향으로 빛이 달려 나가고 위로 솟음. 땅의 별 모양 빛, 별 조각이 떨어져 남음
		private void ComposeBurst(Stage stage, Vector3 center)
		{
			UltimateFxRun run = stage.Run;
			ParticleSystem rays = Streaks(run, "ConstellationCrossRays", 220, 0.07f);
			ParticleSystem rise = Streaks(run, "ConstellationRise", 80, 0.06f);
			Material fragmentMaterial = Glow(run, new Color(0.6f, 0.7f, 0.9f, 1f), starColor * 0.35f, starColor * 0.3f, false);
			ParticleSystem fragments = MeshParticles(run, "ConstellationFragments", Keep(run, UltimateMeshFactory.Rock(1213, Color.white, 0.4f)), fragmentMaterial, fragmentCount + 8, 1.5f, true, 0.3f, 0.5f, false);
			// 땅에 누운 네 갈래 빛 (월드 판). 화면을 향한 그림이 아니라 지면에 붙어 원근으로 눕음
			Material crossMaterial = run.Additive(UltimateFxTextures.CrossGlint, Color.clear);
			GameObject cross = run.Plate("ConstellationGroundCross", crossMaterial, center + Vector3.up * 0.07f, crossLength * 2.4f, 7);
			float yaw = Mathf.Atan2(run.ScreenRight.x, run.ScreenRight.z) * Mathf.Rad2Deg;
			cross.transform.rotation = Quaternion.Euler(0f, yaw + 45f, 0f);
			run.Track(BurstTime, 1.2f, _ =>
			{
				float now = run.Now;
				float grow = UltimateFxRun.EaseOut(UltimateFxRun.Span(now, BurstTime, BurstTime + 0.12f));
				float size = crossLength * 2.4f * Mathf.Lerp(0.3f, 1f, grow);
				cross.transform.localScale = new Vector3(size, 1f, size);
				crossMaterial.SetColor("_Color", pillarCore * 1.2f * Spike(now, BurstTime, 0.02f, 0.22f));
			}, () => run.Destroy(cross));

			run.At(BurstTime, () =>
			{
				Vector3[] directions =
				{
					(run.ScreenRight + run.ScreenForward).normalized,
					(run.ScreenRight - run.ScreenForward).normalized,
					(-run.ScreenRight + run.ScreenForward).normalized,
					(-run.ScreenRight - run.ScreenForward).normalized,
				};
				foreach (Vector3 direction in directions)
				{
					for (int i = 0; i < 46; i++)
					{
						float speed = Random.Range(0.4f, 1f) * crossLength / 0.22f;
						Vector3 side = Vector3.Cross(Vector3.up, direction) * Random.Range(-0.12f, 0.12f) * sizeScale;
						UltimateFxStage.Emit(rays, center + side + Vector3.up * Random.Range(0.1f, 0.35f), direction * speed, Random.Range(0.1f, 0.22f) * sizeScale, Random.Range(0.18f, 0.3f), Color.white);
					}
				}
				for (int i = 0; i < 60; i++)
				{
					Vector3 velocity = (Vector3.up + Random.insideUnitSphere * 0.18f).normalized * Random.Range(10f, 24f);
					UltimateFxStage.Emit(rise, center + Vector3.up * 0.3f, velocity, Random.Range(0.1f, 0.2f) * sizeScale, Random.Range(0.2f, 0.4f), new Color(0.8f, 0.9f, 1f, 1f));
				}
				for (int i = 0; i < fragmentCount; i++)
				{
					Vector3 velocity = (Random.onUnitSphere + Vector3.up * 0.9f).normalized * Random.Range(4f, 10f) * Mathf.Sqrt(sizeScale);
					EmitMesh(fragments, center + Vector3.up * 0.4f, velocity, Random.Range(0.12f, 0.28f) * sizeScale, lingerSeconds * Random.Range(0.7f, 1f), Color.white, Random.rotation.eulerAngles, Random.onUnitSphere * 400f);
				}
				run.SparkBurst(center + Vector3.up * 0.3f, 120, new Vector2(8f, 20f), new Vector2(0.3f, 0.7f), new Vector2(0.08f, 0.18f), new Color(0.75f, 0.88f, 1f, 1f), Color.white, 0.7f);
				DustRing(run, center, 44, crossLength * 0.7f, dustColor, 1.9f * sizeScale);
				run.Debris(center, 1.4f * sizeScale, 16, 9f * sizeScale, new Color(0.3f, 0.3f, 0.34f, 1f), new Color(0.7f, 0.85f, 1f, 1f), 0.5f);
				run.React(center, windSpeed, windReach * 1.6f, true);
			});
			GroundGlow(run, center, crossLength * 2f, pillarOuter * 1f, BurstTime, 0.02f, 0.35f, 1.6f);
		}

		private void ComposeGrade(Stage stage)
		{
			float restore = RestoreTime;
			stage.BloomTint = new Color(0.7f, 0.82f, 1f);
			float Night(float now) => Plateau(now, 0f, firstStarTime + 0.3f, restore, restoreSeconds);
			// 숨 멎음: 다 이어진 뒤 기둥 전까지 한 번 더 어두워짐
			float Hush(float now) => UltimateFxRun.Span(now, LinkedTime, LinkedTime + hushSeconds * 0.5f) * (1f - UltimateFxRun.Span(now, StrikeTime, StrikeTime + 0.1f));
			stage.SunScale = now => Mathf.Lerp(Mathf.Lerp(1f, sunDim, Night(now)), hushSunDim, Hush(now));
			stage.AmbientScale = now => Mathf.Lerp(1f, ambientDim, Night(now)) * Mathf.Lerp(1f, 0.75f, Hush(now));
			stage.Saturation = now => nightSaturation * Night(now);
			stage.Vignette = now => vignette * Night(now);
			stage.Filter = now => Color.Lerp(Color.white, tintColor, Night(now) * 0.8f);
			stage.Bloom = now => Mathf.Max(0.6f * UltimateFxRun.Span(now, firstStarTime, LinkedTime), bloom * Plateau(now, PillarTime, 0.1f, restore, restoreSeconds)) * (1f - UltimateFxRun.Span(now, restore, restore + restoreSeconds));
		}
	}
}
