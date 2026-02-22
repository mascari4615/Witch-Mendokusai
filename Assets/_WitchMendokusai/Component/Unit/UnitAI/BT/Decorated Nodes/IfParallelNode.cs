using System;

namespace WitchMendokusai
{
	public class IfParallelNode : DecoratedCompositeNode
	{
		public IfParallelNode(Func<bool> condition, params Node[] nodes)
			: base(condition, new ParallelNode(nodes)) { }
	}
}