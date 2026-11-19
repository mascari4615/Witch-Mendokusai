using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 밤의 날 궁극기. 낮을 베어 밤으로, 다시 낮으로. 은하 무늬 부채꼴 날이 주제 물체
	/// - 짧은 부채꼴 휘두름 -> 세상 탈색 -> 화면을 가르는 검은 사선 -> 사선이 열리며 밤 하늘이 와이프로 드러남 -> 밤 유지
	///   -> 별빛이 날로 빨려듦 -> 날이 흰 빛으로 크게 휘둘림 -> 사선 와이프로 낮 복귀
	/// - 실루엣: 부채꼴 창 (안에 다른 세계) 과 화면 사선. 배경 장악: 탈색 뒤 사선 와이프로 별 하늘 교체
	/// - 시간 구조는 참고 영상 20fps 실측 (탈색 0.2초, 검은 사선 0.4초, 와이프 0.4초, 흡수 0.8초, 흰 날 1.6초, 낮 복귀 0.4초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_08_NightBlade", menuName = "WM/CombatFx/Ultimate/" + nameof(NightBladeUltimateFx))]
	public sealed class NightBladeUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField] private Color nightTop = new(0.02f, 0.02f, 0.1f, 1f);
		[SerializeField] private Color nightBottom = new(0.14f, 0.05f, 0.24f, 1f);
		[SerializeField] private Color milkyWay = new(0.55f, 0.35f, 0.95f, 0.9f);
		[SerializeField] private Color wipeEdge = new(1f, 0.55f, 0.75f, 1f);
		[SerializeField] private Color slashInk = new(0.02f, 0.02f, 0.04f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color starlight = new(1.2f, 1.3f, 2f);
		[SerializeField, ColorUsage(false, true)] private Color whiteBlade = new(2.4f, 2.4f, 2.6f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("처음 짧은 휘두름")] private float swingSeconds = 0.4f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.2초")] private float desaturateSeconds = 0.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.4초")] private float slashSeconds = 0.4f;
		[SerializeField, Min(0f)] private float slashHoldSeconds = 0.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.4초")] private float wipeSeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.2초")] private float nightSeconds = 1.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.8초")] private float absorbSeconds = 0.8f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.6초")] private float whiteSeconds = 1.6f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.4초")] private float returnSeconds = 0.4f;

		[Header("크기와 배치")]
		[SerializeField, Min(1f), Tooltip("처음 휘두름 날 반지름")] private float smallBladeSize = 3.2f;
		[SerializeField, Min(2f), Tooltip("흰 날 반지름. 캐릭터 키의 약 9배")] private float bigBladeSize = 7f;
		[SerializeField, Range(-80f, 80f), Tooltip("화면 사선 각 (도, 화면 가로 기준)")] private float slashAngle = -62f;
		[SerializeField, Range(0f, 0.2f)] private float slashThickness = 0.03f;
		[SerializeField, Min(0)] private int starCount = 320;
		[SerializeField, Min(1f)] private float starRadius = 14f;
		[SerializeField] private Vector2 swingRoll = new(70f, -70f);
		[SerializeField, Min(1f)] private float impactRadius = 9f;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float desaturate = 1f;
		[SerializeField, Range(0f, 1f)] private float flashPeak = 0.55f;
		[SerializeField, Min(0f)] private float swingShake = 1.2f;

		private float DesaturateStart => swingSeconds + 0.1f;
		private float SlashStart => DesaturateStart + desaturateSeconds;
		private float WipeStart => SlashStart + slashSeconds + slashHoldSeconds;
		private float NightStart => WipeStart + wipeSeconds;
		private float AbsorbStart => NightStart + nightSeconds;
		private float WhiteStart => AbsorbStart + absorbSeconds;
		private float ReturnStart => WhiteStart + whiteSeconds;

		public override float DurationSeconds => ReturnStart + returnSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 pivot = run.Caster + Vector3.up * 0.6f;
			float wipeRadians = (slashAngle + 90f) * Mathf.Deg2Rad;
			float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1.7778f;
			float wipeReach = Mathf.Sqrt(aspect * aspect + 1f) * 0.5f + 0.05f;

			// 부채꼴 날: 꼭지가 시전자에 있는 판. 처음엔 작게 휘두르고 밤 동안 크게 들림
			Material fanMaterial = run.Alpha(UltimateFxTextures.NightFan, Color.white);
			float fanAspect = UltimateFxTextures.NightFan.width / (float)UltimateFxTextures.NightFan.height;
			GameObject fan = run.Billboard("NightFan", fanMaterial, pivot, smallBladeSize * fanAspect, smallBladeSize, true, true, 6);
			UltimateBillboard fanFacing = fan.GetComponent<UltimateBillboard>();
			fanFacing.Upright = false;
			Material fanGlow = run.Additive(UltimateFxTextures.NightFan, Color.clear);
			GameObject glow = run.Billboard("NightFanGlow", fanGlow, pivot, smallBladeSize * fanAspect, smallBladeSize, true, true, 7);
			UltimateBillboard glowFacing = glow.GetComponent<UltimateBillboard>();
			glowFacing.Upright = false;
			run.Track(0f, swingSeconds, progress =>
			{
				float roll = Mathf.Lerp(swingRoll.x, swingRoll.y, UltimateFxRun.EaseOut(progress));
				fanFacing.Roll = glowFacing.Roll = roll;
				fanMaterial.SetColor("_Color", new Color(1f, 1f, 1f, UltimateFxRun.Bell(progress, 0.2f)));
			}, () => fanMaterial.SetColor("_Color", Color.clear));

			// 탈색
			run.Track(DesaturateStart, desaturateSeconds, progress => run.Grade.Desaturate = desaturate * progress);

			// 검은 사선: 화면을 가르는 가는 선이 굵어짐
			run.Track(SlashStart, slashSeconds + slashHoldSeconds + wipeSeconds * 0.5f, progress =>
			{
				float seconds = progress * (slashSeconds + slashHoldSeconds + wipeSeconds * 0.5f);
				float grow = UltimateFxRun.EaseOut(seconds / slashSeconds);
				float fade = 1f - UltimateFxRun.Span(seconds, slashSeconds + slashHoldSeconds, slashSeconds + slashHoldSeconds + wipeSeconds * 0.5f);
				run.Layer.SetBand(0, fade, wipeEdge, slashAngle, 0f, slashThickness * grow * 2.2f, true);
				run.Layer.SetBand(1, fade, slashInk, slashAngle, 0f, slashThickness * grow, false);
			}, () =>
			{
				run.Layer.SetBand(0, 0f, slashInk, slashAngle, 0f, 0f, true);
				run.Layer.SetBand(1, 0f, slashInk, slashAngle, 0f, 0f, false);
			});

			// 와이프: 사선이 열리며 그 자리부터 밤 하늘이 화면을 쓸고 지나감
			run.Grade.SetSpaceColors(nightTop, nightBottom, milkyWay);
			run.Track(WipeStart, wipeSeconds, progress =>
			{
				run.Grade.Space = 1f;
				run.Grade.SetWipe(true, wipeRadians, Mathf.Lerp(0f, wipeReach, UltimateFxRun.EaseInOut(progress)), wipeEdge);
			}, () => run.Grade.SetWipe(true, wipeRadians, wipeReach, Color.clear));

			// 밤: 날이 크게 들림
			run.Track(NightStart, nightSeconds, progress =>
			{
				float size = Mathf.Lerp(smallBladeSize, bigBladeSize, UltimateFxRun.EaseOut(progress));
				fan.transform.localScale = glow.transform.localScale = new Vector3(size * fanAspect, size, 1f);
				fanFacing.Roll = glowFacing.Roll = swingRoll.x;
				fanMaterial.SetColor("_Color", new Color(1f, 1f, 1f, UltimateFxRun.EaseOut(progress * 2f)));
			});

			// 별빛 흡수: 화면 곳곳의 별이 꼬리를 끌며 날로 모임
			run.At(AbsorbStart, () => EmitStars(run, pivot));
			run.Track(AbsorbStart, absorbSeconds, progress => fanGlow.SetColor("_Color", new Color(starlight.r, starlight.g, starlight.b, progress * 0.6f)));

			// 흰 날: 날 전체가 흰 빛, 크게 한 번 휘둘러 내림
			run.Track(WhiteStart, whiteSeconds, progress =>
			{
				float swing = UltimateFxRun.EaseOut(UltimateFxRun.Span(progress, 0.05f, 0.25f));
				fanFacing.Roll = glowFacing.Roll = Mathf.Lerp(swingRoll.x, swingRoll.y, swing);
				float white = UltimateFxRun.EaseOut(progress * 6f);
				float fade = 1f - UltimateFxRun.Span(progress, 0.65f, 1f);
				fanGlow.SetColor("_Color", new Color(whiteBlade.r, whiteBlade.g, whiteBlade.b, white * fade));
				fanMaterial.SetColor("_Color", new Color(1f, 1f, 1f, fade));
			}, () =>
			{
				run.Destroy(fan);
				run.Destroy(glow);
			});
			float hitTime = WhiteStart + whiteSeconds * 0.15f;
			run.Track(hitTime, 0.35f, progress => run.Flash(flashPeak * Mathf.Pow(1f - progress, 2f), Color.white), () => run.Flash(0f, Color.white));
			run.At(hitTime, () =>
			{
				run.ShakeSeries(hitTime, 5, 0.07f, swingShake, 0.75f);
				run.React(run.Caster, 14f, impactRadius, false);
			});

			// 낮 복귀: 같은 사선 와이프를 되돌림
			run.Track(ReturnStart, returnSeconds, progress =>
			{
				run.Grade.SetWipe(true, wipeRadians, Mathf.Lerp(wipeReach, 0f, UltimateFxRun.EaseInOut(progress)), Color.Lerp(wipeEdge, Color.clear, UltimateFxRun.Span(progress, 0.6f, 1f)));
				run.Grade.Desaturate = desaturate * (1f - progress);
			}, () =>
			{
				run.Grade.SetWipe(false, wipeRadians, 0f, Color.clear);
				run.Grade.Space = 0f;
				run.Grade.Desaturate = 0f;
			});
		}

		private void EmitStars(UltimateFxRun run, Vector3 pivot)
		{
			Material material = run.Additive(UltimateFxTextures.Spark, Color.white);
			ParticleSystem stars = run.Particles("Starlight", material, starCount + 8, 0f);
			ParticleSystem.TrailModule trails = stars.trails;
			trails.enabled = true;
			trails.ratio = 1f;
			trails.lifetime = 0.18f;
			trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.25f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
			stars.GetComponent<ParticleSystemRenderer>().trailMaterial = material;
			UltimateFxStage.SetLifetimeLook(stars, UltimateFxStage.FadeGradient(Color.white, 0.15f, 0.9f), null);
			for (int i = 0; i < starCount; i++)
			{
				Vector3 from = pivot + UltimateFxStage.RandomRingPoint(2f, starRadius) + Vector3.up * Random.Range(0f, 6f);
				float life = Random.Range(absorbSeconds * 0.4f, absorbSeconds);
				UltimateFxStage.Emit(stars, from, (pivot + Vector3.up * bigBladeSize * 0.4f - from) / life, Random.Range(0.12f, 0.3f), life, starlight);
			}
		}
	}
}
