using System;

// 날짜 : 2021-01-16 PM 11:23:12
// 작성자 : Rito

namespace WitchMendokusai
{
	public class IfSelectorNode : DecoratedCompositeNode
	{
		public IfSelectorNode(Func<bool> condition, params Node[] nodes)
			: base(condition, new SelectorNode(nodes)) { }
	}
}