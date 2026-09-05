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
	// InputManager 의 입력 사건 배선 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 InputManager.cs 를 본다.
	public partial class InputManager : MonoBehaviour
	{
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
			{ InputEventType.Gather, InputMapType.Player },
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
			{ InputEventType.DiscoveryToggle, InputMapType.UI },
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
		/// <summary>
		/// 「아무거나 눌렀나」 — 대사 넘기기처럼 *무엇을 눌렀는지는 상관없는* 자리에서 쓴다.
		///
		/// ★ 폰엔 키보드가 없다. 이 값이 키보드만 보던 동안 **폰에서는 대사를 넘길 방법이 없었다**
		///   (2026-08-07 실기: 안드로이드 뒤로가기만 우연히 먹혔다 — 그게 키로 잡혀서).
		///   화면을 톡 하는 것도 「아무거나」에 들어가야 뜻이 맞는다.
		/// </summary>
		public bool IsAnyKeyPressedThisFrame =>
			(Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
			|| (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
			|| (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

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

		/// <summary>
		/// 화면 버튼을 누른 것 = 그 키를 누른 것 (TASK-WM-200).
		///
		/// ★ 왜 이 창구가 필요한가: 손가락엔 키보드가 없다. 화면 버튼이 「점프」를 뜻하려면 결국
		///   *같은 점프 이벤트*가 나가야 한다 — 안 그러면 화면 버튼만을 위한 두 번째 점프 경로가 생기고,
		///   나중에 점프 규칙을 고칠 때 한쪽만 고쳐진다. 장치가 다를 뿐 뜻이 같으면 길도 같아야 한다.
		/// ★ 누름/뗌을 나눠 받는 이유: 공격처럼 *누르고 있는 동안* 계속 나가는 것들이 있다
		///   (누름이 Get 반복을 켜고, 뗌이 끈다). 버튼을 「한 번 눌림」으로만 만들면 그게 죽는다.
		/// </summary>
		public void PressFromScreenButton(InputEventType inputEventType)
		{
			OnEventStart(inputEventType, default);
			OnEventPerformed(inputEventType, default);
		}

		/// <summary> 화면 버튼에서 손을 뗐다 — 누르고 있는 동안 도는 것들을 끈다. </summary>
		public void ReleaseFromScreenButton(InputEventType inputEventType)
		{
			OnEventCanceled(inputEventType, default);
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
	}
}
