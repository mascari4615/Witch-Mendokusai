namespace WitchMendokusai
{
	/// <summary> 자식들 중 false가 나올 때까지 연속으로 순회하는 노드 </summary>
	public class SequenceNode : CompositeNode
	{
		public SequenceNode(params Node[] nodes) : base(nodes) { }
		protected int current;

		public override State OnUpdate()
		{
			for (current = 0; current < ChildList.Count; ++current)
			{
				Node node = ChildList[current];
				State result = node.Update();
				if (result == State.Failure)
					return State.Failure;
			}
			return State.Success;
		}
	}
}