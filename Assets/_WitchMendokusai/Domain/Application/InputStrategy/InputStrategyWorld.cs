using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyWorld : InputStrategyBase
	{
		private List<InputRegisterData> _inputRegisterDataList;
		public override List<InputRegisterData> InputRegisterDataList
		{
			get
			{
				_inputRegisterDataList ??= new List<InputRegisterData>()
					{
						#region Player
						new(
							InputEventType.Space,
							InputEventResponseType.Performed,
							() => EventBusBridge.Publish(new PlayerSkillUseRequestedEvent { SkillIndex = 0 }),
							() => CanExecute(InputEventType.Space)
						),
						new(
							InputEventType.Jump,
							InputEventResponseType.Performed,
							() => EventBusBridge.Publish(new PlayerJumpRequestedEvent()),
							() => CanExecute(InputEventType.Jump)
						),
						new(
							InputEventType.Jump,
							InputEventResponseType.Canceled,
							() => EventBusBridge.Publish(new PlayerJumpReleasedEvent()),
							() => CanExecute(InputEventType.Jump)
						),
						new(
							InputEventType.Click0,
							InputEventResponseType.Get,
							() => EventBusBridge.Publish(new PlayerSkillUseRequestedEvent { SkillIndex = 1 }),
							() => CanExecute(InputEventType.Click0)
						),
						new(
							InputEventType.Click1,
							InputEventResponseType.Get,
							() => EventBusBridge.Publish(new PlayerSkillUseRequestedEvent { SkillIndex = 2 }),
							() => CanExecute(InputEventType.Click1)
						),
						new(
							InputEventType.Sprint,
							InputEventResponseType.Started,
							() => EventBusBridge.Publish(new PlayerSprintChangedEvent { IsSprinting = true }),
							() => CanExecute(InputEventType.Sprint)
						),
						new(
							InputEventType.Sprint,
							InputEventResponseType.Canceled,
							() => EventBusBridge.Publish(new PlayerSprintChangedEvent { IsSprinting = false }),
							() => CanExecute(InputEventType.Sprint)
						),
						new(
							InputEventType.Crouch,
							InputEventResponseType.Started,
							() => EventBusBridge.Publish(new PlayerCrouchChangedEvent { IsCrouching = true }),
							() => CanExecute(InputEventType.Crouch)
						),
						new(
							InputEventType.Crouch,
							InputEventResponseType.Canceled,
							() => EventBusBridge.Publish(new PlayerCrouchChangedEvent { IsCrouching = false }),
							() => CanExecute(InputEventType.Crouch)
						),

						new(
							InputEventType.ChangeMode,
							InputEventResponseType.Performed,
							() => EventBusBridge.Publish(new PlayerAutoAimToggledEvent()),
							() => CanExecute(InputEventType.ChangeMode)
						),

						new(
							InputEventType.BuildModeToggle,
							InputEventResponseType.Performed,
							() => GameModeManager.Instance.ToggleBuildMode(),
							() => CanExecute(InputEventType.BuildModeToggle)
						),

						new(
							InputEventType.Scroll,
							InputEventResponseType.Performed,
							() => CameraManager.Instance.Zoom(),
							() => CanExecute(InputEventType.Scroll)
						),

						// TASK-WM-163 — 카메라 2축 매트릭스 토글 (Scroll→Zoom 패턴 동일).
						new(
							InputEventType.CameraControlModeToggle,
							InputEventResponseType.Performed,
							() => CameraManager.Instance.ToggleControlMode(),
							() => CanExecute(InputEventType.CameraControlModeToggle)
						),
						new(
							InputEventType.CameraPerspectiveToggle,
							InputEventResponseType.Performed,
							() => CameraManager.Instance.TogglePerspective(),
							() => CanExecute(InputEventType.CameraPerspectiveToggle)
						),
						// TASK-WM-193 — 마을 경영 시점 순환 (Normal/CityView/FreeFly).
						new(
							InputEventType.CameraViewCycle,
							InputEventResponseType.Performed,
							() => CameraManager.Instance.CycleContentView(),
							() => CanExecute(InputEventType.CameraViewCycle)
						),
						#endregion

						#region UI
						new(
							InputEventType.Submit,
							InputEventResponseType.Performed,
							() => EventBusBridge.Publish(new PlayerInteractRequestedEvent()),
							() => CanExecute(InputEventType.Submit)
						),
						new(
							InputEventType.Cancel,
							InputEventResponseType.Performed,
							() => UIManager.Instance.OnCancelInput(),
							() => CanExecute(InputEventType.Cancel)
						),
						#endregion
					};

				return _inputRegisterDataList;
			}
		}

		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new()
		{
			{ InputEventType.Space, new[] { GameConditionType.IsTyping, GameConditionType.IsFreeCameraMode } },
			
			{
				InputEventType.Jump,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsDied,
					GameConditionType.IsBuilding,
					GameConditionType.IsViewingUI,
					GameConditionType.IsFreeCameraMode
				}
			},
			{
				InputEventType.Click0,
				new[]
				{
					GameConditionType.IsMouseOnUI,
					GameConditionType.IsTyping,
					GameConditionType.IsPaused,
					GameConditionType.IsDied,
					GameConditionType.IsBuilding,
					GameConditionType.IsFreeCameraMode
				}
			},
			{
				InputEventType.Click1,
				new[]
				{
					GameConditionType.IsMouseOnUI,
					GameConditionType.IsTyping,
					GameConditionType.IsPaused,
					GameConditionType.IsDied,
					GameConditionType.IsBuilding,
					GameConditionType.IsFreeCameraMode
				}
			},
			{ InputEventType.ChangeMode, new[] { GameConditionType.IsTyping } },
			{
				InputEventType.BuildModeToggle,
				new[]
				{
					GameConditionType.IsTyping,
					GameConditionType.IsPaused,
					GameConditionType.IsDied,
					// IsViewingUI 제외 — Tab 패턴과 동일. 빌드 모드는 플레이어 상태라
					// fullscreen UI 뒤에서도 G 로 해제 가능해야.
				}
			},
			{ InputEventType.Scroll, new[] { GameConditionType.IsTyping } },
			{ InputEventType.CameraControlModeToggle, new[] { GameConditionType.IsPaused, GameConditionType.IsTyping } },
			{ InputEventType.CameraPerspectiveToggle, new[] { GameConditionType.IsPaused, GameConditionType.IsTyping } },
			{ InputEventType.CameraViewCycle, new[] { GameConditionType.IsPaused, GameConditionType.IsTyping } },

			{
				InputEventType.Submit,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsDied,
					GameConditionType.IsBuilding,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI
				}
			},
			{ InputEventType.Cancel, new[] { GameConditionType.IsTyping } },
			{ InputEventType.Status, new[] { GameConditionType.IsTyping } },
		};

		protected override Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions => new()
		{
			{
				InputAxisType.Move,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsDied,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI,
					// TASK-WM-193 — 자유 카메라 모드 시 플레이어 이동 정지 (카메라 전용 축이 대신 활성).
					GameConditionType.IsFreeCameraMode
				}
			},
			{
				InputAxisType.CameraRotate,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsDied,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI
				}
			},
			// TASK-WM-193 — 자유 카메라 전용 축. 자유 모드 무관(컨트롤러가 활성 판단) — UI/일시정지/타이핑만 게이트.
			{
				InputAxisType.CameraMove,
				new[] { GameConditionType.IsPaused, GameConditionType.IsTyping, GameConditionType.IsViewingUI }
			},
			{
				InputAxisType.CameraVertical,
				new[] { GameConditionType.IsPaused, GameConditionType.IsTyping, GameConditionType.IsViewingUI }
			},
			{
				InputAxisType.ScrollWheel,
				new[] { GameConditionType.IsPaused, GameConditionType.IsTyping, GameConditionType.IsViewingUI }
			},
			{
				InputAxisType.Look,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsDied,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI
				}
			}
		};
	}
}