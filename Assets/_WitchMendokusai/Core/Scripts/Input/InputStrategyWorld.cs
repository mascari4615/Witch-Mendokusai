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
							() => Player.Instance.TryUseSkill(0),
							() => CanExecute(InputEventType.Space)
						),
						new(
							InputEventType.Jump,
							InputEventResponseType.Performed,
							() => Player.Instance.TryJump(),
							() => CanExecute(InputEventType.Jump)
						),
						new(
							InputEventType.Jump,
							InputEventResponseType.Canceled,
							() => Player.Instance.StopJump(),
							() => CanExecute(InputEventType.Jump)
						),
						new(
							InputEventType.Click0,
							InputEventResponseType.Get,
							() => Player.Instance.TryUseSkill(1),
							() => CanExecute(InputEventType.Click0)
						),
						new(
							InputEventType.Click1,
							InputEventResponseType.Get,
							() => Player.Instance.TryUseSkill(2),
							() => CanExecute(InputEventType.Click1)
						),
						new(
							InputEventType.Sprint,
							InputEventResponseType.Started,
							() => Player.Instance.SetSprinting(true),
							() => CanExecute(InputEventType.Sprint)
						),
						new(
							InputEventType.Sprint,
							InputEventResponseType.Canceled,
							() => Player.Instance.SetSprinting(false),
							() => CanExecute(InputEventType.Sprint)
						),
						new(
							InputEventType.Crouch,
							InputEventResponseType.Started,
							() => Player.Instance.SetCrouching(true),
							() => CanExecute(InputEventType.Crouch)
						),
						new(
							InputEventType.Crouch,
							InputEventResponseType.Canceled,
							() => Player.Instance.SetCrouching(false),
							() => CanExecute(InputEventType.Crouch)
						),

						new(
							InputEventType.ChangeMode,
							InputEventResponseType.Performed,
							() => Player.Instance.SetAutoAim(!Player.Instance.IsAutoAim),
							() => CanExecute(InputEventType.ChangeMode)
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
							() => Player.Instance.TryInteract(),
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
						),
						new(
							InputEventType.Status,
							InputEventResponseType.Performed,
							() => UIManager.Instance.ToggleStatus(),
							() => CanExecute(InputEventType.Status)
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