using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyLoading : IInputStrategy
	{
		public List<InputRegisterData> InputRegisterDataList { get; } = new List<InputRegisterData>()
		{
			// Loading은 딱히 입력 받을 것이 없음
			// 나중에 Addressable 다운 받을 거 많아지면, 미니게임 같은 거 넣을 수는 있을 듯 - KarmoDDrine 2025-08-08
		};
	}
}