using UnityEngine;
using UnityEngine.InputSystem;

namespace WitchMendokusai
{
	public class UGCDevSampleRunner : MonoBehaviour
	{
		[SerializeField] private bool loadOnStart = true;
		[SerializeField] private bool ignoreConditionsInDev = false;
		[SerializeField] private bool showDebugGui = false;
		[SerializeField] private bool verboseInputLog = true;
		[SerializeField] private string manifestFileName = "wm_jump_001.manifest.json";
		[SerializeField] private string triggerFileName = "wm_jump_001.triggers.json";

		private readonly UGCRuntimeSession session = new();
		private string lastStatus = "Not loaded";
		private float nextInputHeartbeatTime;
		private Vector2 guiScroll = Vector2.zero;

		private InputAction actionToggleGui;
		private InputAction actionReload;
		private InputAction actionDoor;
		private InputAction actionPlatform;
		private InputAction actionCheckpoint;
		private InputAction actionHazard;

		private void OnEnable()
		{
			EnsureInputActions();
			actionToggleGui.Enable();
			actionReload.Enable();
			actionDoor.Enable();
			actionPlatform.Enable();
			actionCheckpoint.Enable();
			actionHazard.Enable();
			LogInputBackend();
		}

		private void OnDisable()
		{
			actionToggleGui?.Disable();
			actionReload?.Disable();
			actionDoor?.Disable();
			actionPlatform?.Disable();
			actionCheckpoint?.Disable();
			actionHazard?.Disable();
		}

		private void Start()
		{
			if (loadOnStart)
				LoadSamples();
		}

		private void Update()
		{
			LogInputHeartbeat();

			if (actionToggleGui.WasPressedThisFrame())
			{
				LogHotkey("ToggleGui", "F1");
				showDebugGui = !showDebugGui;
			}

			if (actionReload.WasPressedThisFrame())
			{
				LogHotkey("Reload", "F5/R");
				LoadSamples();
			}

			if (actionDoor.WasPressedThisFrame())
			{
				LogHotkey("Door", "F6/6");
				RunOpenGate();
			}

			if (actionPlatform.WasPressedThisFrame())
			{
				LogHotkey("Platform", "F7/7");
				RunStartPlatform();
			}

			if (actionCheckpoint.WasPressedThisFrame())
			{
				LogHotkey("Checkpoint", "F8/8");
				RunCheckpoint();
			}

			if (actionHazard.WasPressedThisFrame())
			{
				LogHotkey("Hazard", "F9/9");
				RunHazardToggle();
			}
		}

		private void LogInputBackend()
		{
			if (!verboseInputLog)
				return;

			Debug.Log($"[UGC][Input] Runner enabled. scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}, inputSystem=on, legacy=off");
		}

		private void LogInputHeartbeat()
		{
			if (!verboseInputLog || Time.unscaledTime < nextInputHeartbeatTime)
				return;

			nextInputHeartbeatTime = Time.unscaledTime + 2f;

			bool anyKey = InputManager.Instance.IsAnyKeyPressedThisFrame;
			// Debug.Log($"[UGC][Input] heartbeat keyboardPresent={keyboardPresent}, anyKeyThisFrame={anyKey}, focus={Application.isFocused}");
		}

		private void LogHotkey(string action, string key)
		{
			if (!verboseInputLog)
				return;

			Debug.Log($"[UGC][Input] hotkey action={action}, key={key}");
		}

		private void EnsureInputActions()
		{
			actionToggleGui ??= CreateKeyboardAction("ToggleGui", "<Keyboard>/f1");
			actionReload ??= CreateKeyboardAction("Reload", "<Keyboard>/f5", "<Keyboard>/r");
			actionDoor ??= CreateKeyboardAction("Door", "<Keyboard>/f6", "<Keyboard>/digit6", "<Keyboard>/numpad6");
			actionPlatform ??= CreateKeyboardAction("Platform", "<Keyboard>/f7", "<Keyboard>/digit7", "<Keyboard>/numpad7");
			actionCheckpoint ??= CreateKeyboardAction("Checkpoint", "<Keyboard>/f8", "<Keyboard>/digit8", "<Keyboard>/numpad8");
			actionHazard ??= CreateKeyboardAction("Hazard", "<Keyboard>/f9", "<Keyboard>/digit9", "<Keyboard>/numpad9");
		}

		private static InputAction CreateKeyboardAction(string name, params string[] bindings)
		{
			InputAction action = new InputAction(name, InputActionType.Button);
			for (int i = 0; i < bindings.Length; i++)
				action.AddBinding(bindings[i]);

			return action;
		}

		public void Setup(string manifest, string triggers, bool ignoreConditions)
		{
			manifestFileName = manifest;
			triggerFileName = triggers;
			ignoreConditionsInDev = ignoreConditions;
		}

		[ContextMenu("UGC/Load Samples")]
		public void LoadSamples()
		{
			if (session.TryLoadSamples(manifestFileName, triggerFileName, out string error))
			{
				lastStatus = $"Loaded: {session.Manifest?.mapId} ({session.TriggerMap.Count} triggers)";
				UGCLog.Info($"Samples loaded. mapId={session.Manifest?.mapId}, triggers={session.TriggerMap.Count}");
			}
			else
			{
				lastStatus = $"Load failed: {error}";
				UGCLog.Error($"Sample load failed: {error}");
			}
		}

		private void OnGUI()
		{
			GUIStyle toggleButtonStyle = new GUIStyle(GUI.skin.button)
			{
				fontSize = 16,
				fontStyle = FontStyle.Bold,
				fixedHeight = 32f,
				fixedWidth = 120f,
			};
			toggleButtonStyle.normal.textColor = showDebugGui ? new Color(1f, 0.86f, 0.45f) : new Color(0.6f, 1f, 0.6f);

			if (GUI.Button(new Rect(12, 12, 120f, 32f), showDebugGui ? "▼ UGC DEV" : "▶ UGC DEV", toggleButtonStyle))
				showDebugGui = !showDebugGui;

			if (!showDebugGui)
				return;

			const int width = 1040;
			const int height = 640;
			Rect area = new Rect(12, 48, width, height);
			GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
			{
				fontSize = 28,
				alignment = TextAnchor.UpperLeft,
				fontStyle = FontStyle.Bold,
				padding = new RectOffset(20, 20, 20, 20),
			};
			GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 20,
				fontStyle = FontStyle.Bold,
				wordWrap = true,
			};
			GUIStyle bigButtonStyle = new GUIStyle(GUI.skin.button)
			{
				fontSize = 19,
				fontStyle = FontStyle.Bold,
				fixedHeight = 48f,
			};
			GUIStyle normalButtonStyle = new GUIStyle(bigButtonStyle);
			normalButtonStyle.normal.textColor = new Color(0.85f, 0.95f, 1f);
			GUIStyle forceButtonStyle = new GUIStyle(bigButtonStyle);
			forceButtonStyle.normal.textColor = new Color(1f, 0.86f, 0.45f);
			GUIStyle helpBoxStyle = new GUIStyle(GUI.skin.box)
			{
				fontSize = 18,
				fontStyle = FontStyle.Normal,
				wordWrap = true,
				alignment = TextAnchor.UpperLeft,
				padding = new RectOffset(16, 16, 14, 14),
			};
			GUIStyle sectionHeaderStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 19,
				fontStyle = FontStyle.Bold,
			};
			GUIStyle focusStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 18,
				fontStyle = FontStyle.Bold,
				normal = { textColor = Application.isFocused ? new Color(0.25f, 0.9f, 0.35f) : new Color(1f, 0.4f, 0.35f) },
			};
			GUIStyle okStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 18,
				fontStyle = FontStyle.Bold,
			};
			okStyle.normal.textColor = lastStatus.StartsWith("FORCE OK") || lastStatus.StartsWith("OK") ? new Color(0.2f, 0.9f, 0.35f) : new Color(1f, 0.85f, 0.2f);

			GUI.Box(area, "UGC DEV TEST PANEL", boxStyle);

			Rect scrollRect = new Rect(area.x + 12f, area.y + 48f, area.width - 24f, area.height - 60f);
			Rect contentRect = new Rect(0f, 0f, scrollRect.width - 22f, 760f);
			guiScroll = GUI.BeginScrollView(scrollRect, guiScroll, contentRect);

			float x = 8f;
			float y = 8f;
			GUI.Label(new Rect(x, y, contentRect.width - 16f, 30f), $"맵: {session.Manifest?.mapId ?? "(아직 로드 안 됨)"}", labelStyle);
			y += 38f;
			GUI.Label(new Rect(x, y, contentRect.width - 16f, 30f), $"트리거 수: {session.TriggerMap.Count}", labelStyle);
			y += 38f;

			Rect helpRect = new Rect(x, y, contentRect.width - 16f, 122f);
			GUI.Box(helpRect, "", helpBoxStyle);
			GUI.Label(helpRect,
				"빠른 테스트 순서\n" +
				"1. Reload -> 시작존(zone_start_01)으로 이동\n" +
				"2. 문1은 시작존에 들어가면 자동 오픈(evt_open_gate_001)\n" +
				"3. 안 열리면 Force 문 열기 버튼 또는 F6/6\n" +
				"4. 플랫폼/위험물/체크포인트는 각 존 진입으로 발동\n" +
				"5. 전체 루트: 시작존 -> 문1 -> 플랫폼 -> 위험구간 -> 문2 -> 체크포인트3 -> 탈출플랫폼 -> 최종체크포인트4\n" +
				"단, once / cooldown / 타겟 없음 같은 제한은 여전히 걸릴 수 있습니다.",
				helpBoxStyle);
			y += 138f;

			GUI.Label(new Rect(x, y, contentRect.width - 16f, 28f), $"포커스 상태: {(Application.isFocused ? "Game 뷰 포커스 있음" : "Game 뷰 포커스 없음")}", focusStyle);
			y += 32f;

			ignoreConditionsInDev = GUI.Toggle(new Rect(x, y, contentRect.width - 16f, 28f), ignoreConditionsInDev, "개발 모드: 조건 무시하고 실행(Force)");
			y += 34f;
			GUI.Label(new Rect(x, y, contentRect.width - 16f, 30f), "단축키: F6/6 문, F7/7 플랫폼, F8/8 체크포인트, F9/9 위험물", labelStyle);
			y += 34f;
			GUI.Label(new Rect(x, y, contentRect.width - 16f, 30f), "문 열기 핵심: 시작존 진입(일반) 또는 Force 문 열기/F6(강제)", labelStyle);
			y += 34f;
			GUI.Label(new Rect(x, y, contentRect.width - 16f, 48f), $"마지막 결과: {lastStatus}", okStyle);
			y += 60f;

			Rect normalSection = new Rect(x, y, contentRect.width - 16f, 118f);
			GUI.Box(normalSection, "", helpBoxStyle);
			GUI.Label(new Rect(normalSection.x + 12f, normalSection.y + 10f, normalSection.width - 24f, 24f), "일반 실행", sectionHeaderStyle);
			GUI.Label(new Rect(normalSection.x + 12f, normalSection.y + 38f, normalSection.width - 24f, 24f), "존에 들어가야 반응하는 실제 진행 확인용입니다.", labelStyle);

			Rect forceSection = new Rect(x, y + 132f, contentRect.width - 16f, 118f);
			GUI.Box(forceSection, "", helpBoxStyle);
			GUI.Label(new Rect(forceSection.x + 12f, forceSection.y + 10f, forceSection.width - 24f, 24f), "Force 실행", sectionHeaderStyle);
			GUI.Label(new Rect(forceSection.x + 12f, forceSection.y + 38f, forceSection.width - 24f, 24f), "조건을 건너뛰고 액션 반응만 바로 보는 확인용입니다.", labelStyle);

			y += 264f;

			float buttonX = x;
			float buttonWidth = 188f;
			float buttonGap = 16f;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "Reload", normalButtonStyle))
				LoadSamples();
			buttonX += buttonWidth + buttonGap;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "문 열기", normalButtonStyle))
				RunOpenGate();
			buttonX += buttonWidth + buttonGap;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "플랫폼 시작", normalButtonStyle))
				RunStartPlatform();
			buttonX += buttonWidth + buttonGap;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "체크포인트", normalButtonStyle))
				RunCheckpoint();
			buttonX += buttonWidth + buttonGap;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "위험물 토글", normalButtonStyle))
				RunHazardToggle();

			y += 58f;
			buttonX = x;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "Force 문 열기", forceButtonStyle))
				RunOpenGate(true);
			buttonX += buttonWidth + buttonGap;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "Force 플랫폼", forceButtonStyle))
				RunStartPlatform(true);
			buttonX += buttonWidth + buttonGap;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "Force 체크포인트", forceButtonStyle))
				RunCheckpoint(true);
			buttonX += buttonWidth + buttonGap;
			if (GUI.Button(new Rect(buttonX, y, buttonWidth, 48f), "Force 위험물", forceButtonStyle))
				RunHazardToggle(true);

			GUI.EndScrollView();
		}

		[ContextMenu("UGC/Run evt_open_gate_001")]
		public void RunOpenGate()
		{
			RunTrigger("evt_open_gate_001");
		}

		public void RunOpenGate(bool force)
		{
			RunTrigger("evt_open_gate_001", force);
		}

		[ContextMenu("UGC/Run evt_start_platform_001")]
		public void RunStartPlatform()
		{
			RunTrigger("evt_start_platform_001");
		}

		public void RunStartPlatform(bool force)
		{
			RunTrigger("evt_start_platform_001", force);
		}

		[ContextMenu("UGC/Run evt_checkpoint_003")]
		public void RunCheckpoint()
		{
			RunTrigger("evt_checkpoint_003");
		}

		public void RunCheckpoint(bool force)
		{
			RunTrigger("evt_checkpoint_003", force);
		}

		[ContextMenu("UGC/Run evt_toggle_hazard_001")]
		public void RunHazardToggle()
		{
			RunTrigger("evt_toggle_hazard_001");
		}

		public void RunHazardToggle(bool force)
		{
			RunTrigger("evt_toggle_hazard_001", force);
		}

		public bool RunTrigger(string triggerId)
		{
			return RunTrigger(triggerId, ignoreConditionsInDev);
		}

		public bool RunTrigger(string triggerId, bool ignoreConditions)
		{
			if (session.TryExecuteTrigger(triggerId, ignoreConditions, out string error))
			{
				lastStatus = ignoreConditions ? $"FORCE OK: {triggerId}" : $"OK: {triggerId}";
				UGCLog.Info($"Trigger executed: {triggerId}");
				return true;
			}

			lastStatus = $"SKIP: {triggerId} ({error})";
			UGCLog.Warn($"Trigger execution skipped: {triggerId}, reason={error}");
			return false;
		}
	}
}
