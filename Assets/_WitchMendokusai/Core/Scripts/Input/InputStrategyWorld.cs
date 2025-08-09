using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyWorld : IInputStrategy
	{
		public List<InputRegisterData> InputRegisterDataList { get; } = new List<InputRegisterData>()
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
				() => TryUseSkill(1)
			),
			new(
				InputEventType.Click1,
				InputEventResponseType.Get,
				() => TryUseSkill(2)
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
				Player.Instance.TryInteract
			),
			new(
				InputEventType.Cancel,
				InputEventResponseType.Performed,
				UIManager.Instance.ToggleOverlayUI_Setting
			),
			new(
				InputEventType.Tab,
				InputEventResponseType.Performed,
				UIManager.Instance.ToggleOverlayUI_Tab
			),
			new(
				InputEventType.Status,
				InputEventResponseType.Performed,
				UIManager.Instance.ToggleStatus
			)
			#endregion
		};

		private static void TryUseSkill(int skillIndex)
		{
			GameConditionType skillCondition = GameConditionType.IsMouseOnUI
											| GameConditionType.IsChatting
											| GameConditionType.IsPaused
											| GameConditionType.IsDied
											| GameConditionType.IsBuilding;

			if (GameManager.Instance.Conditions.IsGameCondition(skillCondition))
				return;

			Player.Instance.TryUseSkill(skillIndex);
		}
	}
}