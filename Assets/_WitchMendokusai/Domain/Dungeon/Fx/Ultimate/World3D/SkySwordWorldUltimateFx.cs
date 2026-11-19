using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 하늘 검 궁극기 3D 판. 화면 위 그림 없이 월드 안의 메시, 광원, 입자, 데칼, 카메라로만 구성
	/// - 흐름: 해가 어두워지며 시전자 손에 빛 -> 실제 검 메시가 하늘에서 내려와 돌며 멈춤 (검의 점광과 아래로 쏘는 스포트가 바닥, 풀, 인형을 비춤)
	///   -> 충돌 500ms 전 스포트 원이 밝아짐 (크기 고정) -> 0.15초 낙하 -> 꽂힘: 지면 광원, 노출 섬광, 흔들림, 바위 솟음, 메시 파편, 먼지 고리, 연기, 불씨, 균열 데칼, 풀 쓰러짐
	///   -> 검이 남아 식고 디졸브로 사라짐, 해 복귀
	/// - 수치 근거: memo wm/design/ult-3d-refs-20261119 (8작품 11장면 실측). 낙하 100~250ms, 예고 450~500ms, 섬광 반치폭 70~170ms, 흔들림 1~2% 화면폭 250~800ms, 지면 광원 반감 0.5~1.8초, 균열 2.5초 이상 잔류, 히트스톱과 줌 펀치 없음
	/// - 검 메시: Claymore by Quaternius (CC0), 바위: 프로젝트 Gravel 모델
	/// - 재생: 개발 창 fx ultimate ult3d, 프리셋은 메뉴 WM/CombatFx/Build Ultimate 3D Presets
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_01_SkySword", menuName = "WM/CombatFx/Ultimate/" + nameof(SkySwordWorldUltimateFx))]
	public sealed partial class SkySwordWorldUltimateFx : UltimateFx
	{
		[Header("3D 연결")]
		[SerializeField, Tooltip("칼끝이 원점, 손잡이가 +Y, 길이 1 인 검 모델")] private GameObject swordModel;
		[SerializeField, Tooltip("솟는 바위와 파편 메시")] private Mesh rockMesh;
		[SerializeField] private Shader meshGlowShader;
		[SerializeField] private Shader litParticleShader;

		[Header("색")]
		[SerializeField] private Color bladeColor = new(0.55f, 0.58f, 0.66f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color bladeGlow = new(0.18f, 0.16f, 0.12f);
		[SerializeField, ColorUsage(false, true)] private Color bladeRim = new(0.3f, 0.28f, 0.22f);
		[SerializeField, Range(0f, 1f), Tooltip("칼날 반사 세기")] private float bladeSpecular = 0.55f;
		[SerializeField, Range(1f, 128f), Tooltip("칼날 반사 날카로움. 낮을수록 넓게")] private float bladeSmoothness = 48f;
		[SerializeField] private Color hiltColor = new(0.32f, 0.3f, 0.26f, 1f);
		[SerializeField] private Color gripColor = new(0.08f, 0.06f, 0.05f, 1f);
		[SerializeField] private Color swordLightColor = new(1f, 0.93f, 0.75f, 1f);
		[SerializeField] private Color impactLightColor = new(1f, 0.72f, 0.42f, 1f);
		[SerializeField] private Color rockColor = new(0.4f, 0.33f, 0.24f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color rockHeat = new(2.2f, 0.9f, 0.3f);
		[SerializeField] private Color dustColor = new(0.62f, 0.56f, 0.48f, 0.75f);
		[SerializeField] private Color smokeColor = new(0.5f, 0.48f, 0.46f, 0.55f);
		[SerializeField, ColorUsage(true, true)] private Color emberColor = new(2.5f, 1.6f, 0.7f, 1f);
		[SerializeField] private Color scorchColor = new(0.06f, 0.05f, 0.04f, 0.92f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("해가 어두워지는 시간")] private float dimSeconds = 0.8f;
		[SerializeField, Min(0f), Tooltip("검이 하늘에서 나타나는 시각")] private float summonStart = 0.3f;
		[SerializeField, Min(0.1f), Tooltip("검이 내려와 멈추기까지")] private float descendSeconds = 0.9f;
		[SerializeField, Min(0.05f), Tooltip("검이 녹아 나타나는 시간")] private float materializeSeconds = 0.5f;
		[SerializeField, Min(0f), Tooltip("멈춰 떠 있는 시간")] private float hangSeconds = 0.4f;
		[SerializeField, Min(0.05f), Tooltip("실측 100~250ms")] private float fallSeconds = 0.15f;
		[SerializeField, Min(0.1f), Tooltip("실측 450~500ms. 충돌 전 스포트 원이 밝아지는 시간")] private float telegraphSeconds = 0.5f;
		[SerializeField, Min(0.01f), Tooltip("접지 뒤 최대 섬광까지. 실측 100~150ms")] private float flashDelay = 0.12f;
		[SerializeField, Min(0.1f), Tooltip("검이 꽂힌 채 남는 시간")] private float remainSeconds = 1.6f;
		[SerializeField, Min(0.1f)] private float vanishSeconds = 0.6f;
		[SerializeField, Min(0.1f)] private float restoreSeconds = 1.4f;

		[Header("크기와 배치 (월드 단위)")]
		[SerializeField, Min(1f), Tooltip("검 길이. 실측 4~5 인물 키")] private float swordLength = 5.4f;
		[SerializeField, Min(0.5f), Tooltip("모델 폭 배율 (쿼터뷰에서 칼날이 읽히게)")] private float swordWidthScale = 1.7f;
		[SerializeField, Min(0.5f)] private float swordThicknessScale = 1.3f;
		[SerializeField, Tooltip("표적의 화면 위쪽 거리. 검이 인물 뒤에 꽂혀 인물을 가리지 않게")] private float targetDistance = 2.8f;
		[SerializeField, Min(1f), Tooltip("나타나는 칼끝 높이 (화면 밖)")] private float spawnHeight = 22f;
		[SerializeField, Min(0.5f), Tooltip("멈춘 칼끝 높이")] private float hangHeight = 2.4f;
		[SerializeField, Range(0f, 0.5f), Tooltip("땅에 박히는 깊이 (길이 비율)")] private float sinkRatio = 0.16f;
		[SerializeField, Tooltip("내려오는 동안 도는 총 각도")] private float spinDegrees = 900f;
		[SerializeField, Range(0f, 40f), Tooltip("하강과 떠 있는 동안 손잡이가 카메라 쪽으로 기우는 각도. 원근으로 길이와 두께가 읽히게. 낙하 중 바로 섬")] private float hangTilt = 16f;

		[Header("빛")]
		[SerializeField, Range(0f, 1f), Tooltip("어두워진 해 세기 배율")] private float sunDim = 0.5f;
		[SerializeField, Range(0f, 1f)] private float ambientDim = 0.6f;
		[SerializeField, Min(0f)] private float casterLightIntensity = 6f;
		[SerializeField, Min(0.5f)] private float casterLightRange = 4.5f;
		[SerializeField, Min(0f), Tooltip("검이 나타난 뒤 시전자 빛이 남는 시간")] private float casterLightOverlap = 0.2f;
		[SerializeField, Min(0f)] private float swordLightIntensity = 14f;
		[SerializeField, Min(1f)] private float swordLightRange = 11f;
		[SerializeField, Min(0f), Tooltip("충돌 직전 스포트 세기")] private float spotIntensity = 36f;
		[SerializeField, Range(5f, 90f)] private float spotAngle = 38f;
		[SerializeField, Min(0f), Tooltip("스포트 예고 전 세기 비율")] private float spotIdleRatio = 0.25f;
		[SerializeField, Min(0f), Tooltip("지면 광원 최대 세기")] private float impactLightIntensity = 34f;
		[SerializeField, Min(1f), Tooltip("실측: 폭발 반경의 1.0~1.2배")] private float impactLightRange = 13f;
		[SerializeField, Min(0f)] private float impactLightHeight = 2f;
		[SerializeField, Range(0f, 1f), Tooltip("접지 순간 세기 비율")] private float contactRatio = 0.4f;
		[SerializeField, Min(0.05f), Tooltip("실측 반감 0.5~1.8초")] private float impactHalfLife = 0.7f;
		[SerializeField, Min(0f), Tooltip("빛 안 받는 인형 그림이 연출 광원을 받는 배율")] private float spriteLightGain = 0.12f;
		[SerializeField, Min(1f)] private float spriteTintCap = 2.2f;
		[SerializeField, Min(0.05f)] private float spriteRefreshSeconds = 0.3f;

		[Header("바닥 반응")]
		[SerializeField, Min(0), Tooltip("검 둘레에서 솟는 바위 수")] private int upheavalCount = 11;
		[SerializeField] private Vector2 upheavalRadius = new(1.3f, 3.1f);
		[SerializeField] private Vector2 upheavalScale = new(1.6f, 3f);
		[SerializeField, Tooltip("바깥으로 기우는 각도 범위 (도)")] private Vector2 upheavalTilt = new(20f, 50f);
		[SerializeField, Min(0.01f)] private float upheavalRiseSeconds = 0.12f;
		[SerializeField, Min(0), Tooltip("실측 메시 파편 6~10")] private int debrisCount = 14;
		[SerializeField, Min(0f)] private float debrisSpeed = 9f;
		[SerializeField, Range(0f, 1f)] private float debrisHotChance = 0.35f;
		[SerializeField, Min(0)] private int rayCount = 70;
		[SerializeField, Min(0)] private int dustCount = 46;
		[SerializeField, Min(0.5f)] private float dustRadius = 6f;
		[SerializeField, Min(0)] private int smokeCount = 18;
		[SerializeField, Min(0)] private int emberCount = 46;
		[SerializeField, Min(0)] private int trailCount = 26;
		[SerializeField, Min(0)] private int gatherCount = 40;
		[SerializeField, Min(1f)] private float decalSize = 9f;
		[SerializeField] private int crackSeed = 41;
		[SerializeField, Min(0.1f), Tooltip("검이 사라진 뒤 그을림이 옅어지는 시간. 실측 2.5초 이상 잔류")] private float scorchLingerSeconds = 1.4f;
		[SerializeField, Min(1f), Tooltip("풀이 쓰러지는 바람 속도")] private float windSpeed = 16f;
		[SerializeField, Min(1f)] private float windReach = 13f;
		[SerializeField, Range(-90f, 90f), Tooltip("멈춘 검이 카메라 정면에서 돌아간 각도. 0 이면 칼날 면만 보여 판처럼 읽힘")] private float faceYawOffset = 35f;

		[Header("카메라와 후처리")]
		[SerializeField, Range(1f, 1.6f), Tooltip("검 전체가 들어오게 물러나는 배율")] private float pullBack = 1.32f;
		[SerializeField, Min(0f)] private float shakeStrength = 1.6f;
		[SerializeField, Min(1)] private int shakeCount = 9;
		[SerializeField, Min(0.01f)] private float shakeInterval = 0.07f;
		[SerializeField, Range(0f, 1f)] private float shakeDecay = 0.74f;
		[SerializeField, Min(0f), Tooltip("충돌 노출 섬광 (EV)")] private float exposureFlash = 1.2f;
		[SerializeField, Min(0.01f), Tooltip("노출 섬광 반감")] private float exposureHalfLife = 0.1f;
		[SerializeField, Min(0f)] private float bloomSummon = 0.9f;
		[SerializeField, Min(0f)] private float bloomImpact = 2.2f;
		[SerializeField, Min(0f)] private float bloomThreshold = 1.2f;
		[SerializeField, Range(0f, 1f)] private float chromaticImpact = 0.35f;
		[SerializeField, Range(0f, 1f)] private float vignetteSummon = 0.28f;

		private float DescendEnd => summonStart + descendSeconds;
		private float FallStart => DescendEnd + hangSeconds;
		private float ImpactTime => FallStart + fallSeconds;
		private float VanishStart => ImpactTime + remainSeconds;
		private float RestoreStart => VanishStart - restoreSeconds * 0.5f;

		public override float DurationSeconds => VanishStart + vanishSeconds + scorchLingerSeconds;

		public override void Compose(UltimateFxRun run)
		{
			UltimateWorldLighting lighting = new(run.MainCamera, spriteLightGain, spriteTintCap, spriteRefreshSeconds);
			run.OnRelease(lighting.Dispose);
			Vector3 target = run.Center + run.ScreenForward * targetDistance;
			target.y = UltimateFxStage.GroundHeight(target);

			ComposeSky(run, lighting);
			ComposeGather(run, lighting);
			Transform sword = ComposeSword(run, lighting, target);
			ComposeImpact(run, lighting, target, sword);

			// 하늘 감독 Update 뒤에 매 프레임 덮음. 트랙 하나로 끝까지
			run.Track(0f, DurationSeconds, _ => lighting.Apply(run.Delta));
		}

		private void ComposeSky(UltimateFxRun run, UltimateWorldLighting lighting)
		{
			run.Track(0f, DurationSeconds, _ =>
			{
				float darken = UltimateFxRun.EaseInOut(run.Now / dimSeconds);
				float restore = UltimateFxRun.EaseInOut(UltimateFxRun.Span(run.Now, RestoreStart, RestoreStart + restoreSeconds));
				float amount = darken * (1f - restore);
				lighting.SetSkyDim(Mathf.Lerp(1f, sunDim, amount), Mathf.Lerp(1f, ambientDim, amount));
				lighting.SetVignette(vignetteSummon * amount, Color.black);
				float impactAge = run.Now - ImpactTime;
				float bloom = bloomSummon * amount;
				float exposure = 0f;
				float chromatic = 0f;
				if (impactAge >= 0f)
				{
					// 노출 섬광: 접지 뒤 flashDelay 에 최대, 반감 exposureHalfLife
					float rise = Mathf.Clamp01(impactAge / flashDelay);
					float fall = impactAge <= flashDelay ? 1f : Mathf.Pow(0.5f, (impactAge - flashDelay) / exposureHalfLife);
					exposure = exposureFlash * rise * fall;
					chromatic = chromaticImpact * Mathf.Pow(0.5f, impactAge / 0.12f);
					bloom = Mathf.Max(bloom, bloomImpact * Mathf.Pow(0.5f, impactAge / 0.4f));
				}
				lighting.SetExposure(exposure);
				lighting.SetChromatic(chromatic);
				lighting.SetBloom(bloom, bloomThreshold, Color.white);
			});

			run.At(0f, () =>
			{
				System.Action release = run.Zoom(pullBack, 0f, summonStart + descendSeconds * 0.7f, float.PositiveInfinity, 1.1f);
				run.At(VanishStart, () => release());
			});
		}

		// 시전자 손에 빛이 모이고 빛 알갱이가 하늘로 올라감
		private void ComposeGather(UltimateFxRun run, UltimateWorldLighting lighting)
		{
			Vector3 hand = run.Caster + Vector3.up * 1.5f;
			Light casterLight = run.PointLight("CasterLight", swordLightColor, casterLightRange);
			casterLight.transform.position = hand;
			lighting.Track(casterLight);
			// 검이 나타나면 끔. 장면에 이미 추가 광원 2개 (점광, 보조 직사광) 가 있어 연출 광원은 동시에 2개까지 (물체당 4개 상한)
			run.Track(0f, summonStart + casterLightOverlap, progress =>
			{
				float pulse = UltimateFxRun.Bell(progress, 0.55f);
				casterLight.intensity = casterLightIntensity * pulse * (0.85f + 0.15f * Mathf.Sin(run.Now * 40f));
			}, () => run.Destroy(casterLight.gameObject));

			ParticleSystem motes = run.Particles("GatherMotes", run.Additive(UltimateFxTextures.Soft, Color.white), gatherCount + 8, -0.2f);
			UltimateFxStage.SetLifetimeLook(motes, UltimateFxStage.FadeGradient(Color.white, 0.1f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
			for (int i = 0; i < gatherCount; i++)
			{
				float time = Mathf.Lerp(0.05f, DescendEnd * 0.8f, i / (float)Mathf.Max(1, gatherCount - 1));
				run.At(time, () =>
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(0.3f, 1.4f);
					UltimateFxStage.Emit(motes, run.Caster + offset + Vector3.up * 0.2f, Vector3.up * Random.Range(3f, 6f) - offset * 0.5f, Random.Range(0.1f, 0.2f), Random.Range(0.6f, 1f), emberColor);
				});
			}
		}
	}
}
