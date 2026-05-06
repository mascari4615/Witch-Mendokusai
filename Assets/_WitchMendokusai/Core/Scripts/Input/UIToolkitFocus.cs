using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// UI Toolkit 모든 활성 UIDocument 를 스캔해 텍스트 입력 컨트롤 포커스 여부 검사.
	// IsTyping GameCondition 의 일반화 — UIChat/DevWindow CommandLine 외 모든 TextField 가 자동 보호.
	public static class UIToolkitFocus
	{
		public static bool IsAnyTextFieldFocused()
		{
			UIDocument[] documents = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude);

			for (int i = 0; i < documents.Length; i++)
			{
				UIDocument document = documents[i];
				VisualElement root = document.rootVisualElement;
				if (root == null)
					continue;

				IPanel panel = root.panel;
				if (panel == null)
					continue;

				FocusController focusController = panel.focusController;
				if (focusController == null)
					continue;

				VisualElement focused = focusController.focusedElement as VisualElement;
				while (focused != null)
				{
					if (focused is TextField)
						return true;
					focused = focused.parent;
				}
			}

			return false;
		}
	}
}
