using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 판의 칸 하나. 성질 그림과 상태 클래스만 알고, 규칙은 모름. 모양은 HexCell 과 DiamondCell
	/// </summary>
	public abstract class AspectCell : Button
	{
		public const string ANCHOR = "wm-aspect-cell--anchor";
		public const string PLACED = "wm-aspect-cell--placed";
		public const string LINKED = "wm-aspect-cell--linked";
		public const string LOOSE = "wm-aspect-cell--loose";
		public const string SELECTED = "wm-aspect-cell--selected";
		public const string GHOST = "wm-aspect-cell--ghost";
		public const string DOLL = "wm-aspect-cell--doll";
		private const string USS_CLASS = "wm-aspect-cell";

		private readonly VisualElement icon = new() { pickingMode = PickingMode.Ignore };
		private readonly VisualElement doll = new() { pickingMode = PickingMode.Ignore };

		protected AspectCell(Action submitted) : base(submitted)
		{
			AddToClassList(USS_CLASS);
			icon.AddToClassList(USS_CLASS + "__icon");
			doll.AddToClassList(USS_CLASS + "__doll");
		}

		/// <summary>모양을 먼저 쌓은 뒤 그림을 위에</summary>
		protected void AddMarks()
		{
			Add(icon);
			Add(doll);
		}

		/// <summary>성질이 없으면 null</summary>
		public void ShowAspect(Sprite sprite) => icon.style.backgroundImage = sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);

		/// <summary>인형이 놓은 칸의 표시. 아직 그림이 없는 인형은 빈 표시만</summary>
		public void ShowDoll(bool isDoll, Sprite sprite)
		{
			EnableInClassList(DOLL, isDoll);
			doll.style.backgroundImage = isDoll && sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
		}

		/// <summary>가운데 자리와 크기 (판 px)</summary>
		public void Place(Vector2 center, Vector2 size)
		{
			style.left = center.x - size.x * 0.5f;
			style.top = center.y - size.y * 0.5f;
			style.width = size.x;
			style.height = size.y;
		}
	}
}
