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
		Space,
		Click0,
		Click1,
		ChangeMode,
		Scroll,

		// UI
		Submit,
		Cancel,
		Tab,
		Status,
	}

	public enum InputEventResponseType
	{
		Started,
		Performed,
		Canceled,
		Get, // Custom
	}

	public class InputManager : Singleton<InputManager>
	{
		[SerializeField] private InputActionAsset inputActionAsset;
		private readonly Dictionary<InputEventType, (InputMapType inputMapType, GameConditionType gameConditionType)> inputEventBindings = new()
		{
			{ InputEventType.Space, (InputMapType.Player, GameConditionType.IsChatting) },
			{ InputEventType.Click0, (InputMapType.Player, GameConditionType.IsChatting) },
			{ InputEventType.Click1, (InputMapType.Player, GameConditionType.IsChatting) },
			{ InputEventType.ChangeMode, (InputMapType.Player, GameConditionType.IsChatting) },
			{ InputEventType.Scroll, (InputMapType.Player, GameConditionType.IsChatting) },

			{ InputEventType.Submit, (InputMapType.UI, GameConditionType.IsChatting) },
			{ InputEventType.Cancel, (InputMapType.UI, GameConditionType.IsChatting) },
			{ InputEventType.Tab, (InputMapType.UI, GameConditionType.IsChatting) },
			{ InputEventType.Status, (InputMapType.UI, GameConditionType.IsChatting) },
		};

		private readonly Dictionary<(InputEventType, InputEventResponseType), Action<InputAction.CallbackContext>> inputEventsWithContent = new();
		private readonly Dictionary<(InputEventType, InputEventResponseType), Action> inputEvents = new();
		private readonly Dictionary<InputEventType, bool> isPressed = new();

		public Vector3 MouseWorldPosition { get; private set; }

		// Calling IsPointerOverGameObject() from within event processing (such as from InputAction callbacks) will not work as expected; it will query UI state from the last frame UnityEngine.EventSystems.EventSystem:IsPointerOverGameObject ()
		// public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

		private bool isPointerOverUI;
		public bool IsPointerOverUI() => isPointerOverUI;

		protected override void Awake()
		{
			base.Awake();
			Init();
		}

		private void Init()
		{
			inputActionAsset.Enable();

			foreach (InputEventType inputEventType in Enum.GetValues(typeof(InputEventType)))
			{
				foreach (InputEventResponseType inputEventResponseType in Enum.GetValues(typeof(InputEventResponseType)))
				{
					inputEventsWithContent[(inputEventType, inputEventResponseType)] = delegate { };
					inputEvents[(inputEventType, inputEventResponseType)] = delegate { };
					isPressed[inputEventType] = false;
				}
			}

			BindEvents();
			InitEvents();
		}

		private void BindEvents()
		{
			foreach (InputEventType inputEventType in Enum.GetValues(typeof(InputEventType)))
			{
				BindEvent(inputEventType);
			}

			void BindEvent(InputEventType inputEventType)
			{
				(InputMapType actionMapType, GameConditionType returnCondition) = inputEventBindings[inputEventType];
				string actionName = $"{actionMapType}/{inputEventType}";

				inputActionAsset[actionName].started += ctx => OnEventStart(inputEventType, ctx);
				inputActionAsset[actionName].performed += ctx => OnEventPerformed(inputEventType, ctx);
				inputActionAsset[actionName].canceled += ctx => OnEventCanceled(inputEventType, ctx);
			}
		}

		private void OnEventStart(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			if (GameManager.Instance.Conditions.CheckGameCondition(inputEventBindings[inputEventType].gameConditionType) == false)
				return;

			inputEventsWithContent[(inputEventType, InputEventResponseType.Started)]?.Invoke(ctx);
			inputEvents[(inputEventType, InputEventResponseType.Started)]?.Invoke();

			isPressed[inputEventType] = true;
			GetLoop(inputEventType).Forget();
		}

		private async UniTaskVoid GetLoop(InputEventType inputEventType = InputEventType.Space)
		{
			(InputMapType actionMapType, GameConditionType returnCondition) = inputEventBindings[inputEventType];
			while (isPressed[inputEventType] == true)
			{
				await UniTask.Yield(PlayerLoopTiming.Update);

				if (GameManager.Instance.Conditions.CheckGameCondition(returnCondition) == false)
					continue;

				inputEventsWithContent[(inputEventType, InputEventResponseType.Get)]?.Invoke(default);
				inputEvents[(inputEventType, InputEventResponseType.Get)]?.Invoke();
			}
		}

		private void OnEventPerformed(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			if (GameManager.Instance.Conditions.CheckGameCondition(inputEventBindings[inputEventType].gameConditionType) == false)
				return;

			inputEventsWithContent[(inputEventType, InputEventResponseType.Performed)]?.Invoke(ctx);
			inputEvents[(inputEventType, InputEventResponseType.Performed)]?.Invoke();
		}

		private void OnEventCanceled(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			inputEventsWithContent[(inputEventType, InputEventResponseType.Canceled)]?.Invoke(ctx);
			inputEvents[(inputEventType, InputEventResponseType.Canceled)]?.Invoke();

			isPressed[inputEventType] = false;
		}

		private void InitEvents()
		{
			// Player
			{
				// TODO: Setup Class 같은 것이 있어야 할 듯 - 2025.04.19 11:38
				RegisterInputEvent(InputEventType.Space, InputEventResponseType.Performed, () => Player.Instance.TryUseSkill(0));
				RegisterInputEvent(InputEventType.Click0, InputEventResponseType.Get, () => TryUseSkill(1));
				RegisterInputEvent(InputEventType.Click1, InputEventResponseType.Get, () => TryUseSkill(2));

				static void TryUseSkill(int skillIndex)
				{
					GameConditionType skillCondition = GameConditionType.IsMouseOnUI | GameConditionType.IsChatting | GameConditionType.IsPaused | GameConditionType.IsDied | GameConditionType.IsBuilding;
					if (GameManager.Instance.Conditions.CheckGameCondition(skillCondition) == false)
						return;
					Player.Instance.TryUseSkill(skillIndex);
				}
			}

			// UI
			{
				RegisterInputEvent(InputEventType.ChangeMode, InputEventResponseType.Performed, () => Player.Instance.SetAutoAim(!Player.Instance.IsAutoAim));
				RegisterInputEvent(InputEventType.Submit, InputEventResponseType.Performed, Player.Instance.TryInteract);
				RegisterInputEvent(InputEventType.Cancel, InputEventResponseType.Performed, UIManager.Instance.ToggleOverlayUI_Setting);
				RegisterInputEvent(InputEventType.Tab, InputEventResponseType.Performed, UIManager.Instance.ToggleOverlayUI_Tab);
				RegisterInputEvent(InputEventType.Status, InputEventResponseType.Performed, UIManager.Instance.ToggleStatus);
			}
		}

		public void RegisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action<InputAction.CallbackContext> action)
		{
			inputEventsWithContent[(inputEventType, inputEventResponseType)] += action;
		}

		public void UnregisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action<InputAction.CallbackContext> action)
		{
			inputEventsWithContent[(inputEventType, inputEventResponseType)] -= action;
		}

		public void RegisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action action)
		{
			inputEvents[(inputEventType, inputEventResponseType)] += action;
		}

		public void UnregisterInputEvent(InputEventType inputEventType, InputEventResponseType inputEventResponseType, Action action)
		{
			inputEvents[(inputEventType, inputEventResponseType)] -= action;
		}

		private void Update()
		{
			UpdateMouseWorldPosition();
			isPointerOverUI = EventSystem.current.IsPointerOverGameObject();
		}

		private void UpdateMouseWorldPosition()
		{
			Vector3 mousePos = Input.mousePosition;
			mousePos.z = Camera.main.nearClipPlane;
			Ray ray = Camera.main.ScreenPointToRay(mousePos);

			if (Physics.Raycast(ray, out RaycastHit hit, 100, LayerMask.GetMask("GROUND")))
				MouseWorldPosition = hit.point;
			else
				MouseWorldPosition = Vector3.zero;
		}
	}
}