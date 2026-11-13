using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 단축키 안내 창 — KeybindRegistry 가 노출하는 단일 출처에서 카테고리·표시명·현재 키를 자동 렌더한다.
	/// 새 InputEventType 항목을 추가하면 이 창에 자동으로 따라 나타난다.
	/// </summary>
	public class KeybindHelpView : MonoBehaviour
	{
		private const string WINDOW_ID = "KeybindHelp";
		// 첫 스테이지 진입 때 한 번 자동 열기, 이후 F1
		private const string SHOWN_PREF_KEY = "KeybindHelpShownOnce";
		private const string MOVE_CATEGORY = "이동";
		// 이동: InputEventType 아닌 Move 축 입력, 레지스트리 밖
		private const string MOVE_DISPLAY_NAME = "이동";
		private const string MOVE_KEYS = "W A S D";

		public const string USS_VIEW = "wm-keybind-help";
		public const string USS_CATEGORY = "wm-keybind-help__category";
		public const string USS_CATEGORY_LABEL = "wm-keybind-help__category-label";
		public const string USS_ROW = "wm-keybind-help__row";
		public const string USS_DISPLAY_NAME = "wm-keybind-help__display-name";
		public const string USS_KEY = "wm-keybind-help__key";

		private WMWindow window;
		private UIRoot uiRoot;
		private InputManager inputManager;

		[Inject]
		public void Construct(InputManager inputManager)
		{
			this.inputManager = inputManager;
		}

		private void Start()
		{
			uiRoot = GetComponent<UIRoot>();
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = "단축키 안내 (F1)"
			};
			window.style.left = 80;
			window.style.top = 60;
			window.style.width = 360;
			window.style.height = 520;
			uiRoot.WindowsLayer.Add(window);

			BuildContent();

			inputManager.RegisterInputEvent(InputEventType.KeybindHelpToggle, InputEventResponseType.Performed, OnToggle);

			if (PlayerPrefs.GetInt(SHOWN_PREF_KEY, 0) == 1)
				return;
			// 스테이지가 이 창보다 먼저 뜬 경우 (StageManager.Start 선행) 알림을 놓치므로 바로 확인
			if (StageManager.TryGetExistingInstance(out StageManager stageManager) && stageManager.CurStage != null)
				StartCoroutine(OpenFirstTime());
			else
				StageManager.OnStageChanged += OnFirstStage;
		}

		private void OnDestroy()
		{
			inputManager.UnregisterInputEvent(InputEventType.KeybindHelpToggle, InputEventResponseType.Performed, OnToggle);
			StageManager.OnStageChanged -= OnFirstStage;
		}

		private void OnFirstStage(Stage stage, StageObject stageObject)
		{
			if (isActiveAndEnabled)
				StartCoroutine(OpenFirstTime());
		}

		// 한 프레임 뒤 판정: 씬 전환 중 사라질 이전 씬 UI 가 받으면 코루틴째 끊겨 기록이 안 남음
		private IEnumerator OpenFirstTime()
		{
			yield return null;
			if (UIRoot.Instance != uiRoot || PlayerPrefs.GetInt(SHOWN_PREF_KEY, 0) == 1)
				yield break;
			StageManager.OnStageChanged -= OnFirstStage;
			PlayerPrefs.SetInt(SHOWN_PREF_KEY, 1);
			PlayerPrefs.Save();
			window.Open();
		}

		// 릴리스: 개발자 창 단축키 줄 숨김 (DevWindow 와 같은 조건)
		private static bool IsVisible(KeybindEntry entry)
			=> entry.ShownInRelease || Debug.isDebugBuild;

		private VisualElement CreateRow(string displayName, string keyText)
		{
			VisualElement row = new();
			row.AddToClassList(USS_ROW);

			Label displayLabel = new(displayName);
			displayLabel.AddToClassList(USS_DISPLAY_NAME);
			row.Add(displayLabel);

			Label keyLabel = new(keyText);
			keyLabel.AddToClassList(USS_KEY);
			row.Add(keyLabel);
			return row;
		}

		private void BuildContent()
		{
			ScrollView scrollView = new();
			scrollView.AddToClassList(USS_VIEW);

			IEnumerable<IGrouping<string, KeybindEntry>> grouped = KeybindRegistry.EnumerateGroupedByCategory();
			foreach (IGrouping<string, KeybindEntry> group in grouped)
			{
				List<KeybindEntry> visibleEntries = group.Where(IsVisible).ToList();
				// 릴리스에서 전부 숨은 묶음 (핫바) 은 제목도 빼기
				if (visibleEntries.Count == 0 && group.Key != MOVE_CATEGORY)
					continue;

				VisualElement categoryBox = new();
				categoryBox.AddToClassList(USS_CATEGORY);

				Label categoryLabel = new(group.Key);
				categoryLabel.AddToClassList(USS_CATEGORY_LABEL);
				categoryBox.Add(categoryLabel);

				if (group.Key == MOVE_CATEGORY)
					categoryBox.Add(CreateRow(MOVE_DISPLAY_NAME, MOVE_KEYS));

				foreach (KeybindEntry entry in visibleEntries)
					categoryBox.Add(CreateRow(entry.DisplayName, FormatPath(entry.CurrentPath)));

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
