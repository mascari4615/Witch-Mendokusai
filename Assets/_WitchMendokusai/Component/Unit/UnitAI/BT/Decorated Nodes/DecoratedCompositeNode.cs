using System;

namespace WitchMendokusai
{
	/// <summary> 조건에 따른 Composite 수행 노드 </summary>
	public abstract class DecoratedCompositeNode : CompositeNode
	{
		public Func<bool> Condition { get; protected set; }

		public CompositeNode Composite { get; protected set; }

		public DecoratedCompositeNode(Func<bool> condition, CompositeNode composite)
		{
			Condition = condition;
			Composite = composite;
		}

		public override BTState OnUpdate()
		{
			return Condition() ? Composite.OnUpdate() : BTState.Failure;
		}
	}
}