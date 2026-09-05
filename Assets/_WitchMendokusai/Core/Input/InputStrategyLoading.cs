using System.Collections.Generic;

namespace WitchMendokusai
{
	public class InputStrategyLoading : InputStrategyBase
	{
		public override List<InputRegisterData> InputRegisterDataList { get; } = new List<InputRegisterData>()
		{
			// Loading은 딱히 입력 받을 것이 없음
			// 나중에 Addressable 다운 받을 거 많아지면, 미니게임 같은 거 넣을 수는 있을 듯 - KarmoDDrine 2025-08-08
		};

		// 입력 조건도 없음 - KarmoDDrine 2026-01-12
		protected override Dictionary<InputEventType, GameConditionType[]> EventReturnConditions => new();
		protected override Dictionary<InputAxisType, GameConditionType[]> AxisReturnConditions => new();
	}
}