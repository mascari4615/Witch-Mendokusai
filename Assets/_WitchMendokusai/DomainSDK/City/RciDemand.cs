namespace WitchMendokusai
{
	// RCI 수요 결과. 각 -1..1 (+ = 성장압 / - = 쇠퇴압). RciDemandModel.Evaluate 출력.
	public readonly struct RciDemand
	{
		public readonly float Residential;
		public readonly float Commercial;
		public readonly float Industrial;

		public RciDemand(float residential, float commercial, float industrial)
		{
			Residential = residential;
			Commercial = commercial;
			Industrial = industrial;
		}
	}
}
