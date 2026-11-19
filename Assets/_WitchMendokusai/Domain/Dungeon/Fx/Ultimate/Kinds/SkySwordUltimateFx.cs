using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 하늘 검 궁극기. 검을 들어 십자 반짝 -> 밤으로 물들며 하늘에서 표적으로 가는 유도선 -> 흰 섬광 한 장
	/// -> 검은 바탕 흰 실루엣 한 장 -> 후광 고리와 함께 거대한 검이 꽂힘 -> 흰 연기 띠가 바닥을 굴러감 -> 검은 꽂힌 채 남음
	/// - 실루엣: 세로 십자 (검) 와 그 뒤 원 (후광). 배경 장악: 남색 밤 물들임과 옅은 별, 정점 한 장은 흑백
	/// - 시간 구조는 참고 영상 15fps 실측 (들기 0.4초, 밤 물들임 2초, 백색 1장, 흑백 1장, 꽂힘 0.33초, 연기 2초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_05_SkySword", menuName = "WM/CombatFx/Ultimate/" + nameof(SkySwordUltimateFx))]
	public sealed class SkySwordUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField] private Color nightTint = new(0.3f, 0.36f, 0.75f, 1f);
		[SerializeField] private Color nightTop = new(0.03f, 0.04f, 0.12f, 1f);
		[SerializeField] private Color nightBottom = new(0.08f, 0.1f, 0.25f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color glint = new(2f, 2f, 2.2f);
		[SerializeField, ColorUsage(false, true)] private Color halo = new(1.6f, 1.5f, 1.2f);
		[SerializeField] private Color smoke = new(0.95f, 0.96f, 1f, 0.8f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("실측 0.4초")] private float raiseSeconds = 0.4f;
		[SerializeField, Min(0.05f), Tooltip("십자 반짝")] private float glintSeconds = 0.27f;
		[SerializeField, Min(0.1f), Tooltip("실측 2초")] private float nightSeconds = 2f;
		[SerializeField, Min(0.01f), Tooltip("실측 1장")] private float whiteSeconds = 0.067f;
		[SerializeField, Min(0.01f), Tooltip("실측 1장")] private float silhouetteSeconds = 0.067f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.33초")] private float plantSeconds = 0.33f;
		[SerializeField, Min(0.1f), Tooltip("실측 2초")] private float smokeSeconds = 2f;
		[SerializeField, Min(0f), Tooltip("꽂힌 검이 남아 있는 시간")] private float remainSeconds = 1f;
		[SerializeField, Min(0.1f)] private float vanishSeconds = 0.5f;

		[Header("크기와 배치 (월드 단위)")]
		[SerializeField, Min(2f), Tooltip("거대한 검 높이. 캐릭터 키의 약 8배")] private float swordHeight = 7.5f;
		[SerializeField, Min(0f), Tooltip("표적까지 화면 위쪽 (카메라가 보는 쪽) 거리")] private float targetDistance = 0.6f;
		[SerializeField, Min(0f), Tooltip("검이 땅에 박히는 깊이 (검 높이 비율)")] private float sinkRatio = 0f;
		[SerializeField, Min(1f)] private float haloSize = 9f;
		[SerializeField, Min(1f)] private float impactRadius = 9f;
		[SerializeField, Min(0)] private int smokeCount = 70;
		[SerializeField, Min(0f)] private float smokeSpeed = 7f;
		[SerializeField] private int crackSeed = 23;
		[SerializeField, Min(0.1f)] private float crackHeatSeconds = 2.4f;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float tintAmount = 0.45f;
		[SerializeField, Range(0f, 1f)] private float darken = 0.55f;
		[SerializeField, Range(0f, 1f), Tooltip("밤 동안 옅게 비치는 별 하늘")] private float starAmount = 0f;
		[SerializeField, Range(0f, 1f)] private float flashPeak = 1f;
		[SerializeField, Min(0f)] private float plantShake = 2f;
		[SerializeField, Range(0.8f, 1.5f), Tooltip("밤 동안 카메라가 물러나 검 전체가 화면에 들어오게")] private float nightZoom = 1.22f;

		private float GlintTime => raiseSeconds;
		private float NightStart => GlintTime + glintSeconds;
		private float WhiteTime => NightStart + nightSeconds;
		private float SilhouetteTime => WhiteTime + whiteSeconds;
		private float PlantTime => SilhouetteTime + silhouetteSeconds;
		private float SmokeStart => PlantTime + plantSeconds;
		private float VanishStart => SmokeStart + smokeSeconds + remainSeconds;

		public override float DurationSeconds => VanishStart + vanishSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 target = run.Center + run.ScreenForward * targetDistance;
			target.y = UltimateFxStage.GroundHeight(target);
			Vector3 hand = run.Caster + Vector3.up * 1.4f;

			// 들기: 시전자 머리 위로 작은 검
			Material smallSword = run.Alpha(UltimateFxTextures.Sword, Color.white);
			GameObject raised = run.Billboard("RaisedSword", smallSword, hand, 0.45f, 1.8f, true, true, 5);
			run.Track(0f, raiseSeconds, progress => raised.transform.position = hand + Vector3.up * UltimateFxRun.EaseOut(progress) * 0.8f);

			// 칼끝 반짝: 둥근 빛 한 번 (십자광은 별자리 궁극기의 정점이라 쓰지 않음)
			Material glintMaterial = run.Additive(UltimateFxTextures.Soft, Color.clear);
			GameObject glintBoard = run.Billboard("Glint", glintMaterial, hand + Vector3.up * 2.4f, 4f, 4f, false, true, 9);
			run.Track(GlintTime, glintSeconds * 2f, progress =>
			{
				float pulse = UltimateFxRun.Bell(progress, 0.15f);
				glintBoard.transform.localScale = Vector3.one * Mathf.Lerp(2f, 5f, pulse);
				glintMaterial.SetColor("_Color", new Color(glint.r, glint.g, glint.b, pulse));
			}, () => run.Destroy(glintBoard));

			// 밤 물들임과 유도선. 카메라가 물러나 검이 다 들어올 자리를 만듦
			run.Grade.SetSpaceColors(nightTop, nightBottom, new Color(0.6f, 0.65f, 1f, 0.2f));
			run.At(NightStart, () =>
			{
				System.Action releaseZoom = run.Zoom(nightZoom, 5f, nightSeconds * 0.8f, float.PositiveInfinity, 1.2f);
				run.At(VanishStart, () => releaseZoom());
			});
			run.Track(NightStart, nightSeconds, progress =>
			{
				float amount = UltimateFxRun.EaseInOut(progress * 1.3f);
				run.Grade.SetTint(tintAmount * amount, nightTint);
				run.Grade.Darken = darken * amount;
				run.Grade.Space = starAmount * amount;
			});
			Material guideMaterial = run.Additive(UltimateFxTextures.Line, Color.clear);
			GameObject guide = run.Billboard("Guide", guideMaterial, target, 0.25f, 40f, true, true, 8);
			run.Track(NightStart + nightSeconds * 0.4f, nightSeconds * 0.6f, progress =>
			{
				float flicker = 0.75f + 0.25f * Mathf.Sin(run.Now * 30f);
				guideMaterial.SetColor("_Color", new Color(glint.r, glint.g, glint.b, UltimateFxRun.EaseOut(progress * 3f) * flicker));
			}, () => run.Destroy(guide));

			// 흰 섬광 한 장, 이어서 검은 바탕 흰 실루엣 한 장
			run.Track(WhiteTime, whiteSeconds, _ => run.Flash(flashPeak, Color.white), () => run.Flash(0f, Color.white));
			run.Grade.SetToneColors(Color.black, Color.white, 0.95f);
			Material swordMaterial = run.Alpha(UltimateFxTextures.Sword, Color.white);
			float swordWidth = swordHeight * UltimateFxTextures.Sword.width / UltimateFxTextures.Sword.height;
			Vector3 swordFoot = target - Vector3.up * swordHeight * sinkRatio;
			GameObject sword = null;
			run.Track(SilhouetteTime, silhouetteSeconds, _ =>
			{
				run.Grade.TwoTone = 1f;
				run.Grade.Darken = 0f;
				if (sword == null)
				{
					sword = run.Billboard("SkySword", swordMaterial, swordFoot, swordWidth, swordHeight, true, true, 10);
					sword.GetComponent<UltimateBillboard>().Upright = false;
					run.Destroy(raised);
				}
				swordMaterial.SetColor("_Color", new Color(4f, 4f, 4f, 1f));
			}, () =>
			{
				run.Grade.TwoTone = 0f;
				swordMaterial.SetColor("_Color", Color.white);
			});

			// 꽂힘: 검이 위에서 박히고 후광 고리가 퍼짐
			Material haloMaterial = run.Additive(UltimateFxTextures.ThinRing, Color.clear);
			GameObject haloBoard = null;
			run.Track(PlantTime, plantSeconds + smokeSeconds, progress =>
			{
				float seconds = progress * (plantSeconds + smokeSeconds);
				float drop = 1f - UltimateFxRun.EaseOut(seconds / plantSeconds);
				sword.transform.position = swordFoot + Vector3.up * drop * swordHeight * 0.6f;
				if (haloBoard == null)
					haloBoard = run.Billboard("Halo", haloMaterial, swordFoot + Vector3.up * swordHeight * 0.72f, 1f, 1f, false, true, 9);
				float grow = UltimateFxRun.EaseOut(seconds / (plantSeconds * 3f));
				haloBoard.transform.localScale = Vector3.one * Mathf.Lerp(haloSize * 0.3f, haloSize, grow);
				float fade = 1f - UltimateFxRun.Span(seconds, plantSeconds, plantSeconds + smokeSeconds);
				haloMaterial.SetColor("_Color", new Color(halo.r, halo.g, halo.b, fade));
				float night = 1f - UltimateFxRun.Span(seconds, plantSeconds * 2f, plantSeconds + smokeSeconds);
				run.Grade.SetTint(tintAmount * night, nightTint);
				run.Grade.Darken = darken * night * 0.6f;
				run.Grade.Space = starAmount * night;
			}, () =>
			{
				run.Destroy(haloBoard);
				run.Grade.SetTint(0f, nightTint);
				run.Grade.Darken = 0f;
				run.Grade.Space = 0f;
			});
			run.At(PlantTime, () =>
			{
				run.ShakeSeries(PlantTime, 6, 0.07f, plantShake, 0.72f);
				run.Debris(target, 2f, 36, 13f, new Color(0.6f, 0.58f, 0.55f), halo, 0.2f);
			});
			run.At(PlantTime, () => run.React(target, 12f, impactRadius));
			run.Crack(PlantTime, target, impactRadius * 0.55f, crackSeed, new Color(1.6f, 1.6f, 1.9f, 1f), new Color(0.6f, 0.7f, 1.4f, 0.8f), 0.18f, crackHeatSeconds);

			// 흰 연기 띠: 바닥을 따라 바깥으로 굴러감
			run.At(SmokeStart - plantSeconds * 0.5f, () => EmitSmoke(run, target));

			// 남은 검이 점 무늬로 녹아 사라짐
			run.Track(VanishStart, vanishSeconds, progress => swordMaterial.SetFloat("_Dither", progress), () => run.Destroy(sword));
		}

		private void EmitSmoke(UltimateFxRun run, Vector3 target)
		{
			ParticleSystem cloud = run.Particles("SmokeBand", run.Alpha(UltimateFxTextures.Smoke, Color.white), smokeCount + 8, 0f);
			UltimateFxStage.SetLifetimeLook(cloud, UltimateFxStage.FadeGradient(Color.white, 0.08f, 0.55f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
			ParticleSystem.LimitVelocityOverLifetimeModule drag = cloud.limitVelocityOverLifetime;
			drag.enabled = true;
			drag.limit = 1f;
			drag.dampen = 0.06f;
			for (int i = 0; i < smokeCount; i++)
			{
				float angle = Random.value * Mathf.PI * 2f;
				Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
				Vector3 position = target + direction * Random.Range(1.6f, 2.6f) + Vector3.up * Random.Range(0.1f, 0.4f);
				UltimateFxStage.Emit(cloud, position, direction * smokeSpeed * Random.Range(0.7f, 1.2f) + Vector3.up * Random.Range(0f, 0.6f), Random.Range(1.8f, 3.2f), Random.Range(smokeSeconds * 0.7f, smokeSeconds), smoke, Random.value * 360f, Random.Range(-60f, 60f));
			}
		}
	}
}
