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
		private const float SIZE = 186f;

		private readonly VisualElement root;
		private readonly VisualElement dotLayer;
		private readonly VisualElement viewRect;
		private readonly List<VisualElement> dots = new();
		private int usedDots;

		public VisualElement Root => root;

		public TowerDefenseMinimapView()
		{
			root = new VisualElement { name = "Minimap" };
			root.style.position = Position.Absolute;
			root.style.right = 24;
			root.style.bottom = 130;
			root.style.width = SIZE;
			root.style.height = SIZE;
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
				PlaceDot(match, match.CoreCombatant.Position, stage.CoreTint, 7f);

			foreach (Transform outpost in match.Outposts)
			{
				if (outpost != null)
					PlaceDot(match, outpost.position, stage.OutpostTint, 6f);
			}

			// 내 인형들 — 이름표가 있는 것이 곧 내가 세운 것이다(같은 목록을 쓰니 화면과 안 갈라진다).
			foreach (TowerDefenseDollLabel doll in match.DollLabels)
			{
				if (doll.IsAlive == false)
					continue;

				Color tint = doll.Working ? doll.Tint : new Color(0.45f, 0.47f, 0.52f, 1f);
				PlaceDot(match, doll.Anchor.position, tint, 4f);
			}

			// 마수·둥지 — 밝힌 곳만. 안 가본 자리를 미니맵이 알려주면 시야가 무의미해진다.
			foreach (ArenaCombatant enemy in match.WaveEnemies)
			{
				if (enemy == null || enemy.IsAlive == false)
					continue;
				if (match.IsExploredAt(enemy.Position) == false)
					continue;

				PlaceDot(match, enemy.Position, stage.EnemyTint, 3.5f);
			}

			if (match.HasHero)
				PlaceDot(match, match.HeroPosition, stage.HeroTint, 6f);

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
			float width = Mathf.Clamp01(visibleWidth / match.GroundWidth) * SIZE;
			float length = Mathf.Clamp01(visibleLength / match.GroundLength) * SIZE;

			viewRect.style.display = DisplayStyle.Flex;
			viewRect.style.width = width;
			viewRect.style.height = length;
			viewRect.style.left = center.x * SIZE - width * 0.5f;
			viewRect.style.top = (1f - center.y) * SIZE - length * 0.5f;
		}

		private void PlaceDot(TowerDefenseMatch match, Vector3 worldPosition, Color color, float size)
		{
			Vector2 normalized = ToNormalized(match, worldPosition);
			if (normalized.x < 0f || normalized.x > 1f || normalized.y < 0f || normalized.y > 1f)
				return; // 판 밖 — 그릴 자리가 없다.

			VisualElement dot = RentDot();
			dot.style.display = DisplayStyle.Flex;
			dot.style.width = size;
			dot.style.height = size;
			dot.style.backgroundColor = color;
			int radius = Mathf.RoundToInt(size * 0.5f);
			dot.style.borderTopLeftRadius = radius;
			dot.style.borderTopRightRadius = radius;
			dot.style.borderBottomLeftRadius = radius;
			dot.style.borderBottomRightRadius = radius;
			dot.style.left = normalized.x * SIZE - size * 0.5f;
			// 화면 세로는 위가 0 이고 판의 +z 는 위쪽이라 뒤집는다.
			dot.style.top = (1f - normalized.y) * SIZE - size * 0.5f;
		}

		/// <summary> 월드 좌표 → 판 안의 비율(0~1). 판이 자라도 이 식은 그대로다. </summary>
		private static Vector2 ToNormalized(TowerDefenseMatch match, Vector3 worldPosition)
		{
			Vector3 local = match.StageRoot.InverseTransformPoint(worldPosition);
			return new Vector2(
				local.x / match.GroundWidth + 0.5f,
				local.z / match.GroundLength + 0.5f);
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
