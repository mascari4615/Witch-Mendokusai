using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 윈도우 진입점. WMWindow 생성·마운트, 카테고리 사이드바 빌드, 단축키 wire(단계 D).
	/// World 씬 진입 시 Resources/Singletons/CodexWindowController.prefab 에서 자동 스폰 (단계 D 에서 prefab 추가).
	/// DevWindowController 와 같은 모양.
	/// </summary>
	public class CodexWindowController : MonoBehaviour
	{
		public static CodexWindowController Instance { get; private set; }

		public static bool TryGetExistingInstance(out CodexWindowController mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private const string WINDOW_ID = "Codex";
		private const string WINDOW_TITLE = "도감";

		private WMWindow window;
		private CodexView view;

		public EntryProviderRegistry Providers { get; } = new();

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void Start()
		{
			BuildWindow();
			RegisterBuiltins();
			view.RebuildRoot(Providers.Providers);

			view.OnCategorySelected += OnCategorySelected;
			view.OnEntrySelected += OnEntrySelected;

			InputManager.Instance.RegisterInputEvent(InputEventType.CodexToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.CodexToggle, InputEventResponseType.Performed, OnToggle);

			if (Instance == this)
				Instance = null;
		}

		private void OnToggle() => Toggle();

		private void RegisterBuiltins()
		{
			if (Providers.FindById("block") == null)
				Providers.Register(new BlockCodexCategory());
			if (Providers.FindById("item") == null)
				Providers.Register(new ItemCodexCategory());
			if (Providers.FindById("entity") == null)
				Providers.Register(new EntityCodexCategory());
		}

		private void BuildWindow()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = WINDOW_TITLE,
			};
			window.style.left = 100;
			window.style.top = 100;
			window.style.width = 900;
			window.style.height = 600;

			view = new CodexView();
			view.style.flexGrow = 1;

			StyleSheet codexStyleSheet = Resources.Load<StyleSheet>("Codex/CodexWindow");
			if (codexStyleSheet != null)
				view.styleSheets.Add(codexStyleSheet);

			window.Content.Add(view);

			UIRoot.Instance.WindowsLayer.Add(window);
		}

		public void Toggle() => window?.Toggle();
		public void Open() => window?.Open();
		public void Close() => window?.Close();

		private void OnCategorySelected(IEntryProvider category) => view.SetActiveCategory(category);
		private void OnEntrySelected(EntryDescriptor entry) => view.SetActiveEntry(entry);
	}
}
