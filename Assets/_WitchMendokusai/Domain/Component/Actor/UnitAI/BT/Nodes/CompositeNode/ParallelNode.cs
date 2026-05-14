using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary> 자식들 리턴에 관계 없이 모두 순회하는 노드 </summary>
	public class ParallelNode : CompositeNode
	{
		public ParallelNode(params Node[] nodes) : base(nodes) { }

		public override BTState OnUpdate()
		{
			foreach (var node in ChildList)
			{
				node.OnUpdate();
			}
			return BTState.Success;
		}
	}
}