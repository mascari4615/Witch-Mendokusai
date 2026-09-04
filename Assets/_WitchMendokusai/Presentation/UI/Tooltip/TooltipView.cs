using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// 툴팁 카드 베이스 컨테이너.
	/// 빌더가 자식 element와 변형 USS class를 자유롭게 부여.
	/// 위치/표시 토글은 TooltipController가 담당.
	/// </summary>
	public class TooltipView : VisualElement
	{
		public const string USS_CLASS = "wm-tooltip";
		public const string VISIBLE_CLASS = "wm-tooltip--visible";

		public TooltipView()
		{
			AddToClassList(USS_CLASS);
			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;
		}

		public void SetVisible(bool visible)
		{
			if (visible)
				AddToClassList(VISIBLE_CLASS);
			else
				RemoveFromClassList(VISIBLE_CLASS);
		}
	}
}
