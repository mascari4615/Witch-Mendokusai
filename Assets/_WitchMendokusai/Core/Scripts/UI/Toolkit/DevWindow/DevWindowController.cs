using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 진입점. 윈도우 생성·마운트, 모드/명령 등록, / 토글 입력 연동, 명령 catch.
	/// 모든 모드와 명령은 이 컨트롤러가 시작 시 일괄 Register.
	/// World 씬 진입 시 Resources/Singletons/DevWindowController.prefab 에서 자동 스폰.
	/// </summary>
	public class DevWindowController : Singleton<DevWindowController>
	{
		private const string WINDOW_ID = "DevWindow";
		private const string WINDOW_TITLE = "WM Dev";
		private const string WORLD_SCENE_NAME = "World";

		private WMWindow window;
		private DevWindowView view;

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
			view.RebuildSidebar(DevModeRegistry.Instance.Modes);

			IDevMode firstMode = DevModeRegistry.Instance.FindById("console");
			if (firstMode != null)
				view.SetActiveMode(firstMode);

			view.OnModeSelected += OnModeSelected;
			view.CommandLine.OnSubmit += OnCommandSubmit;

			InputManager.Instance.RegisterInputEvent(InputEventType.DevWindowToggle, InputEventResponseType.Performed, OnToggle);
		}

		protected override void OnDestroy()
		{
			if (InputManager.TryGetExistingInstance(out InputManager inputManager))
				inputManager.UnregisterInputEvent(InputEventType.DevWindowToggle, InputEventResponseType.Performed, OnToggle);

			base.OnDestroy();
		}

		/// <summary>GameCondition.IsTyping 이 호출. 명령행에 포커스가 있으면 true.</summary>
		public bool IsCommandLineFocused => view != null && view.CommandLine != null && view.CommandLine.HasInputFocus;

		private void BuildWindow()
		{
			window = new WMWindow
			{
				WindowId = WINDOW_ID,
				Title = WINDOW_TITLE,
			};
			window.style.left = 60;
			window.style.top = 60;
			window.style.width = 720;
			window.style.height = 520;

			view = new DevWindowView();
			view.style.flexGrow = 1;
			window.Content.Add(view);

			UIRoot.Instance.WindowsLayer.Add(window);
			// dropdown 은 OverlayLayer 에 (normal flow 밖, 다른 UI 위에 떠있음).
			UIRoot.Instance.OverlayLayer.Add(view.CommandLine.Dropdown);
		}

		private void RegisterBuiltins()
		{
			DevCommandRegistry.Instance.Register(new HelpCommand());
			DevCommandRegistry.Instance.Register(new ClearCommand());

			if (DevModeRegistry.Instance.FindById("console") == null)
				DevModeRegistry.Instance.Register(new ConsoleMode());
		}

		private void OnModeSelected(IDevMode mode) => view.SetActiveMode(mode);

		private void OnToggle()
		{
			if (window.IsOpen && view.CommandLine.HasInputFocus)
				return;

			if (window.IsOpen)
			{
				window.Close();
				return;
			}

			window.Open();
			view.CommandLine.FocusInput();
		}

		private void OnCommandSubmit(string text)
		{
			DevCommandContext context = new(view.Console, text);

			if (DevCommandParser.TryParse(text, out string commandName, out string[] args) == false)
				return;

			context.LogInfo($"> {text}");

			if (DevCommandRegistry.Instance.TryGet(commandName, out IDevCommand command) == false)
			{
				context.LogError($"알 수 없는 명령: {commandName}");
				return;
			}

			try
			{
				command.Execute(context, args);
			}
			catch (System.Exception exception)
			{
				context.LogError($"명령 실행 중 예외: {exception.Message}");
				Debug.LogException(exception);
			}
		}
	}
}
