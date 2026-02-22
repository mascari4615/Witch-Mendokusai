namespace WitchMendokusai
{
	public class FSMNPC : FSM<FSMStateCommon>
	{
		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void InitFSMEvent() {}
	}
}