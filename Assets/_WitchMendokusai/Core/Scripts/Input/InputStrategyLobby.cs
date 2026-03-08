using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyLobby : InputStrategyBase
	{
		private List<InputRegisterData> _inputRegisterDataList;
		public override List<InputRegisterData> InputRegisterDataList
		{
			get
			{
				_inputRegisterDataList ??= new List<InputRegisterData>()
					{
						#region UI
						new (InputEventType.Cancel,
							InputEventResponseType.Performed,
							() => LobbyManager.Instance.ToggleSettings(),
							() => CanExecute(InputEventType.Cancel)
						)
					#endregion
				};

				return _inputRegisterDataList;
			}
		}

		// 입력 조건도 없음 - KarmoDDrine 2026-01-12
		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new();
		protected override Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions => new();
	}
}