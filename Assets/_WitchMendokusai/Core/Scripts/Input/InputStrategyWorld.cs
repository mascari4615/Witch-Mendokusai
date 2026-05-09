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
							() => PlayerRegistry.Instance.CurrentPlayer.TryUseSkill(0),
							() => CanExecute(InputEventType.Space)
						),
						new(
							InputEventType.Jump,
							InputEventResponseType.Performed,
							() => PlayerRegistry.Instance.CurrentPlayer.TryJump(),
							() => CanExecute(InputEventType.Jump)
						),
						new(
							InputEventType.Jump,
							InputEventResponseType.Canceled,
							() => PlayerRegistry.Instance.CurrentPlayer.StopJump(),
							() => CanExecute(InputEventType.Jump)
						),
						new(
							InputEventType.Click0,
							InputEventResponseType.Get,
							() => PlayerRegistry.Instance.CurrentPlayer.TryUseSkill(1),
							() => CanExecute(InputEventType.Click0)
						),
						new(
							InputEventType.Click1,
							InputEventResponseType.Get,
							() => PlayerRegistry.Instance.CurrentPlayer.TryUseSkill(2),
							() => CanExecute(InputEventType.Click1)
						),
						new(
							InputEventType.Sprint,
							InputEventResponseType.Started,
							() => PlayerRegistry.Instance.CurrentPlayer.SetSprinting(true),
							() => CanExecute(InputEventType.Sprint)
						),
						new(
							InputEventType.Sprint,
							InputEventResponseType.Canceled,
							() => PlayerRegistry.Instance.CurrentPlayer.SetSprinting(false),
							() => CanExecute(InputEventType.Sprint)
						),
						new(
							InputEventType.Crouch,
							InputEventResponseType.Started,
							() => PlayerRegistry.Instance.CurrentPlayer.SetCrouching(true),
							() => CanExecute(InputEventType.Crouch)
						),
						new(
							InputEventType.Crouch,
							InputEventResponseType.Canceled,
							() => PlayerRegistry.Instance.CurrentPlayer.SetCrouching(false),
							() => CanExecute(InputEventType.Crouch)
						),

						new(
							InputEventType.ChangeMode,
							InputEventResponseType.Performed,
							() => PlayerRegistry.Instance.CurrentPlayer.SetAutoAim(!PlayerRegistry.Instance.CurrentPlayer.IsAutoAim),
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
						#endregion

						#region UI
						new(
							InputEventType.Submit,
							InputEventResponseType.Performed,
							() => PlayerRegistry.Instance.CurrentPlayer.TryInteract(),
							() => CanExecute(InputEventType.Submit)
						),
						new(
							InputEventType.Cancel,
							InputEventResponseType.Performed,
							() => UIManager.Instance.OnCancelInput(),
							() => CanExecute(InputEventType.Cancel)
						),
						new(
							InputEventType.Tab,
							InputEventResponseType.Performed,
							() => UIManager.Instance.ToggleTabUI(),
							() => CanExecute(InputEventType.Tab)
						)
						#endregion
					};

				return _inputRegisterDataList;
			}
		}

		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new()
		{
			{ InputEventType.Space, new[] { GameConditionType.IsTyping } },
			
			{
				InputEventType.Jump,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsTyping,
					GameConditionType.IsDied,
					GameConditionType.IsBuilding,
					GameConditionType.IsViewingUI
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
					GameConditionType.IsBuilding
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
					GameConditionType.IsBuilding
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
			{
				InputEventType.Tab,
				new[]
				{
					GameConditionType.IsTyping,
					GameConditionType.IsPaused,
					// GameConditionType.IsViewingUI, // Tab도 전체화면 UI이므로 제외 - KarmoDDrine 2026-01-12
				}
			},
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
					GameConditionType.IsViewingUI
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
			}
		};
	}
}