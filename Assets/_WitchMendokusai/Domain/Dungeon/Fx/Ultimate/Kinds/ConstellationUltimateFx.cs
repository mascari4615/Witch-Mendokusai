using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 별자리 궁극기. 표적 조준 고리 -> 밤하늘과 은하수로 바뀜 -> 별이 하나씩 켜지며 선으로 이어져 표적까지 내려옴 -> 은하수가 걷혀 검은 우주
	/// -> 마지막 별에서 표적으로 번개 한 장 -> 번쩍임 -> 워프 방사선 -> 백색 세 장 -> 표적 위 거대한 십자광 -> 낮 복귀
	/// - 실루엣: 꺾인 선 그래프 (별자리) 와 네 갈래 십자광. 배경 장악: 은하수 밤하늘 -> 검은 우주 2단 교체
	/// - 시간 구조는 참고 영상 15fps 실측 (조준 1.2초, 별자리 1.2초, 우주 0.2초, 워프 1.4초, 백색 3장, 십자광 0.6초, 복귀 1.07초)
	/// </summary>
	[CreateAssetMenu(fileName = "Ultimate_07_Constellation", menuName = "WM/CombatFx/Ultimate/" + nameof(ConstellationUltimateFx))]
	public sealed class ConstellationUltimateFx : UltimateFx
	{
		[Header("색")]
		[SerializeField] private Color nightTop = new(0.02f, 0.03f, 0.1f, 1f);
		[SerializeField] private Color nightBottom = new(0.07f, 0.09f, 0.22f, 1f);
		[SerializeField] private Color milkyWay = new(0.7f, 0.75f, 1f, 0.5f);
		[SerializeField] private Color voidTop = new(0f, 0f, 0.01f, 1f);
		[SerializeField] private Color voidBottom = new(0.01f, 0.01f, 0.04f, 1f);
		[SerializeField, ColorUsage(false, true)] private Color starColor = new(2f, 2f, 2.3f);
		[SerializeField, ColorUsage(false, true)] private Color lineColor = new(1.3f, 1.4f, 1.8f);

		[Header("시간 (초)")]
		[SerializeField, Min(0.1f), Tooltip("조준과 밤하늘 전환. 실측 1.2초")] private float aimSeconds = 1.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.2초")] private float drawSeconds = 1.2f;
		[SerializeField, Min(0.05f), Tooltip("실측 0.2초")] private float voidSeconds = 0.2f;
		[SerializeField, Min(0.01f), Tooltip("번개 한 장")] private float strikeSeconds = 0.067f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.4초")] private float warpSeconds = 1.4f;
		[SerializeField, Min(0.01f), Tooltip("실측 3장")] private float whiteSeconds = 0.2f;
		[SerializeField, Min(0.1f), Tooltip("실측 0.6초")] private float crossSeconds = 0.6f;
		[SerializeField, Min(0.1f), Tooltip("실측 1.07초")] private float returnSeconds = 1.07f;

		[Header("크기와 배치 (월드 단위)")]
		[SerializeField, Tooltip("표적 위치 (화면 가로, 화면 위쪽)")] private Vector2 targetOffset = new(-1.5f, 2.2f);
		[SerializeField, Min(3)] private int starCount = 8;
		[SerializeField, Min(0f), Tooltip("별자리가 걸리는 높이 (월드 위쪽)")] private float skyHeight = 1.5f;
		[SerializeField, Min(1f), Tooltip("별자리 폭 (화면 가로)")] private float skyWidth = 8f;
		[SerializeField, Min(0f), Tooltip("별자리가 걸리는 화면 위쪽 거리")] private float skyDepth = 3f;
		[SerializeField, Min(0.2f)] private float starSize = 1.4f;
		[SerializeField, Min(0.05f)] private float lineWidth = 0.14f;
		[SerializeField, Min(2f), Tooltip("십자광 지름. 캐릭터 키의 약 11배")] private float crossSize = 11f;
		[SerializeField, Min(1)] private int boltCount = 3;
		[SerializeField] private int starSeed = 5;

		[Header("화면과 카메라")]
		[SerializeField, Range(0f, 1f)] private float flashPeak = 0.5f;
		[SerializeField, Range(0f, 1f)] private float warpAlpha = 0.85f;
		[SerializeField, Min(0f)] private float strikeShake = 1.8f;

		private float DrawStart => aimSeconds;
		private float VoidStart => DrawStart + drawSeconds;
		private float StrikeTime => VoidStart + voidSeconds;
		private float WarpStart => StrikeTime + strikeSeconds * 3f;
		private float WhiteStart => WarpStart + warpSeconds;
		private float CrossStart => WhiteStart + whiteSeconds;
		private float ReturnStart => CrossStart + crossSeconds;

		public override float DurationSeconds => ReturnStart + returnSeconds;

		public override void Compose(UltimateFxRun run)
		{
			Vector3 target = run.Center + run.ScreenRight * targetOffset.x + run.ScreenForward * targetOffset.y;
			target.y = UltimateFxStage.GroundHeight(target);
			Vector3[] stars = PlaceStars(run, target);

			// 조준 고리와 밤하늘
			run.ThinRing(0f, target, 2.5f, 1.2f, aimSeconds + drawSeconds, new Color(1f, 1f, 1f, 1f), 8);
			run.Grade.SetSpaceColors(nightTop, nightBottom, milkyWay);
			run.Track(0f, aimSeconds, progress => run.Grade.Space = UltimateFxRun.EaseInOut(progress));

			// 별자리: 별이 하나씩 켜지고 선이 다음 별로 자라남. 마지막 선은 표적 바닥까지
			Material starMaterial = run.Additive(UltimateFxTextures.CrossGlint, starColor);
			Material lineMaterial = run.Additive(UltimateFxTextures.Line, lineColor);
			List<GameObject> lit = new();
			LineRenderer constellation = CreateLine(run, "Constellation", lineMaterial, lineWidth);
			float step = drawSeconds / starCount;
			for (int i = 0; i < starCount; i++)
			{
				int index = i;
				run.At(DrawStart + step * index, () =>
				{
					GameObject star = run.Billboard("Star" + index, starMaterial, stars[index], starSize, starSize, false, true, 9);
					lit.Add(star);
				});
			}
			run.Track(DrawStart, drawSeconds, progress =>
			{
				float reached = progress * starCount;
				int whole = Mathf.Min(starCount - 1, Mathf.FloorToInt(reached));
				Vector3 next = whole + 1 < starCount ? stars[whole + 1] : target;
				constellation.positionCount = whole + 2;
				for (int i = 0; i <= whole; i++)
					constellation.SetPosition(i, stars[i]);
				constellation.SetPosition(whole + 1, Vector3.Lerp(stars[whole], next, reached - whole));
				for (int i = 0; i < lit.Count; i++)
					lit[i].transform.localScale = Vector3.one * starSize * (1f + 0.25f * Mathf.Sin(run.Now * 12f + i));
			}, () =>
			{
				constellation.positionCount = starCount + 1;
				for (int i = 0; i < starCount; i++)
					constellation.SetPosition(i, stars[i]);
				constellation.SetPosition(starCount, target);
			});

			// 검은 우주: 은하수가 걷힘
			run.Track(VoidStart, voidSeconds, progress =>
			{
				Color top = Color.Lerp(nightTop, voidTop, progress);
				Color bottom = Color.Lerp(nightBottom, voidBottom, progress);
				run.Grade.SetSpaceColors(top, bottom, new Color(milkyWay.r, milkyWay.g, milkyWay.b, milkyWay.a * (1f - progress)));
			});

			// 번개 한 장과 번쩍임 둘
			List<LineRenderer> bolts = new();
			Material boltMaterial = run.Additive(UltimateFxTextures.Line, starColor);
			run.Track(StrikeTime, strikeSeconds * 3f, progress =>
			{
				if (bolts.Count == 0)
				{
					for (int i = 0; i < boltCount; i++)
						bolts.Add(CreateLine(run, "Bolt" + i, boltMaterial, lineWidth * 2.5f));
				}
				foreach (LineRenderer bolt in bolts)
					Jag(bolt, stars[starCount - 1], target);
				run.Flash(progress < 0.34f ? flashPeak : 0f, Color.white);
			}, () =>
			{
				foreach (LineRenderer bolt in bolts)
					run.Destroy(bolt.gameObject);
				run.Flash(0f, Color.white);
			});
			run.At(StrikeTime, () =>
			{
				run.ShakeSeries(StrikeTime, 5, 0.07f, strikeShake, 0.75f);
				run.React(target, 12f, 9f, false);
				run.SparkBurst(target + Vector3.up * 0.5f, 80, new Vector2(8f, 20f), new Vector2(0.4f, 1.1f), new Vector2(0.15f, 0.35f), lineColor, starColor, 0.8f);
			});

			// 워프 방사선: 표적 쪽으로 빨려드는 빛줄, 중간에 한 번 번쩍
			run.Track(WarpStart, warpSeconds, progress =>
			{
				run.Layer.SetStreaks(warpAlpha * UltimateFxRun.Bell(progress, 0.1f), Color.white, -progress * 30f, Mathf.Lerp(2.4f, 0.9f, progress));
				constellation.widthMultiplier = lineWidth * (1f - progress);
				foreach (GameObject star in lit)
					star.transform.position = Vector3.Lerp(star.transform.position, target + Vector3.up * 2f, progress * 0.08f);
			}, () =>
			{
				run.Layer.SetStreaks(0f, Color.white, 0f);
				run.Destroy(constellation.gameObject);
				foreach (GameObject star in lit)
					run.Destroy(star);
			});

			// 백색 세 장
			run.Track(WhiteStart, whiteSeconds, _ => run.Flash(flashPeak, Color.white), () => run.Flash(0f, Color.white));

			// 거대한 십자광
			Material crossMaterial = run.Additive(UltimateFxTextures.CrossGlint, Color.clear);
			GameObject cross = null;
			run.Track(CrossStart, crossSeconds + returnSeconds * 0.5f, progress =>
			{
				if (cross == null)
					cross = run.Billboard("Cross", crossMaterial, target + Vector3.up * 1.5f, 1f, 1f, false, true, 12);
				float grow = UltimateFxRun.EaseOut(progress * 2.5f);
				cross.transform.localScale = Vector3.one * crossSize * Mathf.Lerp(0.3f, 1f, grow);
				cross.GetComponent<UltimateBillboard>().Roll = progress * 12f;
				crossMaterial.SetColor("_Color", new Color(starColor.r, starColor.g, starColor.b, 1f - UltimateFxRun.Span(progress, 0.4f, 1f)));
			}, () => run.Destroy(cross));
			run.ThinRing(CrossStart, target, 1f, crossSize * 0.7f, crossSeconds * 1.5f, new Color(1f, 1f, 1f, 1f), 8);
			run.ThinRing(CrossStart + 0.1f, target, 1f, crossSize * 0.45f, crossSeconds * 1.5f, new Color(1f, 1f, 1f, 1f), 9);

			// 복귀
			run.Track(ReturnStart, returnSeconds, progress => run.Grade.Space = 1f - UltimateFxRun.EaseInOut(progress), () => run.Grade.Space = 0f);
		}

		private Vector3[] PlaceStars(UltimateFxRun run, Vector3 target)
		{
			System.Random random = new(starSeed);
			Vector3[] stars = new Vector3[starCount];
			Vector3 start = run.Caster + run.ScreenRight * skyWidth * 0.45f + run.ScreenForward * skyDepth + Vector3.up * skyHeight;
			Vector3 finish = target + run.ScreenForward * skyDepth * 0.3f + Vector3.up * skyHeight;
			for (int i = 0; i < starCount; i++)
			{
				float along = i / (starCount - 1f);
				Vector3 point = Vector3.Lerp(start, finish, along);
				point += run.ScreenForward * ((float)random.NextDouble() - 0.5f) * skyDepth * 0.6f;
				stars[i] = point;
			}
			return stars;
		}

		private static LineRenderer CreateLine(UltimateFxRun run, string name, Material material, float width)
		{
			GameObject lineObject = new(name);
			lineObject.transform.SetParent(run.Root, false);
			LineRenderer line = lineObject.AddComponent<LineRenderer>();
			line.sharedMaterial = material;
			material.renderQueue = 3018;
			material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
			line.useWorldSpace = true;
			line.positionCount = 0;
			line.alignment = LineAlignment.View;
			line.widthMultiplier = width;
			line.numCapVertices = 4;
			line.numCornerVertices = 2;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
			return line;
		}

		private static void Jag(LineRenderer bolt, Vector3 from, Vector3 to)
		{
			const int POINTS = 14;
			bolt.positionCount = POINTS;
			Vector3 side = Vector3.Cross((to - from).normalized, Vector3.up);
			for (int i = 0; i < POINTS; i++)
			{
				float along = i / (POINTS - 1f);
				float wiggle = (i == 0 || i == POINTS - 1) ? 0f : Random.Range(-1f, 1f) * 0.9f;
				bolt.SetPosition(i, Vector3.Lerp(from, to, along) + side * wiggle + Vector3.up * Random.Range(-0.3f, 0.3f));
			}
		}
	}
}
