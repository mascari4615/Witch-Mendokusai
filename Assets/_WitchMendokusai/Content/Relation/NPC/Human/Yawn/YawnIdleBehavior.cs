namespace WitchMendokusai
{
	public class YawnIdleBehavior : IdleStateBehavior
	{
		protected override int StateCount => System.Enum.GetValues(typeof(YawnIdleState)).Length;

		public YawnIdleState CurrentState => (YawnIdleState)CurrentStateIndex;
	}
}
