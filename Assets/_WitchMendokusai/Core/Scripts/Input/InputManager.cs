using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public enum InputMapType
	{
		Player,
		UI,
	}

	public enum InputEventType
	{
		// Player
		[InputEvent("이동", "스킬", "<Keyboard>/leftShift")]
		Space,
		[InputEvent("이동", "점프", "<Keyboard>/space")]
		Jump,
		[InputEvent("전투", "기본 공격", "<Mouse>/leftButton")]
		Click0,
		[InputEvent("전투", "보조 공격", "<Mouse>/rightButton")]
		Click1,
		[InputEvent("전투", "조준 모드 전환", "<Keyboard>/y")]
		ChangeMode,
		[InputEvent("월드", "스크롤", "<Mouse>/scroll")]
		Scroll,
		[InputEvent("이동", "달리기", "<Keyboard>/ctrl")]
		Sprint,
		[InputEvent("이동", "앉기", "<Keyboard>/c")]
		Crouch,
		[InputEvent("월드", "건축 모드", "<Keyboard>/g")]
		BuildModeToggle,
		// HotbarSlot1~9는 연속 정의 유지 — UIHotbar이 (HotbarSlot1 + i) 산수에 의존
		[InputEvent("핫바", "핫바 슬롯 1", "<Keyboard>/1")]
		HotbarSlot1,
		[InputEvent("핫바", "핫바 슬롯 2", "<Keyboard>/2")]
		HotbarSlot2,
		[InputEvent("핫바", "핫바 슬롯 3", "<Keyboard>/3")]
		HotbarSlot3,
		[InputEvent("핫바", "핫바 슬롯 4", "<Keyboard>/4")]
		HotbarSlot4,
		[InputEvent("핫바", "핫바 슬롯 5", "<Keyboard>/5")]
		HotbarSlot5,
		[InputEvent("핫바", "핫바 슬롯 6", "<Keyboard>/6")]
		HotbarSlot6,
		[InputEvent("핫바", "핫바 슬롯 7", "<Keyboard>/7")]
		HotbarSlot7,
		[InputEvent("핫바", "핫바 슬롯 8", "<Keyboard>/8")]
		HotbarSlot8,
		[InputEvent("핫바", "핫바 슬롯 9", "<Keyboard>/9")]
		HotbarSlot9,

		// UI
		[InputEvent("UI 탐색", "확인", "<Keyboard>/z")]
		Submit,
		[InputEvent("UI 탐색", "취소", "<Keyboard>/x")]
		Cancel,
		[InputEvent("카메라", "시점 조작 모드 (Tab)", "<Keyboard>/tab")]
		CameraControlModeToggle,
		[InputEvent("카메라", "1인칭/3인칭 (F5)", "<Keyboard>/f5")]
		CameraPerspectiveToggle,
		[InputEvent("카메라", "시점 순환 (F6)", "<Keyboard>/f6")]
		CameraViewCycle,
		[InputEvent("창", "스탯", "<Keyboard>/v")]
		Status,
		[InputEvent("창", "인벤토리", "<Keyboard>/i")]
		Inventory,
		[InputEvent("창", "개발자 창", "<Keyboard>/slash")]
		DevWindowToggle,
		[InputEvent("창", "도감", "<Keyboard>/b")]
		CodexToggle,
		[InputEvent("창", "퀘스트", "<Keyboard>/j")]
		QuestToggle,
		[InputEvent("창", "인형", "<Keyboard>/k")]
		DollToggle,
		[InputEvent("창", "단축키 안내", "<Keyboard>/f1")]
		KeybindHelpToggle,
		[InputEvent("창", "마도서", "<Keyboard>/m")]
		MagicBookToggle,
		[InputEvent("창", "솥 지도", "<Keyboard>/n")]
		CauldronMapToggle,
	}

	public enum InputEventResponseType
	{
		Started,
		Performed,
		Canceled,
		Get, // Custom
	}

	public enum InputAxisType
	{
		Move,
		CameraRotate,
		Look,
		// TASK-WM-193 — 자유 위치 카메라 전용 축 (플레이어 Move 와 분리, 모드별 배타 라우팅).
		CameraMove,      // 부감 pan / 자유비행 수평 (WASD raw)
		CameraVertical,  // 자유비행 상하 (Space=상승 / Shift=하강)
		ScrollWheel,     // 부감 높이 줌 (스크롤 델타)
	}

	public class InputManager : MonoBehaviour
	{
		public static InputManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out InputManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		[SerializeField] private InputActionAsset inputActionAsset;
		[SerializeField] private LayerMask mouseWorldLayerMask;
		[SerializeField] private float mouseWorldRayDistance = 100f;

		// TASK-WM-200 — 손가락 몸짓 문턱값. 화면 밀도가 다른 기기에서 다시 재야 해서 꺼내 둔다.
		[Header("모바일 — 손가락 몸짓")]
		[SerializeField] private float tapMaxSeconds = 0.35f;
		[SerializeField] private float tapMaxTravelPixels = 24f;
		[SerializeField] private float dragSlopPixels = 12f;
		[SerializeField] private float pinchToZoomScale = 0.5f;

		/// <summary>
		/// 가리키는 것 하나 — 마우스/손가락을 같은 얼굴로 만든다 (TASK-WM-200).
		/// 아래 Mouse* 프로퍼티들은 전부 이걸 통해서 나간다. 「마우스」라는 이름은 유지하는데,
		/// 부르는 쪽 300곳의 뜻이 원래부터 「가리키는 자리」였기 때문이다(이름만 마우스였다).
		/// </summary>
		private readonly PointerDevice pointer = new();
		private readonly Dictionary<InputEventType, InputMapType> inputEventBindings = new()
		{
			{ InputEventType.Space, InputMapType.Player },
			{ InputEventType.Jump, InputMapType.Player },
			{ InputEventType.Click0, InputMapType.Player },
			{ InputEventType.Click1, InputMapType.Player },
			{ InputEventType.ChangeMode, InputMapType.Player },
			{ InputEventType.Scroll, InputMapType.Player },
			{ InputEventType.Sprint, InputMapType.Player },
			{ InputEventType.Crouch, InputMapType.Player },
			{ InputEventType.BuildModeToggle, InputMapType.Player },
			{ InputEventType.HotbarSlot1, InputMapType.Player },
			{ InputEventType.HotbarSlot2, InputMapType.Player },
			{ InputEventType.HotbarSlot3, InputMapType.Player },
			{ InputEventType.HotbarSlot4, InputMapType.Player },
			{ InputEventType.HotbarSlot5, InputMapType.Player },
			{ InputEventType.HotbarSlot6, InputMapType.Player },
			{ InputEventType.HotbarSlot7, InputMapType.Player },
			{ InputEventType.HotbarSlot8, InputMapType.Player },
			{ InputEventType.HotbarSlot9, InputMapType.Player },

			{ InputEventType.Submit, InputMapType.UI },
			{ InputEventType.Cancel, InputMapType.UI },
			{ InputEventType.CameraControlModeToggle, InputMapType.UI },
			{ InputEventType.CameraPerspectiveToggle, InputMapType.UI },
			{ InputEventType.CameraViewCycle, InputMapType.UI },
			{ InputEventType.Status, InputMapType.UI },
			{ InputEventType.Inventory, InputMapType.UI },
			{ InputEventType.DevWindowToggle, InputMapType.UI },
			{ InputEventType.CodexToggle, InputMapType.UI },
			{ InputEventType.QuestToggle, InputMapType.UI },
			{ InputEventType.DollToggle, InputMapType.UI },
			{ InputEventType.KeybindHelpToggle, InputMapType.UI },
			{ InputEventType.MagicBookToggle, InputMapType.UI },
			{ InputEventType.CauldronMapToggle, InputMapType.UI },
		};

		// Strategy-owned: cleared on every strategy switch
		private readonly Dictionary<(InputEventType, InputEventResponseType), Action<InputAction.CallbackContext>> strategyEventsWithContext = new();
		private readonly Dictionary<(InputEventType, InputEventResponseType), List<(Action action, Func<bool> condition)>> strategyEvents = new();

		// Component-owned: never touched by strategy management
		private readonly Dictionary<(InputEventType, InputEventResponseType), Action<InputAction.CallbackContext>> componentEventsWithContext = new();
		private readonly Dictionary<(InputEventType, InputEventResponseType), List<(Action action, Func<bool> condition)>> componentEvents = new();

		private readonly Dictionary<InputEventType, bool> isPressed = new();

		// IsTyping 글로벌 게이트 화이트리스트 — 텍스트 입력 중에도 항상 통과 (창 닫기·토글 escape).
		private static readonly HashSet<InputEventType> ALWAYS_DISPATCH_WHILE_TYPING = new()
		{
			InputEventType.Cancel,
			InputEventType.DevWindowToggle,
		};

		public Vector3 MouseWorldPosition { get; private set; }
		// TASK-WM-181 INC-2 — 마우스 ray 가 맞은 표면의 법선. 마크식 면-인접 배치(빌더가 hit+normal 로 인접 셀 계산)용.
		// 히트 없으면 Vector3.up (지면 위 폴백).
		public Vector3 MouseWorldNormal { get; private set; } = Vector3.up;
		public Vector2 MouseScreenPosition { get; private set; }
		public bool IsAnyKeyPressedThisFrame => Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
		// TASK-WM-135 — Mouse.current 직접 접근 캡슐화 (DollAnimator 폴링 / 잔존 null guard 정리).
		// TASK-WM-200 — 그 캡슐 안쪽을 PointerDevice 로 갈아끼웠다. 손가락도 여기로 들어온다.
		public bool IsMouseAvailable => Mouse.current != null || Touchscreen.current != null;
		public bool IsMouseLeftButtonPressed => pointer.IsPressed;
		public bool IsMouseRightButtonPressed => pointer.IsSecondaryPressed;

		// TASK-WM-200 — 모바일 조작. 「지금 손가락인가」는 기기 종류가 아니라 *마지막으로 만진 장치*다
		// (터치 노트북에서 기기로 판정하면 마우스를 쥐고도 손가락 UI 가 뜬다).
		public bool IsTouchMode => pointer.IsTouchMode;
		public bool IsPointerPressed => pointer.IsPressed;
		public bool PointerTappedThisFrame => pointer.TappedThisFrame;
		public Vector2 PointerTapPosition => pointer.TapPosition;
		public bool IsPointerDragging => pointer.IsDragging;
		public Vector2 PointerDragDelta => pointer.DragDelta;
		public Vector2 PointerTwoFingerPanDelta => pointer.TwoFingerPanDelta;
		public float PointerTwistDelta => pointer.TwistDelta;
		public Vector2 MoveInput { get; private set; }
		public float CameraRotateInput { get; private set; }
		// TASK-WM-163 — MouseLook 모드 시야 회전용 마우스 델타 (픽셀/프레임).
		// 캡슐화 경계(InputManager) 내부에서 Mouse.current 직접 read — UpdateMoveInput 패턴과 동일.
		public Vector2 LookDelta { get; private set; }
		// TASK-WM-193 — 자유 위치 카메라 전용 축 (플레이어 Move/Jump 와 분리).
		public Vector2 CameraMoveInput { get; private set; }
		public float CameraVerticalInput { get; private set; }
		public float ScrollWheelDelta { get; private set; }
		// 자유 카메라 가속 (Ctrl) — 캐릭터 sprint 와 동일 키 직관.
		public bool IsCameraBoost { get; private set; }
		private IInputStrategy CurrentInputStrategy { get; set; }

		// Calling IsPointerOverGameObject() from within event processing (such as from InputAction callbacks) will not work as expected; it will query UI state from the last frame UnityEngine.EventSystems.EventSystem:IsPointerOverGameObject ()
		// public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

		private bool isPointerOverUI;
		public bool IsPointerOverUI() => isPointerOverUI;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
			Init();
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		private void Init()
		{
			inputActionAsset.Enable();

			InitEventDictionaries();
			BindEvents();

			KeybindRegistry.ValidateAgainstAsset(inputActionAsset);
			SetInputStrategy(new InputStrategyLoading());
		}

		private void InitEventDictionaries()
		{
			foreach (InputEventType inputEventType in Enum.GetValues(typeof(InputEventType)))
			{
				foreach (InputEventResponseType inputEventResponseType in Enum.GetValues(typeof(InputEventResponseType)))
				{
					strategyEventsWithContext[(inputEventType, inputEventResponseType)] = delegate { };
					strategyEvents[(inputEventType, inputEventResponseType)] = new();
					componentEventsWithContext[(inputEventType, inputEventResponseType)] = delegate { };
					componentEvents[(inputEventType, inputEventResponseType)] = new();
					isPressed[inputEventType] = false;
				}
			}
		}

		private void ClearStrategyEvents()
		{
			foreach (InputEventType inputEventType in Enum.GetValues(typeof(InputEventType)))
			{
				foreach (InputEventResponseType inputEventResponseType in Enum.GetValues(typeof(InputEventResponseType)))
				{
					strategyEventsWithContext[(inputEventType, inputEventResponseType)] = delegate { };
					strategyEvents[(inputEventType, inputEventResponseType)] = new();
					isPressed[inputEventType] = false;
				}
			}
		}

		public void SetInputStrategy(IInputStrategy inputStrategy)
		{
			inputActionAsset.Enable();

			CurrentInputStrategy = inputStrategy;

			ClearStrategyEvents();

			foreach (InputRegisterData data in CurrentInputStrategy.InputRegisterDataList)
			{
				if (data.CallbackWithContext != null)
					strategyEventsWithContext[(data.InputEventType, data.InputEventResponseType)] += data.CallbackWithContext;
				else
					strategyEvents[(data.InputEventType, data.InputEventResponseType)].Add((data.Callback, data.Condition));
			}
		}

		private void BindEvents()
		{
			foreach (InputEventType inputEventType in Enum.GetValues(typeof(InputEventType)))
			{
				BindEvent(inputEventType);
			}

			void BindEvent(InputEventType inputEventType)
			{
				InputMapType actionMapType = inputEventBindings[inputEventType];
				string actionName = $"{actionMapType}/{inputEventType}";

				inputActionAsset[actionName].started += ctx => OnEventStart(inputEventType, ctx);
				inputActionAsset[actionName].performed += ctx => OnEventPerformed(inputEventType, ctx);
				inputActionAsset[actionName].canceled += ctx => OnEventCanceled(inputEventType, ctx);
			}
		}

		private void OnEventStart(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			Dispatch(inputEventType, InputEventResponseType.Started, ctx);
			isPressed[inputEventType] = true;
			GetLoop(inputEventType).Forget();
		}

		private async UniTaskVoid GetLoop(InputEventType inputEventType = InputEventType.Space)
		{
			while (isPressed[inputEventType] == true)
			{
				await UniTask.Yield(PlayerLoopTiming.Update);
				Dispatch(inputEventType, InputEventResponseType.Get, default);
			}
		}

		private void OnEventPerformed(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			Dispatch(inputEventType, InputEventResponseType.Performed, ctx);
		}

		private void OnEventCanceled(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			Dispatch(inputEventType, InputEventResponseType.Canceled, ctx);
			isPressed[inputEventType] = false;
		}

		private void Dispatch(InputEventType inputEventType, InputEventResponseType responseType, InputAction.CallbackContext ctx)
		{
			if (GameConditionBridge.Get(GameConditionType.IsTyping)
				&& ALWAYS_DISPATCH_WHILE_TYPING.Contains(inputEventType) == false)
				return;

			(InputEventType, InputEventResponseType) key = (inputEventType, responseType);

			strategyEventsWithContext[key]?.Invoke(ctx);

			foreach ((Action action, Func<bool> condition) in strategyEvents[key])
				if (condition == null || condition()) action();

			componentEventsWithContext[key]?.Invoke(ctx);

			foreach ((Action action, Func<bool> condition) in componentEvents[key])
				if (condition == null || condition()) action();
		}

		/// <summary> 등록 — 칸이 아직 없으면 만들어서 넣는다(위 설명과 같은 이유). </summary>
		public void RegisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action<InputAction.CallbackContext> action)
		{
			(InputEventType, InputEventResponseType) key = (inputEventType, inputEventResponseType);
			if (componentEventsWithContext.ContainsKey(key) == false)
				componentEventsWithContext[key] = delegate { };
			componentEventsWithContext[key] += action;
		}


		/// <summary>
		/// 해제 — 그 칸이 이미 사라졌으면 아무 일도 아니다.
		///
		/// ★ 왜 여기만 관대한가: 모드가 바뀌면 입력 표가 통째로 새로 세워지고(SetInputStrategy),
		///   그 *뒤에* 옛 화면 컴포넌트들이 OnDestroy 에서 해제를 부른다 — 정상 생명주기다.
		///   여기서 터뜨리면 「없는 칸을 지우려 했다」는 소리가 화면 전환마다 쏟아진다(실측).
		///   등록은 그대로 FastFail 로 둔다 — *없는 칸에 넣는 것*은 진짜 실수다.
		/// </summary>
		public void UnregisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action<InputAction.CallbackContext> action)
		{
			if (componentEventsWithContext.ContainsKey((inputEventType, inputEventResponseType)) == false)
				return;
			componentEventsWithContext[(inputEventType, inputEventResponseType)] -= action;
		}

		/// <summary>
		/// 등록 — 칸이 아직 없으면 만들어서 넣는다.
		///
		/// ★ 왜 「없으면 만든다」인가: 칸을 미리 다 세우는 일은 InputManager 가 깨어날 때 한 번 일어나는데,
		///   화면 컴포넌트가 그보다 *먼저* 깨어나는 경우가 있다(DI 가 계층을 주입하는 순간). 그때 등록이
		///   터지면 「누가 먼저 깨어나나」에 기능이 걸린다 — 순서에 기대는 코드가 곧 init-order 지뢰다.
		///   칸은 그저 담을 자리라 미리 있든 그때 생기든 뜻이 같다.
		/// </summary>
		public void RegisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action action, Func<bool> condition = null)
		{
			(InputEventType, InputEventResponseType) key = (inputEventType, inputEventResponseType);
			if (componentEvents.TryGetValue(key, out List<(Action action, Func<bool> condition)> handlers) == false)
			{
				handlers = new List<(Action action, Func<bool> condition)>();
				componentEvents[key] = handlers;
			}
			handlers.Add((action, condition));
		}

		/// <summary> 해제 — 그 칸이 이미 사라졌으면 아무 일도 아니다(위 설명과 같은 이유). </summary>
		public void UnregisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action action, Func<bool> condition = null)
		{
			if (componentEvents.TryGetValue((inputEventType, inputEventResponseType), out List<(Action action, Func<bool> condition)> handlers) == false)
				return;
			handlers.RemoveAll(x => x.action == action && x.condition == condition);
		}

		private void Update()
		{
			UpdatePointer();
			UpdateMouseWorldPosition();
			UpdateIsPointerOverUI();
			UpdateMoveInput();
			UpdateCameraRotateInput();
			UpdateLookInput();
			UpdateCameraMoveInput();
			UpdateCameraVerticalInput();
			UpdateScrollWheelInput();
			UpdateCameraBoost();
		}

		// TASK-WM-200 — 장치를 읽는 유일한 자리. 시간은 unscaled 로 잰다(판이 멈춰도 손가락은 움직인다).
		private void UpdatePointer()
		{
			pointer.Tuning = new TouchGestureTuning
			{
				TapMaxSeconds = tapMaxSeconds,
				TapMaxTravelPixels = tapMaxTravelPixels,
				DragSlopPixels = dragSlopPixels,
			};
			pointer.PinchToZoomScale = pinchToZoomScale;
			pointer.Update(Time.unscaledDeltaTime);
		}

		private void UpdateMouseWorldPosition()
		{
			// 가리킨 자리는 카메라가 없어도 뜻이 있다(UI 는 있다) — 월드 좌표만 카메라에 걸린다.
			MouseScreenPosition = pointer.Position;

			// Loading 씬은 카메라가 없음 - 2025.08.08 20:24
			if (Camera.main == null)
			{
				MouseWorldPosition = Vector3.zero;
				return;
			}

			Vector2 mouseScreen = pointer.Position;
			Vector3 mousePos = new(mouseScreen.x, mouseScreen.y, Camera.main.nearClipPlane);
			Ray ray = Camera.main.ScreenPointToRay(mousePos);

			if (TryResolveMouseWorldHit(ray, out RaycastHit hit))
			{
				MouseWorldPosition = hit.point;
				MouseWorldNormal = hit.normal; // 면-인접 배치용 (빌더가 hit+normal 로 인접 셀 결정)
			}
			else
			{
				MouseWorldPosition = Vector3.zero;
				MouseWorldNormal = Vector3.up;
			}
		}

		private bool TryResolveMouseWorldHit(Ray ray, out RaycastHit hit)
		{
			hit = default;

			float distance = Mathf.Max(1f, mouseWorldRayDistance);

			if (mouseWorldLayerMask.value != 0)
				return Physics.Raycast(ray, out hit, distance, mouseWorldLayerMask, QueryTriggerInteraction.Ignore);

			RaycastHit[] hits = Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore);
			if (hits == null || hits.Length == 0)
				return false;

			Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

			for (int i = 0; i < hits.Length; i++)
			{
				GroundSurface surface = hits[i].collider.GetComponent<GroundSurface>();
				if (surface != null && surface.IsWalkable)
				{
					hit = hits[i];
					return true;
				}
			}

			hit = hits[0];
			return true;
		}

		private void UpdateIsPointerOverUI()
		{
			// Loading 씬은 EventSystem이 없음 - 2025.08.08 20:24
			if (EventSystem.current == null)
			{
				isPointerOverUI = false;
				return;
			}

			isPointerOverUI = EventSystem.current.IsPointerOverGameObject();
		}

		private void UpdateMoveInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.Move, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				MoveInput = Vector2.zero;
				return;
			}

			Keyboard kb = Keyboard.current;
			float h = 0f;
			float v = 0f;
			if (kb != null)
			{
				if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
				if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
				if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
				if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
			}

			if (h == 0)
				h = JoystickBridge.GetX();
			if (v == 0)
				v = JoystickBridge.GetY();

			MoveInput = new Vector2(h, v).normalized;
		}

		private void UpdateCameraRotateInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.CameraRotate, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				CameraRotateInput = 0f;
				return;
			}

			Keyboard kb = Keyboard.current;
			if (kb == null)
			{
				CameraRotateInput = 0f;
				return;
			}

			float rotate = 0f;
			if (kb.qKey.isPressed) rotate += 1f;
			if (kb.eKey.isPressed) rotate -= 1f;
			CameraRotateInput = rotate;
		}

		private void UpdateLookInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.Look, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				LookDelta = Vector2.zero;
				return;
			}

			LookDelta = pointer.LookDelta;
		}

		// TASK-WM-193 — 자유 위치 카메라 평면 이동 (WASD). 플레이어 Move 와 같은 물리 키지만 별도 축 —
		// 자유 카메라 모드에서 Move 는 차단(플레이어 정지)되고 이 축만 컨트롤러가 소비.
		private void UpdateCameraMoveInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.CameraMove, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				CameraMoveInput = Vector2.zero;
				return;
			}

			Keyboard kb = Keyboard.current;
			float h = 0f;
			float v = 0f;
			if (kb != null)
			{
				if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
				if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
				if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
				if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
			}

			CameraMoveInput = new Vector2(h, v).normalized;
		}

		// TASK-WM-193 — 자유비행 상하 이동 (Space=상승 / Shift=하강).
		private void UpdateCameraVerticalInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.CameraVertical, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				CameraVerticalInput = 0f;
				return;
			}

			Keyboard kb = Keyboard.current;
			float v = 0f;
			if (kb != null)
			{
				if (kb.spaceKey.isPressed) v += 1f;
				if (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) v -= 1f;
			}

			CameraVerticalInput = v;
		}

		// TASK-WM-193 — 부감 높이 줌 (스크롤 휠 델타). 자유 카메라 컨트롤러가 소비.
		private void UpdateScrollWheelInput()
		{
			if (CurrentInputStrategy != null &&
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.ScrollWheel, out GameConditionType[] conditions) &&
				GameConditionBridge.IsGameConditionAny(conditions))
			{
				ScrollWheelDelta = 0f;
				return;
			}

			// 손가락에선 오므리기가 곧 휠이다 — 부르는 쪽(부감 줌)은 어느 쪽인지 알 필요가 없다.
			ScrollWheelDelta = pointer.ZoomDelta;
		}

		// TASK-WM-193 — 자유 카메라 가속 (Ctrl). 캐릭터 sprint(ctrl) 와 동일 키라 직관적.
		private void UpdateCameraBoost()
		{
			Keyboard kb = Keyboard.current;
			IsCameraBoost = kb != null && kb.ctrlKey.isPressed;
		}
	}
}
