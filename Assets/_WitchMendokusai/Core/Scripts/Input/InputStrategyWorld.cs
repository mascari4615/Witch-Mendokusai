using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyWorld : InputStrategyBase
	{
		public override List<InputRegisterData> InputRegisterDataList { get; } = new List<InputRegisterData>()
		{
			#region Player
			new(
				InputEventType.Space,
				InputEventResponseType.Performed,
				() => Player.Instance.TryUseSkill(0)
			),
			new(
				InputEventType.Click0,
				InputEventResponseType.Get,
				() => Player.Instance.TryUseSkill(1)
			),
			new(
				InputEventType.Click1,
				InputEventResponseType.Get,
				() => Player.Instance.TryUseSkill(2)
			),

			new(
				InputEventType.ChangeMode,
				InputEventResponseType.Performed,
				() => Player.Instance.SetAutoAim(!Player.Instance.IsAutoAim)
			),
			#endregion

			#region UI
			new(
				InputEventType.Submit,
				InputEventResponseType.Performed,
				() => Player.Instance.TryInteract()
			),
			new(
				InputEventType.Cancel,
				InputEventResponseType.Performed,
				() => UIManager.Instance.OnCancelInput()
			),
			new(
				InputEventType.Tab,
				InputEventResponseType.Performed,
				() => UIManager.Instance.ToggleTabUI()
			),
			new(
				InputEventType.Status,
				InputEventResponseType.Performed,
				() => UIManager.Instance.ToggleStatus()
			)
			#endregion
		};

		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new()
		{
			{ InputEventType.Space, new[] { GameConditionType.IsChatting } },
			{
				InputEventType.Click0,
				new[]
				{
					GameConditionType.IsMouseOnUI,
					GameConditionType.IsChatting,
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
					GameConditionType.IsChatting,
					GameConditionType.IsPaused,
					GameConditionType.IsDied,
					GameConditionType.IsBuilding
				}
			},
			{ InputEventType.ChangeMode, new[] { GameConditionType.IsChatting } },
			{ InputEventType.Scroll, new[] { GameConditionType.IsChatting } },

			{
				InputEventType.Submit,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsChatting,
					GameConditionType.IsDied,
					GameConditionType.IsBuilding,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI
				}
			},
			{ InputEventType.Cancel, new[] { GameConditionType.IsChatting } },
			{ InputEventType.Tab, new[] { GameConditionType.IsChatting } },
			{ InputEventType.Status, new[] { GameConditionType.IsChatting } },
		};

		protected override Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions => new()
		{
			{
				InputAxisType.Move,
				new[]
				{
					GameConditionType.IsPaused,
					GameConditionType.IsChatting,
					GameConditionType.IsDied,
					GameConditionType.IsInTransition,
					GameConditionType.IsViewingUI
				}
			}
		};
	}
}