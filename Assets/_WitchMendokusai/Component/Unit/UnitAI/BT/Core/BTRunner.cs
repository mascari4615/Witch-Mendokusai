namespace WitchMendokusai
{
	public abstract class BTRunner
	{
		public const float TICK = 0.1f;
	
		private readonly Node rootNode;
		protected UnitObject unitObject;

		public bool CanChangeState => rootNode.State == BTState.Success || rootNode.State == BTState.Failure;

		public BTRunner(UnitObject unitObject)
		{
			rootNode = MakeNode();
			this.unitObject = unitObject;
		}

		protected abstract Node MakeNode();
		
		public BTState UpdateBT()
		{
			return rootNode.UpdateBT();
		}
	}
}