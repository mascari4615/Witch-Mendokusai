using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 미니맵(TASK-WM-194) — 판 전체를 한 눈에.
	///
	/// ★ 왜 필요한가: 판이 200칸에서 무한으로 자라는데 전체를 볼 수단이 시점 이동뿐이었다. 어디가 뚫렸는지,
	///   내 채집이 어디 있는지, 둥지가 몇 개 남았는지를 *보려면 매번 카메라를 끌고 다녀야* 했다.
	///   판을 키운 순간부터 이게 가장 큰 구멍이 됐다 — 넓은 판은 보이지 않으면 넓지 않은 것과 같다.
	///
	/// ★ 왜 점으로 그리나: 판이 자라도 그리는 값이 *비율*이라 크기 변화에 저절로 따라온다. 텍스처를 굽는
	///   방식이면 판이 자랄 때마다 다시 구워야 하고, 그 비용이 확장의 발목을 잡는다.
	/// ★ 밝힌 곳만 그린다 — 안 가본 자리의 마수를 미니맵이 알려주면 시야가 무의미해진다.
	///
	/// 점은 재사용한다(매 프레임 새로 만들지 않는다).
	/// </summary>
	public sealed class TowerDefenseMinimapView
	{
		private const float DEFAULT_SIZE = 186f;

		// 지도 한 변의 길이 — 미니맵은 작게, 펼친 지도는 크게. 그리는 규칙은 하나다.
		private readonly float size;

		/// <summary> 점 크기 배율 — 지도가 커지면 점도 같이 커진다(미니맵은 1). </summary>
		private readonly float dotScale;

		private readonly VisualElement root;
		private readonly VisualElement dotLayer;
		private readonly VisualElement viewRect;
		private readonly List<VisualElement> dots = new();
		private int usedDots;

		public VisualElement Root => root;

		/// <param name="size">한 변 길이. 안 주면 화면 구석 미니맵 크기.</param>
		/// <param name="floating">화면 구석에 띄울까(미니맵) 아니면 부모가 자리를 정할까(펼친 지도).</param>
		public TowerDefenseMinimapView(float size = DEFAULT_SIZE, bool floating = true)
		{
			this.size = size;
			// ★ 점 크기가 지도 크기를 안 따라가고 있었다 (실측: 펼친 지도가 미니맵의 3.5배인데 점은
			//   그대로라, 화면을 꽉 채운 지도 위에서 코어가 좁쌀만 했다). 지도를 여는 이유는
			//   「어디에 뭐가 있나」인데 그게 안 보이면 여는 뜻이 없다. 크기에 비례해 같이 큰다.
			dotScale = Mathf.Max(1f, size / DEFAULT_SIZE);
			root = new VisualElement { name = "Minimap" };
			if (floating)
			{
				root.style.position = Position.Absolute;
				root.style.right = 24;
				root.style.bottom = 130;
			}
			root.style.width = size;
			root.style.height = size;
			root.style.backgroundColor = new Color(0.03f, 0.04f, 0.07f, 0.85f);
			root.style.borderLeftWidth = 1;
			root.style.borderRightWidth = 1;
			root.style.borderTopWidth = 1;
			root.style.borderBottomWidth = 1;
			Color border = new Color(1f, 1f, 1f, 0.22f);
			root.style.borderLeftColor = border;
			root.style.borderRightColor = border;
			root.style.borderTopColor = border;
			root.style.borderBottomColor = border;
			root.pickingMode = PickingMode.Ignore;

			dotLayer = new VisualElement();
			dotLayer.style.position = Position.Absolute;
			dotLayer.style.left = 0;
			dotLayer.style.right = 0;
			dotLayer.style.top = 0;
			dotLayer.style.bottom = 0;
			dotLayer.pickingMode = PickingMode.Ignore;
			root.Add(dotLayer);

			// 지금 화면이 보고 있는 자리 — 미니맵의 어디를 보고 있는지 알아야 미니맵이 길잡이가 된다.
			viewRect = new VisualElement();
			viewRect.style.position = Position.Absolute;
			viewRect.style.borderLeftWidth = 1;
			viewRect.style.borderRightWidth = 1;
			viewRect.style.borderTopWidth = 1;
			viewRect.style.borderBottomWidth = 1;
			Color viewColor = new Color(1f, 1f, 1f, 0.55f);
			viewRect.style.borderLeftColor = viewColor;
			viewRect.style.borderRightColor = viewColor;
			viewRect.style.borderTopColor = viewColor;
			viewRect.style.borderBottomColor = viewColor;
			viewRect.pickingMode = PickingMode.Ignore;
			root.Add(viewRect);
		}

		public void SetVisible(bool visible)
		{
			root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
		}

		/// <summary> 한 프레임 갱신 — 코어·내 건물·전초기지·둥지·마수·영웅을 점으로. </summary>
		public void Tick(TowerDefenseMatch match, TowerDefenseStageSO stage)
		{
			if (match == null || match.StageRoot == null || match.GroundWidth <= 0f)
				return;

			usedDots = 0;

			// 코어 — 가장 크게. 지켜야 할 것이 어디인지가 먼저 보여야 한다.
			if (match.CoreCombatant != null)
				PlaceDot(match, match.CoreCombatant.Position, stage.CoreTint, 7f, tip: "코어 — 부서지면 끝. 여기서 연구를 한다.");

			foreach (Transform outpost in match.Outposts)
			{
				if (outpost != null)
					PlaceDot(match, outpost.position, stage.OutpostTint, 6f, square: true, tip: "전초기지 — 새 보급 원점이자 새로 지켜야 할 곳.");
			}

			// 내 인형들 — 이름표가 있는 것이 곧 내가 세운 것이다(같은 목록을 쓰니 화면과 안 갈라진다).
			foreach (TowerDefenseDollLabel doll in match.DollLabels)
			{
				if (doll.IsAlive == false)
					continue;

				Color tint = doll.Working ? doll.Tint : new Color(0.45f, 0.47f, 0.52f, 1f);
				PlaceDot(match, doll.Anchor.position, tint, 4f, tip: doll.Text);
			}

			// 마수 — 밝힌 곳만. 안 가본 자리를 미니맵이 알려주면 시야가 무의미해진다.
			foreach (ArenaCombatant enemy in match.WaveEnemies)
			{
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if (match.IsExploredAt(enemy.Position) == false)
					continue;
				if (match.IsNestCombatant(enemy))
					continue; // 둥지는 아래에서 크게 따로 그린다.

				PlaceDot(match, enemy.Position, stage.EnemyTint, 3.5f, tip: "마수 — 코어로 오는 중.");
			}

			// ★ 둥지는 마수와 확실히 갈라 그린다(개선 목록 11번) — 같은 크기·색이면 「부술 것」이
			//   마수 무리에 파묻혀 안 보인다. 목표는 눈에 띄어야 목표다.
			foreach (Vector3 nest in match.NestPositions)
			{
				if (match.IsExploredAt(nest) == false)
					continue;

				PlaceDot(match, nest, stage.NestTint, 10f, tip: "둥지 — 부수면 그 출구가 닫힌다.");
			}

			if (match.HasHero)
				PlaceDot(match, match.HeroPosition, stage.HeroTint, 6f, tip: "영웅 — 고르고 땅을 찍으면 그리로 간다.");

			// 남는 점은 감춘다(줄어든 프레임에 옛 점이 남으면 미니맵이 거짓말한다).
			for (int index = usedDots; index < dots.Count; index++)
				dots[index].style.display = DisplayStyle.None;

			UpdateViewRect(match);
		}

		private void UpdateViewRect(TowerDefenseMatch match)
		{
			Camera camera = ViewCameraResolver.Current;
			if (camera == null)
			{
				viewRect.style.display = DisplayStyle.None;
				return;
			}

			// 카메라 높이로 보이는 폭을 어림한다 — 정확한 절두체 대신 「대략 이만큼」이면 길잡이로 충분하다.
			float visibleWidth = camera.orthographic
				? camera.orthographicSize * 2f * camera.aspect
				: camera.transform.position.y * 2f * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * camera.aspect;
			float visibleLength = visibleWidth / Mathf.Max(0.01f, camera.aspect);

			Vector2 center = ToNormalized(match, camera.transform.position);
			float width = Mathf.Clamp01(visibleWidth / match.GroundWidth) * this.size;
			float length = Mathf.Clamp01(visibleLength / match.GroundLength) * this.size;

			viewRect.style.display = DisplayStyle.Flex;
			viewRect.style.width = width;
			viewRect.style.height = length;
			viewRect.style.left = center.x * this.size - width * 0.5f;
			viewRect.style.top = (1f - center.y) * this.size - length * 0.5f;
		}

		/// <summary>
		/// 점 하나 — 색만으로 갈리면 색약인 사람에게는 전부 같은 점이다(개선 목록 16번).
		/// 그래서 *모양*도 같이 갈린다: 둥근 것은 살아 있는 것, 네모난 것은 지어둔 것,
		/// 큰 것은 지켜야 할 것/부술 것.
		/// </summary>
		// 마지막으로 그린 판 크기 — 판이 자라면 지형을 다시 굽는다(안 자라면 그대로 쓴다).
		private int bakedWidth;
		private int bakedLength;
		private Texture2D terrain;

		/// <summary>
		/// 지형 그림을 배경으로 깐다 — 「점 몇 개」였던 지도가 땅을 보여주게 된다.
		/// 판이 자랐을 때만 다시 굽는다(굽는 값이 싸지 않다).
		/// </summary>
		public void RefreshTerrain(TowerDefenseMapLayout layout, TowerDefenseStageSO stage)
		{
			if (layout == null || stage == null)
				return;
			if (terrain != null && bakedWidth == layout.Width && bakedLength == layout.Length)
				return;

			terrain = TowerDefenseMapTexture.Bake(layout,
				ground: new Color(0.16f, 0.19f, 0.28f, 1f),
				obstacle: new Color(0.30f, 0.32f, 0.38f, 1f),
				node: new Color(0.85f, 0.70f, 0.30f, 1f));
			bakedWidth = layout.Width;
			bakedLength = layout.Length;
			if (terrain != null)
				root.style.backgroundImage = new StyleBackground(terrain);
		}

		/// <summary>
		/// 이 지도가 마우스 설명을 띄우나 — 펼친 지도만 켠다.
		/// (미니맵은 곁눈질용이라 포인터를 안 받는다. 받으면 그 아래 땅을 못 누른다.)
		/// </summary>
		public bool ShowTooltips { get; set; }

		private void PlaceDot(TowerDefenseMatch match, Vector3 worldPosition, Color color, float size, bool square = false, string tip = null)
		{
			Vector2 normalized = ToNormalized(match, worldPosition);
			if (normalized.x < 0f || normalized.x > 1f || normalized.y < 0f || normalized.y > 1f)
				return; // 판 밖 — 그릴 자리가 없다.

			size *= dotScale;

			VisualElement dot = RentDot();
			dot.style.display = DisplayStyle.Flex;
			dot.style.width = size;
			dot.style.height = size;
			dot.style.backgroundColor = color;
			int radius = square ? 0 : Mathf.RoundToInt(size * 0.5f);
			dot.style.borderTopLeftRadius = radius;
			dot.style.borderTopRightRadius = radius;
			dot.style.borderBottomLeftRadius = radius;
			dot.style.borderBottomRightRadius = radius;
			dot.style.left = normalized.x * this.size - size * 0.5f;
			// 화면 세로는 위가 0 이고 판의 +z 는 위쪽이라 뒤집는다.
			dot.style.top = (1f - normalized.y) * this.size - size * 0.5f;

			// ★ 「각 요소에 마우스 올리면 그거에 대한 정보가 나오고」(사용자 지시). 범례를 외우게 하는
			//   대신 물어보게 한다 — 지도 위의 점이 곧 질문 대상이다.
			if (ShowTooltips == false || string.IsNullOrEmpty(tip))
				return;

			// 점 자체가 질문 대상이 된다 — 그래서 점이 커지면 얹기도 같이 쉬워진다(위 dotScale).
			dot.pickingMode = PickingMode.Position;
			dot.tooltip = tip;
		}

		/// <summary> 월드 좌표 → 판 안의 비율(0~1). 판이 자라도 이 식은 그대로다. </summary>
		private static Vector2 ToNormalized(TowerDefenseMatch match, Vector3 worldPosition)
		{
			Vector3 local = match.StageRoot.InverseTransformPoint(worldPosition);
			return new Vector2(
				local.x / match.GroundWidth + 0.5f,
				local.z / match.GroundLength + 0.5f);
		}

		/// <summary>
		/// 지도 위 한 점 → 판의 그 자리(월드). 위 ToNormalized 의 역이다.
		/// ★ 둘이 어긋나면 「누른 곳과 간 곳이 다르다」가 된다 — 반드시 같은 식의 뒤집기여야 한다.
		/// </summary>
		private static Vector3 ToWorld(TowerDefenseMatch match, Vector2 normalized)
		{
			Vector3 local = new Vector3(
				(normalized.x - 0.5f) * match.GroundWidth,
				0f,
				(normalized.y - 0.5f) * match.GroundLength);
			return match.StageRoot.TransformPoint(local);
		}

		/// <summary> 지도를 눌렀다 — 그 자리의 월드 좌표를 알려준다(지도는 닫히지 않는다). </summary>
		public event System.Action<Vector3> Clicked = delegate { };

		/// <summary>
		/// 누르면 그 자리로 — 롤·스타의 그 조작(사용자 지시). 켜는 쪽에서만 포인터를 받는다.
		/// ★ 미니맵도 켠다 — 곁눈질용이지만 「저기 보자」는 미니맵에서 더 자주 일어난다.
		/// </summary>
		public void EnableClickToLook(TowerDefenseMatch matchSource)
		{
			root.pickingMode = PickingMode.Position;
			root.RegisterCallback<PointerDownEvent>(evt =>
			{
				TowerDefenseMatch match = matchSource;
				if (match == null || match.StageRoot == null)
					return;

				// 지도 안에서의 비율 — 화면 세로는 위가 0 이라 다시 뒤집는다.
				Vector2 normalized = new Vector2(
					Mathf.Clamp01(evt.localPosition.x / this.size),
					Mathf.Clamp01(1f - evt.localPosition.y / this.size));
				Clicked(ToWorld(match, normalized));
				evt.StopPropagation(); // 지도 위 클릭이 그 아래 땅에 건물을 세우면 안 된다.
			});
		}

		private VisualElement RentDot()
		{
			if (usedDots < dots.Count)
				return dots[usedDots++];

			VisualElement dot = new VisualElement();
			dot.style.position = Position.Absolute;
			dot.pickingMode = PickingMode.Ignore;
			dotLayer.Add(dot);
			dots.Add(dot);
			usedDots++;
			return dot;
		}
	}
}
