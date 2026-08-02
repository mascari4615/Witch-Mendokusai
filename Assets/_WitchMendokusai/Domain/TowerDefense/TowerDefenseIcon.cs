using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 아이콘을 *그려서* 만든다(TASK-WM-194) — 아트 에셋 0.
	///
	/// ★ 왜 코드로 그리는가: 지금 화면엔 그림이 하나도 없어 전부 글자로만 읽힌다(사용자 지적).
	///   그렇다고 아이콘 그림을 기다리면 UI 개선이 통째로 멈춘다. 도형·색만으로도 「자원인가 수입인가」,
	///   「이 포탑은 무엇인가」는 즉시 구분되고, 나중에 진짜 그림이 나오면 이 자리만 갈아끼우면 된다.
	///
	/// 모든 아이콘은 정사각 상자 안에 들어간다 — 핫바 칸이 종류마다 들쭉날쭉해지지 않게.
	/// </summary>
	public static class TowerDefenseIcon
	{
		public enum Kind
		{
			Dot = 0,     // 기본 — 한 점을 쏜다.
			Burst = 1,   // 광역 — 퍼진다.
			Beam = 2,    // 관통 — 꿰뚫는다.
			Snow = 3,    // 둔화 — 붙잡는다.
			Diamond = 4, // 자원.
			Ring = 5,    // 수입/정산.
			Core = 6,    // 코어.
			Leaf = 7,    // 채집.
		}

		public static VisualElement Make(Kind kind, Color color, int size)
		{
			VisualElement box = new VisualElement();
			box.style.width = size;
			box.style.height = size;
			box.style.alignItems = Align.Center;
			box.style.justifyContent = Justify.Center;
			box.pickingMode = PickingMode.Ignore;

			switch (kind)
			{
				case Kind.Dot:
					box.Add(Circle(color, size * 0.5f, filled: true));
					break;

				case Kind.Burst:
					// 가운데 점 + 바깥 테두리 = 「퍼진다」.
					box.Add(Circle(new Color(color.r, color.g, color.b, 0.45f), size * 0.95f, filled: false));
					box.Add(Circle(color, size * 0.34f, filled: true));
					break;

				case Kind.Beam:
					// 가로로 긴 막대 = 「직선으로 꿰뚫는다」.
					box.Add(Bar(color, size * 0.95f, size * 0.22f));
					break;

				case Kind.Snow:
					// 마름모 = 「얼려 붙잡는다」(정사각을 45도 돌린 것).
					box.Add(Diamond(color, size * 0.62f));
					break;

				case Kind.Diamond:
					box.Add(Diamond(color, size * 0.7f));
					break;

				case Kind.Ring:
					box.Add(Circle(color, size * 0.85f, filled: false));
					break;

				case Kind.Core:
					box.Add(Circle(new Color(color.r, color.g, color.b, 0.4f), size * 0.95f, filled: false));
					box.Add(Diamond(color, size * 0.5f));
					break;

				case Kind.Leaf:
					// 한쪽만 둥근 사각 = 「캐서 담는다」.
					VisualElement leaf = Bar(color, size * 0.7f, size * 0.7f);
					leaf.style.borderTopLeftRadius = (int)(size * 0.45f);
					leaf.style.borderBottomRightRadius = (int)(size * 0.45f);
					box.Add(leaf);
					break;
			}

			return box;
		}

		/// <summary> 포탑 성질에서 아이콘을 고른다 — 데이터가 바뀌면 아이콘도 따라 바뀐다(따로 지정 X). </summary>
		public static Kind ForTower(TowerDefenseTowerArchetype tower)
		{
			if (tower == null)
				return Kind.Dot;
			if (tower.SlowFactor > 0f)
				return Kind.Snow;
			if (tower.SplashRadius > 0f)
				return Kind.Burst;
			if (tower.Pierce > 1)
				return Kind.Beam;
			return Kind.Dot;
		}

		private static VisualElement Circle(Color color, float diameter, bool filled)
		{
			VisualElement circle = new VisualElement();
			circle.style.position = Position.Absolute;
			circle.style.width = diameter;
			circle.style.height = diameter;
			int radius = Mathf.RoundToInt(diameter * 0.5f);
			circle.style.borderTopLeftRadius = radius;
			circle.style.borderTopRightRadius = radius;
			circle.style.borderBottomLeftRadius = radius;
			circle.style.borderBottomRightRadius = radius;
			circle.pickingMode = PickingMode.Ignore;

			if (filled)
			{
				circle.style.backgroundColor = color;
				return circle;
			}

			circle.style.borderLeftWidth = 2;
			circle.style.borderRightWidth = 2;
			circle.style.borderTopWidth = 2;
			circle.style.borderBottomWidth = 2;
			circle.style.borderLeftColor = color;
			circle.style.borderRightColor = color;
			circle.style.borderTopColor = color;
			circle.style.borderBottomColor = color;
			return circle;
		}

		private static VisualElement Bar(Color color, float width, float height)
		{
			VisualElement bar = new VisualElement();
			bar.style.position = Position.Absolute;
			bar.style.width = width;
			bar.style.height = height;
			bar.style.backgroundColor = color;
			bar.pickingMode = PickingMode.Ignore;
			return bar;
		}

		private static VisualElement Diamond(Color color, float side)
		{
			VisualElement diamond = Bar(color, side, side);
			// VisualElement.transform 은 버전 간 안정적인 회전 경로(style.rotate 는 시그니처가 갈린다).
			diamond.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
			return diamond;
		}
	}
}
