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

	public enum InputAxisType
	{
		Move,
	}

	public class InputManager : Singleton<InputManager>
	{
		[SerializeField] private InputActionAsset inputActionAsset;
		private readonly Dictionary<InputEventType, InputMapType> inputEventBindings = new()
		{
			{ InputEventType.Space, InputMapType.Player },
			{ InputEventType.Click0, InputMapType.Player },
			{ InputEventType.Click1, InputMapType.Player },
			{ InputEventType.ChangeMode, InputMapType.Player },
			{ InputEventType.Scroll, InputMapType.Player },

			{ InputEventType.Submit, InputMapType.UI },
			{ InputEventType.Cancel, InputMapType.UI },
			{ InputEventType.Tab, InputMapType.UI },
			{ InputEventType.Status, InputMapType.UI },
		};

		private readonly Dictionary<(InputEventType, InputEventResponseType), Action<InputAction.CallbackContext>> inputEventsWithContent = new();
		private readonly Dictionary<(InputEventType, InputEventResponseType), Action> inputEvents = new();
		private readonly Dictionary<InputEventType, bool> isPressed = new();

		public Vector3 MouseWorldPosition { get; private set; }
		public Vector2 MoveInput { get; private set; }
		private IInputStrategy CurrentInputStrategy { get; set; }

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

			BindEvents();

			// ClearInputEvents();
			SetInputStrategy(new InputStrategyLoading());

			// TODO: Setup Class 같은 것이 있어야 할 듯 - 2025.04.19 11:38
			UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
			{
				Debug.Log($"Scene loaded: {scene.name}");
				StartCoroutine(InvokeAfterStart(scene, mode));
			};
		}

		private void ClearInputEvents()
		{
			foreach (InputEventType inputEventType in Enum.GetValues(typeof(InputEventType)))
			{
				foreach (InputEventResponseType inputEventResponseType in Enum.GetValues(typeof(InputEventResponseType)))
				{
					inputEventsWithContent[(inputEventType, inputEventResponseType)] = delegate { };
					inputEvents[(inputEventType, inputEventResponseType)] = delegate { };
					isPressed[inputEventType] = false;
				}
			}
		}

		private IEnumerator InvokeAfterStart(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
		{
			yield return new WaitForEndOfFrame(); // Start 실행 후

			switch (scene.name)
			{
				case "World":
					SetInputStrategy(new InputStrategyWorld());
					break;
				case "Lobby":
					SetInputStrategy(new InputStrategyLobby());
					break;
				case "Loading":
					SetInputStrategy(new InputStrategyLoading());
					break;
				case "loaded":
				default:
					Debug.LogWarning($"No input strategy registered for scene: {scene.name}");
					yield break;
			}
		}

		private void SetInputStrategy(IInputStrategy inputStrategy)
		{
			CurrentInputStrategy = inputStrategy;

			ClearInputEvents();

			List<InputRegisterData> inputRegisterDataList = CurrentInputStrategy.InputRegisterDataList;
			foreach (InputRegisterData inputRegisterData in inputRegisterDataList)
				RegisterInputEvent(inputRegisterData.InputEventType, inputRegisterData.InputEventResponseType, inputRegisterData.Callback);
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
			if (CurrentInputStrategy.TryGetEventReturnConditions(inputEventType, out var conditions) &&
				GameManager.Instance.Conditions.IsGameConditionAny(conditions))
				return;

			inputEventsWithContent[(inputEventType, InputEventResponseType.Started)]?.Invoke(ctx);
			inputEvents[(inputEventType, InputEventResponseType.Started)]?.Invoke();

			isPressed[inputEventType] = true;
			GetLoop(inputEventType).Forget();
		}

		private async UniTaskVoid GetLoop(InputEventType inputEventType = InputEventType.Space)
		{
			InputMapType actionMapType = inputEventBindings[inputEventType];
			while (isPressed[inputEventType] == true)
			{
				await UniTask.Yield(PlayerLoopTiming.Update);

				if (CurrentInputStrategy.TryGetEventReturnConditions(inputEventType, out var conditions) &&
					GameManager.Instance.Conditions.IsGameConditionAny(conditions))
					continue;

				inputEventsWithContent[(inputEventType, InputEventResponseType.Get)]?.Invoke(default);
				inputEvents[(inputEventType, InputEventResponseType.Get)]?.Invoke();
			}
		}

		private void OnEventPerformed(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			if (CurrentInputStrategy.TryGetEventReturnConditions(inputEventType, out var conditions) &&
				GameManager.Instance.Conditions.IsGameConditionAny(conditions))
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
			UpdateIsPointerOverUI();
			UpdateMoveInput();
		}

		private void UpdateMouseWorldPosition()
		{
			// Loading 씬은 카메라가 없음 - 2025.08.08 20:24
			if (Camera.main == null)
			{
				MouseWorldPosition = Vector3.zero;
				return;
			}

			Vector3 mousePos = Input.mousePosition;
			mousePos.z = Camera.main.nearClipPlane;
			Ray ray = Camera.main.ScreenPointToRay(mousePos);

			if (Physics.Raycast(ray, out RaycastHit hit, 100, LayerMask.GetMask("GROUND")))
				MouseWorldPosition = hit.point;
			else
				MouseWorldPosition = Vector3.zero;
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
				CurrentInputStrategy.TryGetAxisReturnConditions(InputAxisType.Move, out var conditions) &&
				GameManager.Instance.Conditions.IsGameConditionAny(conditions))
			{
				MoveInput = Vector2.zero;
				return;
			}

			float h = Input.GetAxisRaw("Horizontal");
			float v = Input.GetAxisRaw("Vertical");

			if (h == 0)
				h = SOManager.Instance.JoystickX.RuntimeValue;
			if (v == 0)
				v = SOManager.Instance.JoystickY.RuntimeValue;

			MoveInput = new Vector2(h, v).normalized;
		}
	}
}