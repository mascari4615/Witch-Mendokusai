using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyLobby : IInputStrategy
	{
		public List<InputRegisterData> InputRegisterDataList { get; } = new List<InputRegisterData>()
		{
			#region UI
			new (InputEventType.Cancel, InputEventResponseType.Performed, () => LobbyManager.Instance.ToggleSettings())
			#endregion
		};
	}
}