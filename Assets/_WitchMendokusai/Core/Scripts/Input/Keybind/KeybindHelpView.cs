using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 단축키 안내 창 — KeybindRegistry 가 노출하는 단일 출처에서 카테고리·표시명·현재 키를 자동 렌더한다.
	/// 새 InputEventType 항목을 추가하면 이 창에 자동으로 따라 나타난다.
	/// </summary>
	public class KeybindHelpView : MonoBehaviour
	{
		private const string WINDOW_ID = "KeybindHelp";

		public const string USS_VIEW = "wm-keybind-help";
		public const string USS_CATEGORY = "wm-keybind-help__category";
		public const string USS_CATEGORY_LABEL = "wm-keybind-help__category-label";
		public const string USS_ROW = "wm-keybind-help__row";
		public const string USS_DISPLAY_NAME = "wm-keybind-help__display-name";
		public const string USS_KEY = "wm-keybind-help__key";

		private WMWindow window;

		private void Start()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "단축키 안내 (F1)"
			};
			window.style.left = 80;
			window.style.top = 60;
			window.style.width = 360;
			window.style.height = 520;
			UIRoot.Instance.WindowsLayer.Add(window);

			BuildContent();

			InputManager.Instance.RegisterInputEvent(InputEventType.KeybindHelpToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.KeybindHelpToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void BuildContent()
		{
			ScrollView scrollView = new();
			scrollView.AddToClassList(USS_VIEW);

			IEnumerable<IGrouping<string, KeybindEntry>> grouped = KeybindRegistry.EnumerateGroupedByCategory();
			foreach (IGrouping<string, KeybindEntry> group in grouped)
			{
				VisualElement categoryBox = new();
				categoryBox.AddToClassList(USS_CATEGORY);

				Label categoryLabel = new(group.Key);
				categoryLabel.AddToClassList(USS_CATEGORY_LABEL);
				categoryBox.Add(categoryLabel);

				foreach (KeybindEntry entry in group)
				{
					VisualElement row = new();
					row.AddToClassList(USS_ROW);

					Label displayLabel = new(entry.DisplayName);
					displayLabel.AddToClassList(USS_DISPLAY_NAME);
					row.Add(displayLabel);

					Label keyLabel = new(FormatPath(entry.CurrentPath));
					keyLabel.AddToClassList(USS_KEY);
					row.Add(keyLabel);

					categoryBox.Add(row);
				}

				scrollView.Add(categoryBox);
			}

			window.Content.Add(scrollView);
		}

		private static string FormatPath(string path)
		{
			if (string.IsNullOrEmpty(path))
				return "(없음)";

			// "<Keyboard>/j" → "J", "<Mouse>/leftButton" → "Mouse Left", "<Keyboard>/leftShift" → "Left Shift"
			int slashIndex = path.IndexOf('/');
			if (slashIndex < 0 || slashIndex == path.Length - 1)
				return path;

			string device = path.Substring(0, slashIndex).Trim('<', '>');
			string key = path.Substring(slashIndex + 1);

			if (device == "Keyboard")
				return PrettifyKeyboardKey(key);
			if (device == "Mouse")
				return $"Mouse {PrettifyMouseKey(key)}";

			return $"{device} {key}";
		}

		private static string PrettifyKeyboardKey(string key)
		{
			return key switch
			{
				"leftShift" => "Left Shift",
				"rightShift" => "Right Shift",
				"leftCtrl" => "Left Ctrl",
				"rightCtrl" => "Right Ctrl",
				"ctrl" => "Ctrl",
				"leftAlt" => "Left Alt",
				"rightAlt" => "Right Alt",
				"upArrow" => "↑",
				"downArrow" => "↓",
				"leftArrow" => "←",
				"rightArrow" => "→",
				"slash" => "/",
				"space" => "Space",
				"tab" => "Tab",
				_ => key.Length == 1 ? key.ToUpper() : key
			};
		}

		private static string PrettifyMouseKey(string key)
		{
			return key switch
			{
				"leftButton" => "Left",
				"rightButton" => "Right",
				"middleButton" => "Middle",
				"scroll" => "Scroll",
				_ => key
			};
		}

		private void OnToggle() => window?.Toggle();
	}
}
