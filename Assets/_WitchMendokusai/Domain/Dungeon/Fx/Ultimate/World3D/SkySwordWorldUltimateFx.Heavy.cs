using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 하늘 검 3D 판의 강화 층. 강한 검 에셋 (Ult3D_02_SkySwordHeavy) 만 켬, 약한 검 에셋 (Ult3D_01_SkySword) 은 꺼짐
	/// - 검, 광원, 바닥 반응은 같은 부품을 값만 바꿔 씀. 여기는 강한 쪽에만 더하는 구성
	/// - 하늘에서 표적까지 잇는 빛 기둥 (원기둥 메시, 체적 셰이더): 떠 있는 동안 가는 예고 기둥, 낙하 때 굵어짐, 접지 뒤 가늘어져 남다가 사라짐
	/// - 기둥 안을 타고 내려오는 빛 줄, 접지 때 위로 솟는 빛 가시, 금빛 고리 둘과 굴절 충격파
	/// - 수치 근거: memo wm/design/ult3d-heavy-20261120 (기둥 세로 0.8~0.95 화면 높이, 굵은 기둥 반치폭 약 330ms, 가는 기둥 잔류 약 400ms)
	/// </summary>
	public sealed partial class SkySwordWorldUltimateFx
	{
		/// <summary> 기둥 굵기, 검, 고리, 충격파, 바위, 먼지, 그을림을 같은 배율로. 기둥 높이는 이미 화면 위로 넘어가 그대로 </summary>
		public override void ScaleWorld(float scale)
		{
			beamFullRadius *= scale;
			crownSpikeRadius *= scale;
			goldRingRadius *= scale;
			shockRadius *= scale;
			swordLength *= scale;
			hangHeight *= scale;
			upheavalRadius *= scale;
			upheavalScale *= scale;
			dustRadius *= scale;
			decalSize *= scale;
			windReach *= scale;
			impactLightHeight *= scale;
			crownSpikeCount = Mathf.RoundToInt(crownSpikeCount * scale);
			rayCount = Mathf.RoundToInt(rayCount * scale);
			dustCount = Mathf.RoundToInt(dustCount * scale);
			upheavalCount = Mathf.RoundToInt(upheavalCount * scale);
			debrisCount = Mathf.RoundToInt(debrisCount * scale);
		}

		private static readonly int BEAM_COLOR_ID = Shader.PropertyToID("_Color");
		private static readonly int BEAM_CORE_COLOR_ID = Shader.PropertyToID("_CoreColor");
		private static readonly int BEAM_INTENSITY_ID = Shader.PropertyToID("_Intensity");

		[Header("강화 층 (강한 검만)")]
		[SerializeField, Tooltip("켜면 빛 기둥, 빛 가시, 금빛 고리, 충격파를 더함")] private bool heavyLayers;
		[SerializeField] private Shader beamShader;
		[SerializeField, ColorUsage(false, true)] private Color beamColor = new(3f, 2.1f, 0.7f);
		[SerializeField, ColorUsage(false, true), Tooltip("기둥 가운데 흰 심")] private Color beamCoreColor = new(4f, 3.6f, 2.6f);
		[SerializeField, Min(1f), Tooltip("지면에서 기둥 끝까지. 화면 위로 넘어가게")] private float beamHeight = 40f;
		[SerializeField, Min(0.01f), Tooltip("떠 있는 동안 예고 기둥 반지름")] private float beamTelegraphRadius = 0.18f;
		[SerializeField, Range(0f, 1f), Tooltip("예고 기둥 밝기 비율")] private float beamTelegraphIntensity = 0.45f;
		[SerializeField, Min(0.05f), Tooltip("낙하와 접지 때 굵은 기둥 반지름")] private float beamFullRadius = 1.3f;
		[SerializeField, Min(0.01f), Tooltip("접지 뒤 남는 가는 기둥 반지름")] private float beamThinRadius = 0.14f;
		[SerializeField, Min(0f), Tooltip("접지 뒤 굵은 채로 있는 시간. 실측 반치폭 약 330ms")] private float beamThickSeconds = 0.25f;
		[SerializeField, Min(0.01f)] private float beamShrinkSeconds = 0.2f;
		[SerializeField, Min(0f), Tooltip("가는 기둥이 남는 시간. 실측 약 400ms")] private float beamLingerSeconds = 0.45f;
		[SerializeField, Min(0.01f)] private float beamFadeSeconds = 0.35f;
		[SerializeField, Min(1f), Tooltip("접지 순간 기둥 밝기 배율")] private float beamImpactBoost = 1.6f;
		[SerializeField, Min(0), Tooltip("굵은 기둥 동안 기둥을 타고 내려오는 빛 줄 수")] private int beamStreakCount = 70;
		[SerializeField, Min(0), Tooltip("접지 때 위로 솟는 빛 가시 수")] private int crownSpikeCount = 46;
		[SerializeField] private Vector2 crownSpikeSpeed = new(10f, 24f);
		[SerializeField] private Vector2 crownSpikeRadius = new(0.3f, 1.8f);
		[SerializeField, ColorUsage(true, true)] private Color crownSpikeColor = new(3f, 2.2f, 0.8f, 1f);
		[SerializeField, Min(0.5f), Tooltip("금빛 고리 최대 반지름")] private float goldRingRadius = 8f;
		[SerializeField, Min(0.05f)] private float goldRingSeconds = 0.55f;
		[SerializeField, Min(0f), Tooltip("둘째 고리가 늦게 나가는 시간")] private float goldRingSecondDelay = 0.12f;
		[SerializeField] private Color goldRingColor = new(1f, 0.78f, 0.3f, 1f);
		[SerializeField, Min(0.5f)] private float shockRadius = 11f;
		[SerializeField, Min(0.05f)] private float shockSeconds = 0.5f;
		[SerializeField, Range(0f, 0.2f)] private float shockDistortion = 0.035f;
		[SerializeField, Range(0.01f, 0.5f)] private float shockBandWidth = 0.12f;
		[SerializeField, Tooltip("그을림을 바닥 판 대신 URP DecalProjector 로 투영 (지형 굴곡을 따름)")] private bool projectorDecal;
		[SerializeField, Tooltip("Shader Graphs/Decal")] private Shader decalShader;
		[SerializeField, Min(0.5f), Tooltip("투영 깊이. 위아래로 이만큼 안의 바닥에 그림")] private float decalDepth = 4f;

		private float BeamThinStart => ImpactTime + beamThickSeconds;
		private float BeamFadeStart => BeamThinStart + beamShrinkSeconds + beamLingerSeconds;

		private void ComposeHeavy(UltimateFxRun run, Vector3 target)
		{
			if (beamShader == null)
				throw new System.InvalidOperationException($"{name}: 빛 기둥 셰이더 미연결. 메뉴 WM/CombatFx/Build Ultimate 3D Presets");
			ComposeBeam(run, target);
			BeamStreaks(run, target);
			run.At(ImpactTime, () => CrownSpikes(run, target));
			run.ThinRing(ImpactTime, target, 0.6f, goldRingRadius, goldRingSeconds, goldRingColor, 5);
			run.ThinRing(ImpactTime + goldRingSecondDelay, target, 0.4f, goldRingRadius * 0.7f, goldRingSeconds, goldRingColor, 5);
			run.Shockwave(ImpactTime, target, shockRadius, shockSeconds, shockDistortion, shockBandWidth, goldRingColor, false);
		}

		// 하늘에서 표적까지 잇는 원기둥. 반지름과 밝기만 시간에 따라 바뀜
		private void ComposeBeam(UltimateFxRun run, Vector3 target)
		{
			Material material = new(beamShader) { name = "UltimateFx_Beam" };
			material.SetColor(BEAM_COLOR_ID, beamColor);
			material.SetColor(BEAM_CORE_COLOR_ID, beamCoreColor);
			material.SetFloat(BEAM_INTENSITY_ID, 0f);
			run.OnRelease(() => Object.Destroy(material));

			GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
			beam.name = "SkyBeam";
			Object.Destroy(beam.GetComponent<Collider>());
			beam.transform.SetParent(run.Root, false);
			MeshRenderer beamRenderer = beam.GetComponent<MeshRenderer>();
			beamRenderer.sharedMaterial = material;
			beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			beamRenderer.receiveShadows = false;
			// 원기둥 기본 메시는 높이 2, 반지름 0.5, 가운데 원점
			beam.transform.position = target + Vector3.up * beamHeight * 0.5f;
			beam.SetActive(false);

			void SetBeam(float radius, float intensity)
			{
				beam.transform.localScale = new Vector3(radius * 2f, beamHeight * 0.5f, radius * 2f);
				material.SetFloat(BEAM_INTENSITY_ID, intensity);
			}

			run.At(DescendEnd, () => beam.SetActive(true));
			// 예고: 검이 떠 있는 동안 가는 기둥이 표적을 찍고 점점 밝아짐
			run.Track(DescendEnd, FallStart - DescendEnd, progress => SetBeam(beamTelegraphRadius, beamTelegraphIntensity * UltimateFxRun.EaseOut(progress)));
			// 낙하: 검과 함께 굵어짐
			run.Track(FallStart, fallSeconds, progress =>
			{
				float ease = UltimateFxRun.EaseIn(progress);
				SetBeam(Mathf.Lerp(beamTelegraphRadius, beamFullRadius, ease), Mathf.Lerp(beamTelegraphIntensity, 1f, ease));
			});
			// 접지: 순간 밝아졌다 굵은 채 유지
			run.Track(ImpactTime, beamThickSeconds, progress => SetBeam(beamFullRadius * (1f + 0.15f * (1f - progress)), Mathf.Lerp(beamImpactBoost, 1f, UltimateFxRun.EaseOut(progress))));
			run.Track(BeamThinStart, beamShrinkSeconds, progress =>
			{
				float ease = UltimateFxRun.EaseOut(progress);
				SetBeam(Mathf.Lerp(beamFullRadius, beamThinRadius, ease), Mathf.Lerp(1f, 0.8f, ease));
			});
			run.Track(BeamFadeStart, beamFadeSeconds, progress => SetBeam(beamThinRadius * (1f - 0.5f * progress), 0.8f * (1f - UltimateFxRun.EaseIn(progress))), () => run.Destroy(beam));
		}

		// 그을림과 균열을 위에서 아래로 투영. 투영기는 앞 (+Z) 방향으로 투영하므로 아래를 보게 90도 눕힘
		// - 2026-11-20 MOIS2 실측: 같은 장면에서 아래로 눕힌 투영기는 그려짐. 옛 기록의 "그려지지 않음" 은 방향 문제로 추정
		private void ProjectorDecal(UltimateFxRun run, Vector3 target, Texture2D texture)
		{
			if (decalShader == null)
				throw new System.InvalidOperationException($"{name}: 데칼 셰이더 미연결. 메뉴 WM/CombatFx/Build Ultimate 3D Presets");
			Material material = new(decalShader) { name = "UltimateFx_ScorchDecal" };
			material.SetTexture("Base_Map", texture);
			run.OnRelease(() => Object.Destroy(material));
			GameObject decalObject = new("ScorchDecal");
			decalObject.transform.SetParent(run.Root, false);
			decalObject.transform.SetPositionAndRotation(target + Vector3.up * decalDepth * 0.5f, Quaternion.Euler(90f, Random.value * 360f, 0f));
			UnityEngine.Rendering.Universal.DecalProjector projector = decalObject.AddComponent<UnityEngine.Rendering.Universal.DecalProjector>();
			projector.material = material;
			projector.size = new Vector3(decalSize, decalSize, decalDepth);
			projector.fadeFactor = 0f;
			run.Track(ImpactTime, 0.08f, progress => projector.fadeFactor = progress);
			run.Track(VanishStart + vanishSeconds, scorchLingerSeconds, progress => projector.fadeFactor = 1f - UltimateFxRun.EaseIn(progress), () => run.Destroy(decalObject));
		}

		// 굵은 기둥 동안 기둥 안을 타고 내려오는 늘어난 빛 줄
		private void BeamStreaks(UltimateFxRun run, Vector3 target)
		{
			if (beamStreakCount <= 0)
				return;
			ParticleSystem streaks = run.Particles("BeamStreaks", run.Additive(UltimateFxTextures.Spark, Color.white), beamStreakCount + 8, 0f, true);
			UltimateFxStage.SetLifetimeLook(streaks, UltimateFxStage.FadeGradient(Color.white, 0.05f, 0.5f), AnimationCurve.Linear(0f, 1f, 1f, 0.5f));
			ParticleSystemRenderer streakRenderer = streaks.GetComponent<ParticleSystemRenderer>();
			streakRenderer.velocityScale = 0.06f;
			streakRenderer.lengthScale = 1f;
			float window = fallSeconds + beamThickSeconds;
			for (int i = 0; i < beamStreakCount; i++)
			{
				float time = FallStart + window * i / Mathf.Max(1, beamStreakCount);
				run.At(time, () =>
				{
					Vector3 offset = UltimateFxStage.RandomRingPoint(0f, beamFullRadius * 0.8f);
					Vector3 position = target + offset + Vector3.up * Random.Range(4f, beamHeight * 0.4f);
					UltimateFxStage.Emit(streaks, position, Vector3.down * Random.Range(40f, 70f), Random.Range(0.12f, 0.24f), Random.Range(0.15f, 0.3f), crownSpikeColor);
				});
			}
		}

		// 접지 때 표적 둘레에서 위로 솟는 빛 가시
		private void CrownSpikes(UltimateFxRun run, Vector3 target)
		{
			if (crownSpikeCount <= 0)
				return;
			ParticleSystem spikes = run.Particles("CrownSpikes", run.Additive(UltimateFxTextures.Spark, Color.white), crownSpikeCount + 8, 1.5f, true);
			UltimateFxStage.SetLifetimeLook(spikes, UltimateFxStage.FadeGradient(Color.white, 0.02f, 0.35f), AnimationCurve.Linear(0f, 1f, 1f, 0.6f));
			ParticleSystemRenderer spikeRenderer = spikes.GetComponent<ParticleSystemRenderer>();
			spikeRenderer.velocityScale = 0.09f;
			spikeRenderer.lengthScale = 1.2f;
			for (int i = 0; i < crownSpikeCount; i++)
			{
				Vector3 offset = UltimateFxStage.RandomRingPoint(crownSpikeRadius.x, crownSpikeRadius.y);
				Vector3 velocity = Vector3.up * Random.Range(crownSpikeSpeed.x, crownSpikeSpeed.y) + offset * 1.2f;
				UltimateFxStage.Emit(spikes, target + offset + Vector3.up * 0.2f, velocity, Random.Range(0.18f, 0.34f), Random.Range(0.25f, 0.45f), crownSpikeColor);
			}
		}
	}
}
