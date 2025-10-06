namespace WitchMendokusai
{
	/// <summary> 자식들을 순회하며 true인 것 하나만 수행하는 노드 </summary>
	public class SelectorNode : CompositeNode
	{
		public SelectorNode(params Node[] nodes) : base(nodes) { }
		protected int current;

		public override State OnUpdate()
		{
			for (; current < ChildList.Count; ++current)
			{
				Node child = ChildList[current];
				switch (child.Update())
				{
					case State.Running:
						return State.Running;
					case State.Success:
						return State.Success;
					case State.Failure:
						continue;
				}
			}

			current = 0;
			return State.Failure;
		}
	}
}