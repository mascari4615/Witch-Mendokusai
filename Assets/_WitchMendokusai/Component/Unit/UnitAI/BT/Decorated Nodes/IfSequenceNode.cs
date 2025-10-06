using System;

// 날짜 : 2021-01-16 PM 11:20:37
// 작성자 : Rito

namespace WitchMendokusai
{
	public class IfSequenceNode : DecoratedCompositeNode
	{
		public IfSequenceNode(Func<bool> condition, params Node[] nodes)
			: base(condition, new SequenceNode(nodes)) { }
	}
}