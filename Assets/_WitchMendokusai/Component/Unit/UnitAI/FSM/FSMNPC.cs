namespace WitchMendokusai
{
	public class FSMNPC : FSM<FSMStateCommon>
	{
		public FSMNPC(UnitObject unitObject) : base(unitObject) {}
		protected override FSMStateCommon DefaultState => FSMStateCommon.Idle;

		protected override void Init() {}
	}
}