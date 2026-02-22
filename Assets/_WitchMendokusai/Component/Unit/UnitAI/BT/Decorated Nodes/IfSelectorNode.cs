using System;

namespace WitchMendokusai
{
	public class IfSelectorNode : DecoratedCompositeNode
	{
		public IfSelectorNode(Func<bool> condition, params Node[] nodes)
			: base(condition, new SelectorNode(nodes)) { }
	}
}