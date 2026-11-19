using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 창 비와 가시 왕관 궁극기. 하늘 반짝 -> 사선 창 비 -> 집중선 한 장 -> 흑백 임팩트 한 장 (흰 광선) -> 돔 고리
	/// -> 흰 실루엣 한 장 -> 분홍 가시 왕관 정점 -> 유지하며 가라앉음 -> 점 무늬로 분해되고 반짝이가 흩어짐
	/// - 실루엣: 가운데가 높은 가시 수십 개의 왕관. 색은 분홍 한 색과 흰색. 배경 장악: 0.4초 안에 두 색 화면 두 번 (검정, 흰색) 뒤 분홍 물들임
	/// - 시간 구조는 참고 영상 15fps 실측 (창 비 0.6초, 집중선 1장, 임팩트 1장, 돔 0.27초, 실루엣 1장, 유지 0.4초, 분해 0.33초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_02_SpearCrown", menuName = "WM/CombatFx/Ultimate/" + nameof(SpearCrownUltimateFx))]
	public sealed class SpearCrownUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color pink = new(1.8f, 0.45f, 0.75f);
		[SerializeField, ColorUsage(false, true)] private Color pinkCore = new(2.2f, 1.6f, 1.9f);
		[SerializeField] private Color pinkTint = new(1f, 0.45f, 0.7f, 1f);
		[SerializeField] private Color silhouettePaper = new(0.88f, 0.86f, 0.88f, 1f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.05f)] private float skySeconds = 0.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.6초")] private float rainSeconds = 0.6f;
		[SerializeField, Min(0.01f), Tooltip("실측 1장")] private float frameSeconds = 0.067f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.27초")] private float domeSeconds = 0.27f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.4초")] private float holdSeconds = 0.4f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.33초")] private float dissolveSeconds = 0.33f;
		[SerializeField, Min(0f), Tooltip("분해 뒤 반짝이가 남는 시간")] private float sparkleSeconds = 0.8f;

		[Header("크기와 배치 (월드 단위)")]
		[SerializeField, Min(0f), Tooltip("표적까지 화면 위쪽 거리")] private float targetDistance = 2f;
		[SerializeField, Min(1)] private int spearCount = 40;
		[SerializeField, Min(0.5f)] private float spearLength = 3.2f;
		[SerializeField, Range(-60f, 60f), Tooltip("창이 떨어지는 기울기 (도)")] private float spearTilt = -24f;
		[SerializeField, Min(1)] private int spikeCount = 28;
		[SerializeField, Min(1f), Tooltip("가운데 가시 높이. 캐릭터 키의 약 6~9배")] private float crownHeight = 6.5f;
		[SerializeField, Min(1f), Tooltip("왕관 반폭. 전체 폭이 캐릭터 키의 약 11배")] private float crownHalfWidth = 5.5f;
		[SerializeField] private Vector2 spikeWidth = new(0.9f, 1.8f);
		[SerializeField, Min(1f)] private float domeRadius = 8f;
		[SerializeField, Min(0)] private int sparkleCount = 120;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float streakAlpha = 1f;
		[SerializeField, Range(0f, 1f)] private float tintPeak = 0.45f;
		[SerializeField, Min(0f)] private float impactShake = 2f;

		private float RainStart => skySeconds;
		private float FocusTime => RainStart + rainSeconds;
		private float ImpactTime => FocusTime + frameSeconds;
		private float DomeStart => ImpactTime + frameSeconds * 2f;
		private float SilhouetteTime => DomeStart + domeSeconds;
		private float PeakTime => SilhouetteTime + frameSeconds;
		private float DissolveStart => PeakTime + frameSeconds + holdSeconds;

		public override float DurationSeconds => DissolveStart + dissolveSeconds + sparkleSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 target = run.Center + run.ScreenForward * targetDistance;
			target.y = UltimateFxStage.GroundHeight(target);
			Camera mainCamera = run.MainCamera;
			Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;

			// 하늘 반짝: 창이 나올 자리에 작은 별
			Material sparkMaterial = run.Additive(UltimateFxTextures.CrossGlint, Color.clear);
			List<GameObject> glints = new();
			for (int i = 0; i < 4; i++)
				glints.Add(run.Billboard("SkyGlint" + i, sparkMaterial, target + Vector3.up * 9f + cameraRight * (i * 1.6f - 6f), 1.6f, 1.6f, false, true, 9));
			run.Track(0f, skySeconds + rainSeconds * 0.3f, progress => sparkMaterial.SetColor("_Color", new Color(pinkCore.r, pinkCore.g, pinkCore.b, UltimateFxRun.Bell(progress, 0.4f))), () =>
			{
				foreach (GameObject glint in glints)
					run.Destroy(glint);
			});

			// 창 비: 사선으로 떨어져 표적 둘레에 박힘
			Material spearMaterial = run.Additive(UltimateFxTextures.Spear, pink);
			List<GameObject> spears = new();
			List<Vector3> landings = new();
			List<float> delays = new();
			for (int i = 0; i < spearCount; i++)
			{
				Vector3 landing = target + UltimateFxStage.RandomRingPoint(0.3f, crownHalfWidth * 0.9f);
				landings.Add(landing);
				delays.Add(Random.value * rainSeconds * 0.6f);
				GameObject spear = run.Billboard("Spear" + i, spearMaterial, landing, spearLength * 0.1f, spearLength, false, true, 8);
				spear.GetComponent<UltimateBillboard>().Roll = spearTilt + 180f;
				spear.SetActive(false);
				spears.Add(spear);
			}
			Vector3 fallDirection = (Vector3.up + cameraRight * Mathf.Tan(-spearTilt * Mathf.Deg2Rad)).normalized;
			run.Track(RainStart, rainSeconds, progress =>
			{
				float seconds = progress * rainSeconds;
				for (int i = 0; i < spears.Count; i++)
				{
					float fall = UltimateFxRun.Span(seconds, delays[i], delays[i] + rainSeconds * 0.4f);
					spears[i].SetActive(fall > 0f);
					spears[i].transform.position = landings[i] + fallDirection * (1f - fall) * 14f + fallDirection * spearLength * 0.3f;
				}
			});

			// 집중선 한 장, 흑백 임팩트 한 장 (흰 광선 기둥)
			run.Track(FocusTime, frameSeconds, _ => run.Layer.SetStreaks(streakAlpha, Color.white, 0f, 1.05f), () => run.Layer.SetStreaks(0f, Color.white, 0f));
			Material rayMaterial = run.Additive(UltimateFxTextures.Pillar, Color.clear);
			GameObject ray = run.Billboard("Ray", rayMaterial, target, 3f, 30f, true, true, 12);
			run.Track(ImpactTime, frameSeconds * 3f, progress =>
			{
				run.Grade.SetToneColors(Color.black, Color.white, 0.9f);
				run.Grade.TwoTone = progress < 0.34f ? 1f : 0f;
				rayMaterial.SetColor("_Color", new Color(3f, 3f, 3f, 1f - progress));
				ray.transform.localScale = new Vector3(Mathf.Lerp(3f, 1f, progress), 30f, 1f);
			}, () =>
			{
				run.Grade.TwoTone = 0f;
				run.Destroy(ray);
				foreach (GameObject spear in spears)
					run.Destroy(spear);
			});
			run.At(ImpactTime, () =>
			{
				run.ShakeSeries(ImpactTime, 6, 0.06f, impactShake, 0.72f);
				run.React(target, 14f, domeRadius, false);
			});

			// 돔 고리
			run.ThinRing(DomeStart, target, 1f, domeRadius, domeSeconds * 2.5f, pinkCore, 8);
			run.Shockwave(DomeStart, target, domeRadius, domeSeconds * 3f, 0.05f, 0.14f, pink);

			// 가시 왕관: 흰 실루엣 한 장 -> 분홍 정점 -> 가라앉음 -> 점 무늬 분해
			Material spikeMaterial = run.Additive(UltimateFxTextures.Spike, Color.clear);
			Material spikeBody = run.Alpha(UltimateFxTextures.Spike, Color.clear);
			List<GameObject> spikes = new();
			List<float> heights = new();
			for (int i = 0; i < spikeCount; i++)
			{
				float offset = (i / (spikeCount - 1f)) * 2f - 1f;
				offset += Random.Range(-0.03f, 0.03f);
				float height = crownHeight * Mathf.Lerp(1f, 0.32f, Mathf.Abs(offset)) * Random.Range(0.75f, 1.05f);
				Vector3 foot = target + cameraRight * offset * crownHalfWidth + run.ScreenForward * Random.Range(-0.6f, 0.6f);
				float width = Mathf.Lerp(spikeWidth.y, spikeWidth.x, Mathf.Abs(offset));
				GameObject body = run.Billboard("SpikeBody" + i, spikeBody, foot, width, height, true, false, 9);
				GameObject glow = run.Billboard("Spike" + i, spikeMaterial, foot, width * 1.15f, height * 1.04f, true, false, 10);
				body.GetComponent<UltimateBillboard>().Roll = glow.GetComponent<UltimateBillboard>().Roll = -offset * 32f;
				body.SetActive(false);
				glow.SetActive(false);
				spikes.Add(body);
				spikes.Add(glow);
				heights.Add(height);
				heights.Add(height * 1.04f);
			}
			run.Grade.SetTint(0f, pinkTint);
			run.Track(SilhouetteTime, PeakTime + frameSeconds + holdSeconds + dissolveSeconds - SilhouetteTime, progress =>
			{
				float seconds = run.Now - SilhouetteTime;
				bool silhouette = seconds < frameSeconds;
				foreach (GameObject spike in spikes)
					spike.SetActive(true);
				if (silhouette)
				{
					run.Grade.SetToneColors(silhouettePaper * 0.55f, silhouettePaper, 0.5f);
					run.Grade.TwoTone = 1f;
					spikeBody.SetColor("_Color", new Color(4f, 4f, 4f, 1f));
					spikeMaterial.SetColor("_Color", Color.clear);
					return;
				}
				run.Grade.TwoTone = 0f;
				float peak = 1f - UltimateFxRun.Span(seconds, frameSeconds, frameSeconds * 4f);
				spikeBody.SetColor("_Color", Color.Lerp(new Color(pink.r * 0.5f, pink.g * 0.5f, pink.b * 0.5f, 1f), new Color(3f, 3f, 3f, 1f), peak));
				spikeMaterial.SetColor("_Color", new Color(pinkCore.r, pinkCore.g, pinkCore.b, Mathf.Lerp(0.45f, 1f, peak)));
				run.Grade.SetTint(tintPeak * (1f - UltimateFxRun.Span(seconds, frameSeconds * 4f, frameSeconds * 2f + holdSeconds + dissolveSeconds)), pinkTint);
				float sink = UltimateFxRun.Span(seconds, frameSeconds * 2f, frameSeconds * 2f + holdSeconds + dissolveSeconds);
				for (int i = 0; i < spikes.Count; i++)
				{
					Vector3 scale = spikes[i].transform.localScale;
					spikes[i].transform.localScale = new Vector3(scale.x, heights[i] * Mathf.Lerp(1f, 0.7f, UltimateFxRun.EaseIn(sink)), 1f);
				}
				float dissolve = UltimateFxRun.Span(seconds, frameSeconds * 2f + holdSeconds, frameSeconds * 2f + holdSeconds + dissolveSeconds);
				spikeBody.SetFloat("_Dither", dissolve);
				spikeMaterial.SetFloat("_Dither", dissolve);
			}, () =>
			{
				foreach (GameObject spike in spikes)
					run.Destroy(spike);
				run.Grade.SetTint(0f, pinkTint);
			});
			run.Track(SilhouetteTime, frameSeconds, _ => run.Flash(0.4f, Color.white), () => run.Flash(0f, Color.white));

			// 분해 반짝이
			run.At(DissolveStart, () => run.SparkBurst(target + Vector3.up * crownHeight * 0.3f, sparkleCount, new Vector2(2f, 7f), new Vector2(0.5f, sparkleSeconds + 0.3f), new Vector2(0.15f, 0.4f), pink, pinkCore, -0.2f));
		}
	}
}
