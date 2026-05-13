using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// 개발자 윈도우 진입점. 윈도우 생성·마운트, 모드/명령 등록, / 토글 입력 연동, 명령 catch.
	/// 모든 모드와 명령은 이 컨트롤러가 시작 시 일괄 Register.
	/// World 씬 진입 시 Resources/Singletons/DevWindowController.prefab 에서 자동 스폰.
	/// </summary>
	public class DevWindowController : MonoBehaviour
	{
		public static DevWindowController Instance { get; private set; }

		public static bool TryGetExistingInstance(out DevWindowController mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		private const string WINDOW_ID = "DevWindow";
		private const string WINDOW_TITLE = "WM Dev";

		private InputManager inputManager;
		private UIRoot uiRoot;
		private WMWindow window;
		private DevWindowView view;

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
			view.RebuildSidebar(DevModeRegistry.Instance.Modes);

			IDevMode firstMode = DevModeRegistry.Instance.FindById("console");
			if (firstMode != null)
				view.SetActiveMode(firstMode);

			view.OnModeSelected += OnModeSelected;
			view.CommandLine.OnSubmit += OnCommandSubmit;

			inputManager.RegisterInputEvent(InputEventType.DevWindowToggle, InputEventResponseType.Performed, OnToggle);
		}

		private void OnDestroy()
		{
			if (inputManager != null)
				inputManager.UnregisterInputEvent(InputEventType.DevWindowToggle, InputEventResponseType.Performed, OnToggle);

			if (Instance == this)
				Instance = null;
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
			window.style.width = 900;
			window.style.height = 640;

			view = new DevWindowView();
			view.style.flexGrow = 1;
			window.Content.Add(view);

			uiRoot.WindowsLayer.Add(window);
			// dropdown 은 OverlayLayer 에 (normal flow 밖, 다른 UI 위에 떠있음).
			uiRoot.OverlayLayer.Add(view.CommandLine.Dropdown);
		}

		private void RegisterBuiltins()
		{
			DevCommandRegistry.Instance.Register(new HelpCommand());
			DevCommandRegistry.Instance.Register(new ClearCommand());
			DevCommandRegistry.Instance.Register(new GiveItemCommand());
			DevCommandRegistry.Instance.Register(new SpawnMonsterCommand());
			DevCommandRegistry.Instance.Register(new StartDungeonCommand());
			DevCommandRegistry.Instance.Register(new UnlockQuestCommand());

			if (DevModeRegistry.Instance.FindById("console") == null)
				DevModeRegistry.Instance.Register(new ConsoleMode());
			if (DevModeRegistry.Instance.FindById("items") == null)
				DevModeRegistry.Instance.Register(new ItemsMode());
			if (DevModeRegistry.Instance.FindById("mobs") == null)
				DevModeRegistry.Instance.Register(new DevDataListMode<Monster>("mobs", "Mobs", "M_", "spawn"));
			if (DevModeRegistry.Instance.FindById("stages") == null)
				DevModeRegistry.Instance.Register(new DevDataListMode<Dungeon>("stages", "Stages", "D_", "dungeon"));
			if (DevModeRegistry.Instance.FindById("quests") == null)
				DevModeRegistry.Instance.Register(new DevDataListMode<QuestSO>("quests", "Quests", "Q_", "quest", "unlock"));
			if (DevModeRegistry.Instance.FindById("timeweather") == null)
				DevModeRegistry.Instance.Register(new TimeWeatherMode());
		}

		/// <summary>UI 측에서 명령 시스템에 진입할 때 호출. 명령행에 직접 입력한 것과 동일한 경로 (출력/에러 처리 포함).</summary>
		public void InvokeCommand(string commandName, params string[] args)
		{
			string text = args.Length == 0 ? commandName : $"{commandName} {string.Join(' ', args)}";
			OnCommandSubmit(text);
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
