using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 키 안내 표시의 한 자리. 손가락 조작일 때 키 글자를 숨기고 손가락용 글을 보임
	/// - UIRoot 가 터치 모드일 때 패널 뿌리에 TOUCH_ROOT_CLASS
	/// - 공용 시트 (Slot.uss) 규칙이 KEY_CLASS 는 터치에서 숨김, TOUCH_CLASS 는 터치에서만 보임
	/// 문자열에 키를 박지 말고 키 부분만 따로 요소로 (2026-11-17 폰 실기: PC 단축키가 곳곳에 보임)
	/// </summary>
	public static class KeyHint
	{
		public const string TOUCH_ROOT_CLASS = "wm-input-touch";
		public const string KEY_CLASS = "wm-key-hint";
		public const string TOUCH_CLASS = "wm-touch-hint";

		/// <summary> 키보드와 패드에서만 보일 요소 </summary>
		public static T KeyOnly<T>(T element) where T : VisualElement
		{
			element.AddToClassList(KEY_CLASS);
			return element;
		}

		/// <summary> 손가락 조작에서만 보일 요소 </summary>
		public static T TouchOnly<T>(T element) where T : VisualElement
		{
			element.AddToClassList(TOUCH_CLASS);
			return element;
		}

		/// <summary> 버튼 글 옆에 붙이는 키 표시. 예: 닫기 (ESC) </summary>
		public static Label KeyLabel(string key) => KeyOnly(new Label(" (" + key + ")"));
	}
}
