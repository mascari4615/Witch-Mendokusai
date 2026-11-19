using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 달 가르기 궁극기. 점 무늬로 달이 나타나 커짐 -> 배경이 점 무늬 우주로 -> 칼끝 빛줄이 달까지 솟음 -> 번쩍 한 장
	/// -> 흑백 방사 임팩트 두 장 -> 사선 절단 -> 2차 번쩍과 고리 -> 두 쪽이 갈라져 벌어짐 -> 점 무늬로 녹아 사라짐
	/// - 실루엣: 큰 원 하나 (달) 와 사선 하나 (칼). 배경 장악: 두 색 점 무늬 (남색과 흰색) 별 하늘로 교체
	/// - 시간 구조는 참고 영상 15fps 실측 (등장과 성장 1.4초, 우주 전환 2.2초, 번쩍 1장, 임팩트 2장, 절단 0.67초, 분리 1.2초, 소멸 0.6초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_04_Moon", menuName = "WM/CombatFx/Ultimate/" + nameof(MoonUltimateFx))]
	public sealed class MoonUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField] private Color ditherDark = new(0.03f, 0.04f, 0.13f, 1f);
		[SerializeField] private Color ditherLight = new(0.72f, 0.78f, 1f, 1f);
		[SerializeField] private Color spaceTop = new(0.01f, 0.01f, 0.05f, 1f);
		[SerializeField] private Color spaceBottom = new(0.05f, 0.06f, 0.16f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color bladeColor = new(1.6f, 1.7f, 2f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("실측 1.4초")] private float growSeconds = 1.4f;
		[SerializeField, Min(0.1f), Tooltip("실측 2.2초")] private float spaceSeconds = 2.2f;
		[SerializeField, Min(0.01f), Tooltip("실측 1장")] private float flashSeconds = 0.067f;
		[SerializeField, Min(0.01f), Tooltip("번쩍 뒤 임팩트까지")] private float impactDelay = 0.2f;
		[SerializeField, Min(0.01f), Tooltip("실측 2장")] private float impactSeconds = 0.133f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.67초")] private float cutSeconds = 0.67f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.2초")] private float separateSeconds = 1.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.6초")] private float vanishSeconds = 0.6f;

		[Header("크기와 배치 (월드 단위)")]
		[SerializeField, Min(1f), Tooltip("처음 지름")] private float moonStartSize = 3f;
		[SerializeField, Min(2f), Tooltip("다 자란 지름. 캐릭터 키의 약 9배")] private float moonSize = 8f;
		[SerializeField] private Vector2 moonOffset = new(0f, 3.6f);
		[SerializeField, Range(-90f, 90f), Tooltip("절단 사선 각 (도)")] private float cutAngle = 58f;
		[SerializeField, Min(0f)] private float separateDistance = 2.2f;
		[SerializeField, Min(0f), Tooltip("갈라진 두 쪽이 기우는 각 (도)")] private float separateRoll = 9f;
		[SerializeField, Min(1f)] private float impactRadius = 10f;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f), Tooltip("우주 위 점 무늬 세기")] private float spaceDither = 0.9f;
		[SerializeField, Range(0.8f, 1.5f), Tooltip("우주 전환 동안 카메라가 물러나는 배율")] private float spaceZoom = 1.18f;
		[SerializeField, Range(0f, 1f)] private float flashPeak = 0.9f;
		[SerializeField, Range(0f, 1f)] private float streakAlpha = 1f;
		[SerializeField, Min(0f)] private float impactShake = 1.6f;

		private float SpaceStart => growSeconds;
		private float FlashTime => SpaceStart + spaceSeconds;
		private float ImpactStart => FlashTime + flashSeconds + impactDelay;
		private float CutStart => ImpactStart + impactSeconds;
		private float SecondFlash => CutStart + cutSeconds;
		private float SeparateStart => SecondFlash + flashSeconds * 3f;
		private float VanishStart => SeparateStart + separateSeconds;

		public override float DurationSeconds => VanishStart + vanishSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 moonCenter = run.Caster + run.ScreenRight * moonOffset.x + Vector3.up * moonOffset.y;
			Camera mainCamera = run.MainCamera;
			Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;
			Vector3 cameraUp = mainCamera != null ? mainCamera.transform.up : Vector3.up;
			Vector2 cutNormal = new(Mathf.Cos((cutAngle + 90f) * Mathf.Deg2Rad), Mathf.Sin((cutAngle + 90f) * Mathf.Deg2Rad));
			Vector3 cutNormalWorld = cameraRight * cutNormal.x + cameraUp * cutNormal.y;

			// 달 두 쪽: 같은 그림을 절단선 양쪽으로 잘라 그림. 절단 전에는 하나처럼 붙어 있음
			Material halfA = run.Alpha(UltimateFxTextures.Moon, Color.white);
			Material halfB = run.Alpha(UltimateFxTextures.Moon, Color.white);
			halfA.SetVector("_ClipLine", new Vector4(cutNormal.x, cutNormal.y, 0f, 1f));
			halfB.SetVector("_ClipLine", new Vector4(-cutNormal.x, -cutNormal.y, 0f, 1f));
			GameObject moonA = run.Billboard("MoonA", halfA, moonCenter, moonStartSize, moonStartSize, false, true, 4);
			GameObject moonB = run.Billboard("MoonB", halfB, moonCenter, moonStartSize, moonStartSize, false, true, 4);

			// 등장과 성장: 점 무늬로 서서히
			run.Track(0f, growSeconds + spaceSeconds, progress =>
			{
				float seconds = progress * (growSeconds + spaceSeconds);
				float appear = UltimateFxRun.Span(seconds, 0f, growSeconds);
				float size = Mathf.Lerp(moonStartSize, moonSize, UltimateFxRun.EaseOut(seconds / (growSeconds + spaceSeconds * 0.5f)));
				moonA.transform.localScale = moonB.transform.localScale = new Vector3(size, size, 1f);
				halfA.SetFloat("_Dither", 1f - appear);
				halfB.SetFloat("_Dither", 1f - appear);
			});

			// 우주 전환: 별 하늘로 바꾼 뒤 그 위에 점 무늬 두 색
			run.Grade.SetSpaceColors(spaceTop, spaceBottom, Color.clear);
			run.Grade.SetDitherColors(ditherDark, ditherLight);
			System.Action releaseZoom = delegate { };
			run.At(SpaceStart, () => releaseZoom = run.Zoom(spaceZoom, 4f, spaceSeconds, float.PositiveInfinity, 1f));
			run.Track(SpaceStart, spaceSeconds, progress =>
			{
				run.Grade.Space = UltimateFxRun.EaseInOut(progress * 1.4f);
				run.Grade.Dither = spaceDither * UltimateFxRun.EaseInOut(progress);
			});

			// 칼끝 빛줄: 시전자에서 달까지 솟는 가는 선
			Material bladeMaterial = run.Additive(UltimateFxTextures.Line, bladeColor);
			GameObject blade = null;
			float bladeLength = (moonCenter - run.Caster).magnitude;
			run.Track(SpaceStart + spaceSeconds * 0.4f, spaceSeconds * 0.6f, progress =>
			{
				if (blade == null)
					blade = run.Billboard("BladeLine", bladeMaterial, run.Caster, 0.3f, 0.01f, true, true, 6);
				blade.transform.localScale = new Vector3(0.3f, bladeLength * UltimateFxRun.EaseOut(progress), 1f);
			});

			// 번쩍 한 장
			run.Track(FlashTime, flashSeconds, _ => run.Flash(flashPeak, Color.white), () => run.Flash(0f, Color.white));

			// 흑백 방사 임팩트 두 장
			run.Grade.SetToneColors(Color.black, Color.white, 0.18f);
			run.Track(ImpactStart, impactSeconds, _ =>
			{
				run.Grade.TwoTone = 1f;
				run.Layer.SetBand(0, streakAlpha, Color.white, cutAngle, 0.08f, 0.05f, true);
				run.Layer.SetBand(1, streakAlpha, Color.white, cutAngle, -0.12f, 0.02f, true);
			}, () =>
			{
				run.Grade.TwoTone = 0f;
				run.Layer.SetBand(0, 0f, Color.white, cutAngle, 0f, 0f, true);
				run.Layer.SetBand(1, 0f, Color.white, cutAngle, 0f, 0f, true);
				run.Destroy(blade);
			});
			run.At(ImpactStart, () =>
			{
				run.ShakeSeries(ImpactStart, 6, 0.07f, impactShake, 0.75f);
				run.React(run.Center, 12f, impactRadius, false);
			});

			// 절단: 달을 가로지르는 사선이 자라남
			Material slashMaterial = run.Additive(UltimateFxTextures.Line, bladeColor);
			GameObject slash = null;
			run.Track(CutStart, cutSeconds, progress =>
			{
				if (slash == null)
				{
					slash = run.Billboard("Slash", slashMaterial, moonCenter, 0.01f, 0.5f, false, true, 8);
					slash.GetComponent<UltimateBillboard>().Roll = cutAngle;
				}
				slash.transform.localScale = new Vector3(moonSize * 1.6f * UltimateFxRun.EaseOut(progress * 2f), Mathf.Lerp(0.6f, 0.15f, progress), 1f);
				slashMaterial.SetColor("_Color", bladeColor * (1f - UltimateFxRun.Span(progress, 0.7f, 1f)));
			}, () => run.Destroy(slash));

			// 2차 번쩍과 고리
			run.Track(SecondFlash, flashSeconds, _ => run.Flash(flashPeak * 0.7f, Color.white), () => run.Flash(0f, Color.white));
			Material arcMaterial = run.Additive(UltimateFxTextures.ThinRing, Color.clear);
			GameObject arc = null;
			run.Track(SecondFlash, 0.8f, progress =>
			{
				if (arc == null)
					arc = run.Billboard("MoonRing", arcMaterial, moonCenter, 1f, 1f, false, true, 7);
				arc.transform.localScale = Vector3.one * moonSize * Mathf.Lerp(1.05f, 1.6f, UltimateFxRun.EaseOut(progress));
				arcMaterial.SetColor("_Color", new Color(bladeColor.r, bladeColor.g, bladeColor.b, 1f - progress));
			}, () => run.Destroy(arc));

			// 분리: 두 쪽이 절단선의 법선 방향으로 벌어지며 기움
			run.Track(SeparateStart, separateSeconds + vanishSeconds, progress =>
			{
				float open = UltimateFxRun.EaseOut(progress * 1.4f) * separateDistance;
				float drop = UltimateFxRun.EaseIn(progress) * separateDistance;
				moonA.transform.position = moonCenter + cutNormalWorld * open - Vector3.up * drop;
				moonB.transform.position = moonCenter - cutNormalWorld * open - Vector3.up * drop;
				moonA.GetComponent<UltimateBillboard>().Roll = separateRoll * UltimateFxRun.EaseOut(progress);
				moonB.GetComponent<UltimateBillboard>().Roll = -separateRoll * UltimateFxRun.EaseOut(progress);
			});

			// 점 무늬로 녹아 사라짐, 배경 복귀
			run.At(VanishStart, () => releaseZoom());
			run.Track(VanishStart, vanishSeconds, progress =>
			{
				halfA.SetFloat("_Dither", progress);
				halfB.SetFloat("_Dither", progress);
				run.Grade.Space = 1f - progress;
				run.Grade.Dither = spaceDither * (1f - progress);
			}, () =>
			{
				run.Grade.Space = 0f;
				run.Grade.Dither = 0f;
				run.Destroy(moonA);
				run.Destroy(moonB);
			});
		}
	}
}
