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
		// 이동: InputEventType 아닌 Move 축 입력, 레지스트리 밖
		private const string MOVE_KEYS = "W A S D";

		public const string USS_VIEW = "wm-keybind-help";
		public const string USS_CATEGORY = "wm-keybind-help__category";
		public const string USS_CATEGORY_LABEL = "wm-keybind-help__category-label";
		public const string USS_ROW = "wm-keybind-help__row";
		public const string USS_DISPLAY_NAME = "wm-keybind-help__display-name";
		public const string USS_KEY = "wm-keybind-help__key";

		private WMWindow window;
		// 던전 안에서는 달리기가 막혀 있어서 이 줄을 숨김. 창을 열 때마다 다시 맞춤
		private VisualElement sprintRow;
		// 큰 지도는 던전 안에서만 쓰므로 던전 밖에서는 이 줄을 숨김
		private VisualElement minimapRow;
		// 첫 진입 때 저절로 열린 채인지. 사람이 F1 로 연 창은 저절로 안 닫음
		private bool isOpenedForFirstTime;
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
			KeybindRegistry.OnChanged += RebuildContent;

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
			KeybindRegistry.OnChanged -= RebuildContent;
			inputManager.UnregisterInputEvent(InputEventType.KeybindHelpToggle, InputEventResponseType.Performed, OnToggle);
			StageManager.OnStageChanged -= OnFirstStage;
			StageManager.OnStageChanged -= OnStageLeft;
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
			// 키보드 단축키 창이라 손가락 조작에서는 안 띄움. 기록도 안 남겨 나중에 키보드로 하면 그때 한 번 (2026-11-17 폰)
			// 폰은 첫 터치 전엔 터치 모드로 안 잡혀서 기기 종류도 같이 봄
			if (inputManager.IsTouchMode || Application.isMobilePlatform)
				yield break;
			StageManager.OnStageChanged -= OnFirstStage;
			PlayerPrefs.SetInt(SHOWN_PREF_KEY, 1);
			PlayerPrefs.Save();
			RefreshSprintRow();
			RefreshMinimapRow();
			window.Open();
			isOpenedForFirstTime = true;
			StageManager.OnStageChanged += OnStageLeft;
		}

		// 저절로 연 안내는 처음 스테이지를 떠나면 닫음. 판 안까지 남아 카드 고르기와 결과창을 덮던 것
		private void OnStageLeft(Stage stage, StageObject stageObject)
		{
			StageManager.OnStageChanged -= OnStageLeft;
			if (isOpenedForFirstTime)
				window.Close();
			isOpenedForFirstTime = false;
		}

		// 릴리스: 개발자 창 단축키 줄 숨김 (DevWindow 와 같은 조건)
		private static bool IsVisible(KeybindEntry entry)
			=> KeybindRegistry.IsEnabledInThisBuild(entry.EventType);

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
				if (visibleEntries.Count == 0 && group.Key != KeybindRegistry.MOVE_CATEGORY)
					continue;

				VisualElement categoryBox = new();
				categoryBox.AddToClassList(USS_CATEGORY);

				Label categoryLabel = new(group.Key);
				categoryLabel.AddToClassList(USS_CATEGORY_LABEL);
				categoryBox.Add(categoryLabel);

				if (group.Key == KeybindRegistry.MOVE_CATEGORY)
					categoryBox.Add(CreateRow(KeybindRegistry.MOVE_DISPLAY_NAME, MOVE_KEYS));

				foreach (KeybindEntry entry in visibleEntries)
				{
					VisualElement row = CreateRow(entry.DisplayName, KeybindFormat.FormatPath(entry.CurrentPath));
					if (entry.EventType == InputEventType.Sprint)
						sprintRow = row;
					if (entry.EventType == InputEventType.MinimapLargeView)
						minimapRow = row;
					categoryBox.Add(row);
				}

				scrollView.Add(categoryBox);
			}

			window.Content.Add(scrollView);
		}

		// 설정창 키 탭에서 키가 바뀌면 줄 글자를 새로 지음. 줄 참조 (달리기, 큰 지도) 도 다시 잡힘
		private void RebuildContent()
		{
			sprintRow = null;
			minimapRow = null;
			window.Content.Clear();
			BuildContent();
			RefreshSprintRow();
			RefreshMinimapRow();
		}

		private void OnToggle()
		{
			if (window == null)
				return;

			RefreshSprintRow();
			RefreshMinimapRow();
			isOpenedForFirstTime = false;
			window.Toggle();
		}

		private void RefreshSprintRow()
		{
			if (sprintRow == null)
				return;

			sprintRow.style.display = DungeonManagerBridge.IsDungeon ? DisplayStyle.None : DisplayStyle.Flex;
		}

		private void RefreshMinimapRow()
		{
			if (minimapRow == null)
				return;

			minimapRow.style.display = DungeonManagerBridge.IsDungeon ? DisplayStyle.Flex : DisplayStyle.None;
		}
	}
}
