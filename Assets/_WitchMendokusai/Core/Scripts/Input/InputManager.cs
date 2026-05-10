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
		[InputEvent("창", "탭 UI", "<Keyboard>/tab")]
		Tab,
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
			{ InputEventType.Tab, InputMapType.UI },
			{ InputEventType.Status, InputMapType.UI },
			{ InputEventType.Inventory, InputMapType.UI },
			{ InputEventType.DevWindowToggle, InputMapType.UI },
			{ InputEventType.CodexToggle, InputMapType.UI },
			{ InputEventType.QuestToggle, InputMapType.UI },
			{ InputEventType.DollToggle, InputMapType.UI },
			{ InputEventType.KeybindHelpToggle, InputMapType.UI },
			{ InputEventType.MagicBookToggle, InputMapType.UI },
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
		public Vector2 MouseScreenPosition { get; private set; }
		public bool IsAnyKeyPressedThisFrame => Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
		public Vector2 MoveInput { get; private set; }
		public float CameraRotateInput { get; private set; }
		private IInputStrategy CurrentInputStrategy { get; set; }

		// Calling IsPointerOverGameObject() from within event processing (such as from InputAction callbacks) will not work as expected; it will query UI state from the last frame UnityEngine.EventSystems.EventSystem:IsPointerOverGameObject ()
		// public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

		private bool isPointerOverUI;
		public bool IsPointerOverUI() => isPointerOverUI;

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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

		public void RegisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action<InputAction.CallbackContext> action)
		{
			componentEventsWithContext[(inputEventType, inputEventResponseType)] += action;
		}

		public void UnregisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action<InputAction.CallbackContext> action)
		{
			componentEventsWithContext[(inputEventType, inputEventResponseType)] -= action;
		}

		public void RegisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action action, Func<bool> condition = null)
		{
			componentEvents[(inputEventType, inputEventResponseType)].Add((action, condition));
		}

		public void UnregisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action action, Func<bool> condition = null)
		{
			componentEvents[(inputEventType, inputEventResponseType)].RemoveAll(x => x.action == action && x.condition == condition);
		}

		private void Update()
		{
			UpdateMouseWorldPosition();
			UpdateIsPointerOverUI();
			UpdateMoveInput();
			UpdateCameraRotateInput();
		}

		private void UpdateMouseWorldPosition()
		{
			// Loading 씬은 카메라가 없음 - 2025.08.08 20:24
			if (Camera.main == null)
			{
				MouseWorldPosition = Vector3.zero;
				return;
			}

			Vector2 mouseScreen = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
			MouseScreenPosition = mouseScreen;
			Vector3 mousePos = new(mouseScreen.x, mouseScreen.y, Camera.main.nearClipPlane);
			Ray ray = Camera.main.ScreenPointToRay(mousePos);

			if (TryResolveMouseWorldHit(ray, out RaycastHit hit))
				MouseWorldPosition = hit.point;
			else
				MouseWorldPosition = Vector3.zero;
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
	}
}
