using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyLobby : InputStrategyBase
	{
		public override List<InputRegisterData> InputRegisterDataList { get; } = new List<InputRegisterData>()
		{
			#region UI
			new (InputEventType.Cancel, InputEventResponseType.Performed, () => LobbyManager.Instance.ToggleSettings())
			#endregion
		};

		// 입력 조건도 없음 - KarmoDDrine 2026-01-12
		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new();
		protected override Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions => new();
	}
}