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

			ClearInputEvents();
			BindEvents();

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

			IInputStrategy inputStrategy;
			switch (scene.name)
			{
				case "World":
					inputStrategy = new InputStrategyWorld();
					break;
				case "Lobby":
					inputStrategy = new InputStrategyLobby();
					break;
				case "Loading":
					inputStrategy = new InputStrategyLoading();
					break;
				case "loaded":
				default:
					Debug.LogWarning($"No input strategy registered for scene: {scene.name}");
					yield break;
			}

			ClearInputEvents();

			List<InputRegisterData> inputRegisterDataList = inputStrategy.InputRegisterDataList;
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
				(InputMapType actionMapType, GameConditionType returnCondition) = inputEventBindings[inputEventType];
				string actionName = $"{actionMapType}/{inputEventType}";

				inputActionAsset[actionName].started += ctx => OnEventStart(inputEventType, ctx);
				inputActionAsset[actionName].performed += ctx => OnEventPerformed(inputEventType, ctx);
				inputActionAsset[actionName].canceled += ctx => OnEventCanceled(inputEventType, ctx);
			}
		}

		private void OnEventStart(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			if (GameManager.Instance.Conditions.IsGameCondition(inputEventBindings[inputEventType].gameConditionType))
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

				if (GameManager.Instance.Conditions.IsGameCondition(returnCondition))
					continue;

				inputEventsWithContent[(inputEventType, InputEventResponseType.Get)]?.Invoke(default);
				inputEvents[(inputEventType, InputEventResponseType.Get)]?.Invoke();
			}
		}

		private void OnEventPerformed(InputEventType inputEventType, InputAction.CallbackContext ctx)
		{
			if (GameManager.Instance.Conditions.IsGameCondition(inputEventBindings[inputEventType].gameConditionType))
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
	}
}