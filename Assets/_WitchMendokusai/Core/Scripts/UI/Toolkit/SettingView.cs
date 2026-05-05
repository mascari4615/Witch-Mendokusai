using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 전체화면 환경설정 UI (엔드필드/오버워치 스타일).
	/// UIRoot.ScreenLayer에 직접 VisualElement를 추가함.
	/// InventoryView와 동일한 MonoBehaviour 패턴 사용.
	/// </summary>
	public class SettingView : MonoBehaviour
	{
		private const string USS_CLASS = "wm-setting-view";
		private const string ACTIVE_CLASS = "wm-setting-view--active";

		private VisualElement container;

		private Button btnDungeonExit;

		// Audio
		private Slider masterVolume;
		private Slider bgmVolume;
		private Slider sfxVolume;

		// System
		private Toggle framerateToggle;

		public bool IsOpen { get; private set; }

		private void Start()
		{
			container = new VisualElement();
			container.AddToClassList(USS_CLASS);
			UIRoot.Instance.ScreenLayer.Add(container);

			BuildUI();

			// 초기 상태: 닫힘 (display: none 으로 처리됨)
			IsOpen = false;
		}

		private void OnDestroy()
		{
			container?.RemoveFromHierarchy();
		}

		private void BuildUI()
		{
			// 좌측 사이드바
			VisualElement sidebar = new VisualElement();
			sidebar.AddToClassList("wm-setting-sidebar");

			Label titleLabel = new Label("환경설정");
			titleLabel.AddToClassList("wm-setting-title");
			sidebar.Add(titleLabel);

			Button tabSystem = new Button { text = "시스템 설정" };
			tabSystem.AddToClassList("wm-setting-tab");
			tabSystem.AddToClassList("wm-setting-tab--active");
			sidebar.Add(tabSystem);

			// 우측 컨텐츠
			VisualElement content = new VisualElement();
			content.AddToClassList("wm-setting-content");

			// Audio
			Label audioHeader = new Label("오디오");
			audioHeader.AddToClassList("wm-setting-header");
			content.Add(audioHeader);

			masterVolume = CreateSlider("마스터 볼륨", 0f, 1f, AudioManager.Instance.GetVolume(AudioManager.BusType.Master));
			masterVolume.RegisterValueChangedCallback(evt => AudioManager.Instance.SetVolume(AudioManager.BusType.Master, evt.newValue));
			content.Add(masterVolume);

			bgmVolume = CreateSlider("배경음악 (BGM)", 0f, 1f, AudioManager.Instance.GetVolume(AudioManager.BusType.BGM));
			bgmVolume.RegisterValueChangedCallback(evt => AudioManager.Instance.SetVolume(AudioManager.BusType.BGM, evt.newValue));
			content.Add(bgmVolume);

			sfxVolume = CreateSlider("효과음 (SFX)", 0f, 1f, AudioManager.Instance.GetVolume(AudioManager.BusType.SFX));
			sfxVolume.RegisterValueChangedCallback(evt => AudioManager.Instance.SetVolume(AudioManager.BusType.SFX, evt.newValue));
			content.Add(sfxVolume);

			// System
			Label systemHeader = new Label("시스템");
			systemHeader.AddToClassList("wm-setting-header");
			content.Add(systemHeader);

			framerateToggle = new Toggle("60 FPS 고정");
			framerateToggle.AddToClassList("wm-setting-toggle");
			framerateToggle.value = Application.targetFrameRate == 60;
			framerateToggle.RegisterValueChangedCallback(evt => Application.targetFrameRate = evt.newValue ? 60 : 30);
			content.Add(framerateToggle);

			// 버튼 그룹
			VisualElement buttonGroup = new VisualElement();
			buttonGroup.AddToClassList("wm-setting-buttons");

			btnDungeonExit = new Button(OnDungeonExit) { text = "던전 포기" };
			btnDungeonExit.AddToClassList("wm-setting-btn");
			btnDungeonExit.AddToClassList("wm-setting-btn--danger");
			buttonGroup.Add(btnDungeonExit);

			Button btnSaveInit = new Button(OnClearData) { text = "세이브 데이터 초기화" };
			btnSaveInit.AddToClassList("wm-setting-btn");
			btnSaveInit.AddToClassList("wm-setting-btn--danger");
			buttonGroup.Add(btnSaveInit);

			Button btnQuit = new Button(OnQuit) { text = "게임 종료" };
			btnQuit.AddToClassList("wm-setting-btn");
			buttonGroup.Add(btnQuit);

			content.Add(buttonGroup);

			Button btnClose = new Button(Close) { text = "닫기 (ESC)" };
			btnClose.AddToClassList("wm-setting-close");
			content.Add(btnClose);

			container.Add(sidebar);
			container.Add(content);
		}

		private static Slider CreateSlider(string label, float min, float max, float value)
		{
			Slider slider = new Slider(label, min, max)
			{
				value = value
			};
			slider.AddToClassList("wm-setting-slider");
			return slider;
		}

		private void OnDungeonExit()
		{
			Close();
			if (Player.Instance != null && Player.Instance.Object != null)
				Player.Instance.Object.ReceiveDamage(new DamageInfo(damage: 9999, DamageType.Critical, new DamageContext(Player.Instance.Object), ignoreInvincible: true));
		}

		private void OnClearData() => DataManager.Instance.CreateNewGameData();

		private void OnQuit() => Application.Quit();

		public void Open()
		{
			if (IsOpen) return;
			IsOpen = true;
			container.AddToClassList(ACTIVE_CLASS);

			// HACK: World 씬에서만 던전 포기 버튼 활성화
			bool isWorld = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "World";
			bool isDungeon = DungeonManager.TryGetExistingInstance(out DungeonManager dm) && dm.IsDungeon;
			btnDungeonExit.style.display = (isWorld && isDungeon) ? DisplayStyle.Flex : DisplayStyle.None;

			TimeManager.Instance.Pause(gameObject);
		}

		public void Close()
		{
			if (IsOpen == false) return;
			IsOpen = false;
			container.RemoveFromClassList(ACTIVE_CLASS);
			TimeManager.Instance.Resume(gameObject);
		}

		public void Toggle()
		{
			if (IsOpen) Close();
			else Open();
		}
	}
}
