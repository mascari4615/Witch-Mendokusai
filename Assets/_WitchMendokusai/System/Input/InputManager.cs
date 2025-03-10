using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace WitchMendokusai
{
	public class InputManager : Singleton<InputManager>
	{
		// TODO: InputManager를 통해 모든 입력을 받아서 처리하도록 한다.

		[SerializeField]
		private InputActionAsset inputActionAsset;

		public Vector3 MouseWorldPosition { get; private set; }

		public event Action OnExit;

		public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

		public Dictionary<InputMouseEventType, Func<bool>> mouseInputTriggers = new();
		public Dictionary<InputMouseEventType, Action> mouseInputEvents = new();

		protected override void Awake()
		{
			base.Awake();
			Init();
		}

		private void Init()
		{
			inputActionAsset.Enable();

			InitMouseInputTrigger();
		}

		private void InitMouseInputTrigger()
		{
			mouseInputTriggers.Add(InputMouseEventType.Button0Get, () => Input.GetMouseButton(0));
			mouseInputTriggers.Add(InputMouseEventType.Button0Up, () => Input.GetMouseButtonUp(0));
			mouseInputTriggers.Add(InputMouseEventType.Button0Down, () => Input.GetMouseButtonDown(0));

			mouseInputTriggers.Add(InputMouseEventType.Button1Get, () => Input.GetMouseButton(1));
			mouseInputTriggers.Add(InputMouseEventType.Button1Up, () => Input.GetMouseButtonUp(1));
			mouseInputTriggers.Add(InputMouseEventType.Button1Down, () => Input.GetMouseButtonDown(1));

			mouseInputTriggers.Add(InputMouseEventType.Button2Get, () => Input.GetMouseButton(2));
			mouseInputTriggers.Add(InputMouseEventType.Button2Up, () => Input.GetMouseButtonUp(2));
			mouseInputTriggers.Add(InputMouseEventType.Button2Down, () => Input.GetMouseButtonDown(2));

			mouseInputTriggers.Add(InputMouseEventType.ScrollWheel, () => Input.mouseScrollDelta.y != 0);
		}

		public void RegisterMouseEvent(InputMouseEventType inputMouseEventType, Action action)
		{
			if (mouseInputEvents.ContainsKey(inputMouseEventType) == false)
				mouseInputEvents.Add(inputMouseEventType, null);

			mouseInputEvents[inputMouseEventType] += action;
		}

		public void UnregisterMouseEvent(InputMouseEventType inputMouseEventType)
		{
			if (mouseInputEvents.ContainsKey(inputMouseEventType) == false)
				return;

			mouseInputEvents[inputMouseEventType] = null;
		}

		private void Update()
		{
			UpdateMouseWorldPosition();

			foreach ((InputMouseEventType key, Func<bool> action) in mouseInputTriggers)
			{
				if (mouseInputEvents.TryGetValue(key, out Action mouseInputEvent))
				{
					if (action.Invoke())
						mouseInputEvent?.Invoke();
				}
			}

			if (Input.GetKeyDown(KeyCode.Escape))
				OnExit?.Invoke();

			if (GameManager.Instance.IsChatting)
				return;

			// UIManager
			if (Input.GetKeyDown(KeyCode.Tab))
				UIManager.Instance.ToggleOverlayUI_Tab();
			if (inputActionAsset["UI/Cancel"].triggered)
				UIManager.Instance.ToggleOverlayUI_Setting();
			if (Input.GetKeyDown(KeyCode.V))
				UIManager.Instance.ToggleStatus();

			// Player
			if (inputActionAsset["UI/Submit"].triggered)
				Player.Instance.TryInteract();
			if (Input.GetKeyDown(KeyCode.C))
				Player.Instance.SetAutoAim(!Player.Instance.IsAutoAim);
			if (Input.GetKeyDown(KeyCode.Space))
				Player.Instance.TryUseSkill(0);
			if (GameManager.Instance.IsMouseOnUI || TimeManager.Instance.IsPaused)
				return;

			if (IsPointerOverUI())
				return;
				
			if (Input.GetMouseButton(0))
				Player.Instance.TryUseSkill(1);
			if (Input.GetMouseButton(1))
				Player.Instance.TryUseSkill(2);
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