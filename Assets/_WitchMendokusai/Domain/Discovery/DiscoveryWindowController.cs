using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 윈도우 진입점. WMWindow 생성·마운트, 카테고리 사이드바 빌드, 단축키 wire(단계 D).
	/// World 씬 진입 시 Resources/Singletons/DiscoveryWindowController.prefab 에서 자동 스폰 (단계 D 에서 prefab 추가).
	/// DevWindowController 와 같은 모양.
	/// </summary>
	public class DiscoveryWindowController : MonoBehaviour
	{
		public static DiscoveryWindowController Instance { get; private set; }

		public static bool TryGetExistingInstance(out DiscoveryWindowController mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private const string WINDOW_ID = "Discovery";
		private const string WINDOW_TITLE = "도감";

		private InputManager inputManager;
		private UIRoot uiRoot;
		private WMWindow window;
		private DiscoveryView view;

		public EntryProviderRegistry Providers { get; } = new();

		[Inject]
		public void Construct(InputManager inputManager, UIRoot uiRoot)
		{
			this.inputManager = inputManager;
			this.uiRoot = uiRoot;
		}

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

			inputManager.RegisterInputEvent(InputEventType.DiscoveryToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.DiscoveryToggle, InputEventResponseType.Performed, OnToggle);

			if (Instance == this)
				Instance = null;
		}

		private void OnToggle() => Toggle();

		private void RegisterBuiltins()
		{
			if (Providers.FindById("block") == null)
				Providers.Register(new BlockDiscoveryCategory());
			if (Providers.FindById("item") == null)
				Providers.Register(new ItemDiscoveryCategory());
			if (Providers.FindById("entity") == null)
				Providers.Register(new EntityDiscoveryCategory());
			if (Providers.FindById("plant") == null)
				Providers.Register(new PlantDiscoveryCategory());
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

			view = new DiscoveryView();
			view.style.flexGrow = 1;

			StyleSheet discoveryStyleSheet = Resources.Load<StyleSheet>("Discovery/DiscoveryWindow");
			if (discoveryStyleSheet != null)
				view.styleSheets.Add(discoveryStyleSheet);

			window.Content.Add(view);

			uiRoot.WindowsLayer.Add(window);
		}

		public void Toggle() => window?.Toggle();
		public void Open() => window?.Open();
		public void Close() => window?.Close();

		private void OnCategorySelected(IEntryProvider category) => view.SetActiveCategory(category);
		private void OnEntrySelected(EntryDescriptor entry) => view.SetActiveEntry(entry);
	}
}
