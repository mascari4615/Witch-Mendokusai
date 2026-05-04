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
	public class CodexWindowController : Singleton<CodexWindowController>
	{
		private const string WINDOW_ID = "Codex";
		private const string WINDOW_TITLE = "도감";
		private const string WORLD_SCENE_NAME = "World";

		private WMWindow window;
		private CodexView view;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void Bootstrap()
		{
			SceneManager.sceneLoaded += OnSceneLoaded;
			Scene active = SceneManager.GetActiveScene();
			if (active.name == WORLD_SCENE_NAME)
				_ = Instance;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (scene.name == WORLD_SCENE_NAME && TryGetExistingInstance(out _) == false)
				_ = Instance;
		}

		private void Start()
		{
			BuildWindow();
			RegisterBuiltins();
			view.RebuildRoot(CodexCategoryRegistry.Instance.Categories);

			view.OnCategorySelected += OnCategorySelected;
			view.OnEntrySelected += OnEntrySelected;

			InputManager.Instance.RegisterInputEvent(InputEventType.CodexToggle, InputEventResponseType.Performed, OnToggle);
		}

		protected override void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.CodexToggle, InputEventResponseType.Performed, OnToggle);

			base.OnDestroy();
		}

		private void OnToggle() => Toggle();

		private void RegisterBuiltins()
		{
			if (CodexCategoryRegistry.Instance.FindById("block") == null)
				CodexCategoryRegistry.Instance.Register(new BlockCodexCategory());
			if (CodexCategoryRegistry.Instance.FindById("item") == null)
				CodexCategoryRegistry.Instance.Register(new ItemCodexCategory());
			if (CodexCategoryRegistry.Instance.FindById("entity") == null)
				CodexCategoryRegistry.Instance.Register(new EntityCodexCategory());
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

		private void OnCategorySelected(ICodexCategory category) => view.SetActiveCategory(category);
		private void OnEntrySelected(CodexEntry entry) => view.SetActiveEntry(entry);
	}
}
