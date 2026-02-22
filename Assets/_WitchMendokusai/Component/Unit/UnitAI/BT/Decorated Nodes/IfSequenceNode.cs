using System;

namespace WitchMendokusai
{
	public class IfSequenceNode : DecoratedCompositeNode
	{
		public IfSequenceNode(Func<bool> condition, params Node[] nodes)
			: base(condition, new SequenceNode(nodes)) { }
	}
}