using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화염방사 궁극기. 지속 화염 -> 모음 (카메라 당김, 빨려드는 불티, 화면 따뜻하게) -> 방출 고리 -> 화면 절반을 가로지르는 거대 화염
	/// -> 선화 반전 한 장 -> 방사선과 이중 고리 -> 연기 소용돌이와 잔불
	/// - 실루엣: 시전자에서 화면 옆으로 뻗는 긴 쐐기. 배경 장악: 주황 물들임, 반전 한 장은 배경을 흰 종이 위 윤곽선으로
	/// - 시간 구조는 참고 영상 15fps 실측 (모음 0.8초, 화염 1.5초, 반전 1장, 고리 0.4초, 연기 1.1초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_01_Flame", menuName = "WM/CombatFx/Ultimate/" + nameof(FlameUltimateFx))]
	public sealed class FlameUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color flameCore = new(2f, 1.8f, 1.2f);
		[SerializeField, ColorUsage(false, true)] private Color flameBody = new(1.8f, 0.75f, 0.12f);
		[SerializeField, ColorUsage(false, true)] private Color flameEdge = new(1.1f, 0.18f, 0.04f);
		[SerializeField] private Color smokeColor = new(0.42f, 0.4f, 0.38f, 0.4f);
		[SerializeField] private Color warmTint = new(1f, 0.55f, 0.25f, 1f);
		[SerializeField] private Color lineInk = new(0.35f, 0.25f, 0.15f, 1f);

		[Header("시간 (초)")]
		[SerializeField, Min(0f)] private float sustainSeconds = 1.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.8초")] private float gatherSeconds = 0.8f;
		[SerializeField, Min(0.01f), Tooltip("실측 2장")] private float releaseSeconds = 0.13f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.47초")] private float blastSeconds = 1.47f;
		[SerializeField, Min(0.01f), Tooltip("실측 1장 (67ms)")] private float invertSeconds = 0.067f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.4초")] private float ringSeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.1초")] private float smokeSeconds = 1.1f;

		[Header("크기 (월드 단위)")]
		[SerializeField, Min(1f), Tooltip("지속 화염 길이")] private float sustainLength = 4f;
		[SerializeField, Min(2f), Tooltip("거대 화염 길이. 캐릭터 키의 약 10배")] private float blastLength = 7.5f;
		[SerializeField, Min(0.5f), Tooltip("거대 화염 끝 굵기")] private float blastThickness = 3.2f;
		[SerializeField, Min(0.1f)] private float muzzleHeight = 0.8f;
		[SerializeField] private Vector2 blastPuffSize = new(2.2f, 4.2f);

		[Header("양")]
		[SerializeField, Min(1)] private int sustainPuffsPerSecond = 60;
		[SerializeField, Min(1)] private int blastPuffsPerSecond = 320;
		[SerializeField, Min(0)] private int gatherSparksPerSecond = 160;
		[SerializeField, Min(0)] private int smokeCount = 46;
		[SerializeField, Min(0)] private int emberCount = 120;

		[Header("화면과 카메라")]
		[SerializeField, Range(0.5f, 1f), Tooltip("모음 때 카메라 거리 배율. 실측 약 1.2배 확대")] private float gatherZoom = 0.83f;
		[SerializeField, Range(0f, 1f)] private float gatherTint = 0.3f;
		[SerializeField, Range(0f, 1f)] private float blastTint = 0.5f;
		[SerializeField, Range(0f, 1f)] private float streakAlpha = 0.9f;
		[SerializeField, Min(0f)] private float blastShake = 0.5f;
		[SerializeField, Min(0f)] private float lightIntensity = 30f;
		[SerializeField, Min(1f)] private float lightRange = 20f;

		private float GatherStart => sustainSeconds;
		private float ReleaseStart => GatherStart + gatherSeconds;
		private float BlastStart => ReleaseStart + releaseSeconds;
		private float InvertStart => BlastStart + blastSeconds;
		private float RingStart => InvertStart + invertSeconds;
		private float SmokeStart => RingStart + ringSeconds;

		public override float DurationSeconds => SmokeStart + smokeSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 direction = run.ScreenRight;
			Vector3 muzzle = run.Caster + Vector3.up * muzzleHeight + direction * 0.6f;
			Vector3 head = run.Caster + direction * blastLength + Vector3.up * muzzleHeight;
			Gradient flameLife = FlameGradient();

			Material puffMaterial = run.Additive(UltimateFxTextures.FlamePuff, Color.white);
			ParticleSystem flames = run.Particles("Flames", puffMaterial, 2400, -0.25f);
			UltimateFxStage.SetLifetimeLook(flames, flameLife, AnimationCurve.EaseInOut(0f, 0.35f, 1f, 1.4f));
			ParticleSystem.NoiseModule noise = flames.noise;
			noise.enabled = true;
			noise.strength = 1.4f;
			noise.frequency = 0.6f;

			// 지속 화염: 짧고 가는 불줄기
			float sustainCarry = 0f;
			run.Track(0f, sustainSeconds + gatherSeconds * 0.5f, progress =>
			{
				sustainCarry += sustainPuffsPerSecond * run.Delta;
				while (sustainCarry >= 1f)
				{
					sustainCarry -= 1f;
					Vector3 spread = Random.insideUnitSphere * 0.15f;
					float speed = sustainLength / 0.5f * Random.Range(0.8f, 1.1f);
					UltimateFxStage.Emit(flames, muzzle, (direction + spread) * speed, Random.Range(0.5f, 0.9f), Random.Range(0.4f, 0.55f), Color.white, Random.value * 360f, Random.Range(-90f, 90f));
				}
			});

			// 모음: 카메라 당김, 빨려드는 불티, 화면이 따뜻해짐
			Material sparkMaterial = run.Additive(UltimateFxTextures.Spark, Color.white);
			ParticleSystem sparks = run.Particles("GatherSparks", sparkMaterial, 600, 0f, true);
			UltimateFxStage.SetLifetimeLook(sparks, UltimateFxStage.FadeGradient(Color.white, 0.1f, 0.7f), null);
			System.Action releaseZoom = delegate { };
			run.At(GatherStart, () => releaseZoom = run.Zoom(gatherZoom, -4f, gatherSeconds * 0.8f, float.PositiveInfinity, 0.6f));
			float sparkCarry = 0f;
			Light light = run.PointLight("FlameLight", flameBody, lightRange);
			run.Track(GatherStart, gatherSeconds, progress =>
			{
				run.Grade.SetTint(gatherTint * progress, warmTint);
				sparkCarry += gatherSparksPerSecond * run.Delta;
				while (sparkCarry >= 1f)
				{
					sparkCarry -= 1f;
					Vector3 from = muzzle + Random.onUnitSphere * Random.Range(3f, 6f);
					float life = Random.Range(0.25f, 0.45f);
					Color color = Color.Lerp(flameBody, flameCore, Random.value);
					UltimateFxStage.Emit(sparks, from, (muzzle - from) / life, Random.Range(0.15f, 0.35f), life, color);
				}
				light.transform.position = muzzle;
				light.intensity = lightIntensity * 0.4f * progress;
			});

			// 방출 고리: 총구에서 한 번 크게 번쩍이는 고리
			run.ThinRing(ReleaseStart, run.Caster, 0.5f, 3.5f, releaseSeconds * 3f, flameCore, 8);
			run.At(ReleaseStart, () => run.Flash(0.25f, flameCore));
			run.Track(ReleaseStart, releaseSeconds, progress => run.Flash(0.25f * (1f - progress), flameCore));

			// 거대 화염: 굵어지는 쐐기 모양으로 덩어리를 쏟아냄
			float blastCarry = 0f;
			run.At(BlastStart, () => releaseZoom());
			run.Track(BlastStart, blastSeconds, progress =>
			{
				run.Grade.SetTint(Mathf.Lerp(gatherTint, blastTint, UltimateFxRun.EaseOut(progress * 4f)), warmTint);
				float strength = progress < 0.15f ? progress / 0.15f : 1f - UltimateFxRun.Span(progress, 0.8f, 1f) * 0.7f;
				blastCarry += blastPuffsPerSecond * strength * run.Delta;
				while (blastCarry >= 1f)
				{
					blastCarry -= 1f;
					float life = Random.Range(0.55f, 0.85f);
					Vector3 sideways = Vector3.Cross(Vector3.up, direction) * Random.Range(-1f, 1f) * blastThickness * 0.35f;
					Vector3 lift = Vector3.up * Random.Range(-0.2f, 1f) * blastThickness * 0.3f;
					Vector3 target = head + sideways + lift;
					Vector3 velocity = (target - muzzle) / life * Random.Range(0.9f, 1.15f);
					UltimateFxStage.Emit(flames, muzzle, velocity, Random.Range(blastPuffSize.x, blastPuffSize.y), life, Color.white, Random.value * 360f, Random.Range(-120f, 120f));
				}
				light.transform.position = Vector3.Lerp(muzzle, head, 0.4f);
				light.intensity = lightIntensity * strength;
			}, () => light.intensity = 0f);
			run.ShakeSeries(BlastStart, 10, 0.12f, blastShake, 0.9f);

			// 선화 반전 한 장: 배경을 흰 종이 위 윤곽선으로
			run.Grade.SetLineColors(lineInk, Color.white);
			run.Track(InvertStart, invertSeconds, _ => run.Grade.LineArt = 1f, () => run.Grade.LineArt = 0f);

			// 방사선과 이중 고리: 화염 끝에서
			run.Track(RingStart, ringSeconds, progress =>
			{
				run.Layer.SetStreaks(streakAlpha * (1f - progress), Color.Lerp(Color.white, flameCore, 0.3f), progress * 25f, Mathf.Lerp(1.1f, 1.9f, progress));
				run.Grade.SetTint(blastTint * (1f - progress * 0.5f), warmTint);
			}, () => run.Layer.SetStreaks(0f, Color.white, 0f));
			Vector3 headGround = head - Vector3.up * muzzleHeight;
			run.ThinRing(RingStart, headGround, 0.5f, 5f, ringSeconds * 1.6f, flameCore, 8);
			run.ThinRing(RingStart + ringSeconds * 0.25f, headGround, 0.5f, 3.4f, ringSeconds * 1.6f, flameBody, 9);
			run.At(BlastStart, () => run.React(run.Caster + direction * blastLength * 0.4f, 9f, blastLength, false));

			// 연기 소용돌이와 잔불
			run.At(SmokeStart - ringSeconds * 0.5f, () => EmitSmoke(run, muzzle, head));
			run.Track(SmokeStart, smokeSeconds, progress => run.Grade.SetTint(blastTint * 0.5f * (1f - progress), warmTint), () => run.Grade.SetTint(0f, warmTint));
		}

		private Gradient FlameGradient()
		{
			Gradient gradient = new();
			gradient.SetKeys(
				new[]
				{
					new GradientColorKey(flameCore, 0f),
					new GradientColorKey(flameBody, 0.3f),
					new GradientColorKey(flameEdge, 0.7f),
					new GradientColorKey(smokeColor, 1f),
				},
				new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.06f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
			return gradient;
		}

		private void EmitSmoke(UltimateFxRun run, Vector3 muzzle, Vector3 head)
		{
			ParticleSystem smoke = run.Particles("Smoke", run.Alpha(UltimateFxTextures.Smoke, Color.white), smokeCount + 8, -0.15f);
			UltimateFxStage.SetLifetimeLook(smoke, UltimateFxStage.FadeGradient(Color.white, 0.15f, 0.5f), AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2f));
			ParticleSystem.VelocityOverLifetimeModule swirl = smoke.velocityOverLifetime;
			swirl.enabled = true;
			swirl.orbitalY = new ParticleSystem.MinMaxCurve(1.6f);
			ParticleSystem embers = run.Particles("Embers", run.Additive(UltimateFxTextures.Spark, Color.white), emberCount + 8, -0.1f);
			UltimateFxStage.SetLifetimeLook(embers, UltimateFxStage.FadeGradient(Color.white, 0.1f, 0.5f), null);
			for (int i = 0; i < smokeCount; i++)
			{
				Vector3 point = Vector3.Lerp(muzzle, head, Random.value) + Random.insideUnitSphere * 1.2f;
				UltimateFxStage.Emit(smoke, point, Vector3.up * Random.Range(0.6f, 1.6f), Random.Range(2.4f, 4.5f), Random.Range(1.2f, smokeSeconds + 0.8f), smokeColor, Random.value * 360f, Random.Range(-30f, 30f));
			}
			for (int i = 0; i < emberCount; i++)
			{
				Vector3 point = Vector3.Lerp(muzzle, head, Random.value) + Random.insideUnitSphere * 1.5f;
				Color color = Color.Lerp(flameBody, flameCore, Random.value);
				UltimateFxStage.Emit(embers, point, Random.insideUnitSphere * 1.5f + Vector3.up * Random.Range(0.5f, 2f), Random.Range(0.1f, 0.25f), Random.Range(0.6f, smokeSeconds + 0.5f), color);
			}
		}
	}
}
