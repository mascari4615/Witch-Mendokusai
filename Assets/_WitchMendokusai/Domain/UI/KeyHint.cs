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

		public const string KEY_BUTTON_CLASS = "wm-key-button";

		/// <summary>
		/// 글과 키 표시가 나란한 버튼. 예: 닫기 (ESC)
		/// 버튼의 text 와 자식 글을 같이 쓰면 한 자리에 겹쳐 그려져서 (2026-11-17 설정창 닫기 버튼), 둘 다 자식 글로 나란히
		/// </summary>
		public static Button KeyButton(string text, string key, System.Action clicked)
		{
			Button button = new Button(clicked);
			button.AddToClassList(KEY_BUTTON_CLASS);
			button.Add(new Label(text) { pickingMode = PickingMode.Ignore });
			Label keyLabel = KeyOnly(new Label("(" + key + ")"));
			keyLabel.pickingMode = PickingMode.Ignore;
			button.Add(keyLabel);
			return button;
		}
	}
}
