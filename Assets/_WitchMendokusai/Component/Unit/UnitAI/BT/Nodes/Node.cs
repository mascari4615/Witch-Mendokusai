using System.Collections.Generic;

namespace WitchMendokusai
{
	public enum State
	{
		Success,
		Running,
		Failure,

	}

	/// <summary> 행동트리 최상위 인터페이스 </summary>
	public abstract class Node
	{
		public State State { get; private set; } = State.Running;

		public State Update()
		{
			State = OnUpdate();
			return State;
		}

		public void Abort()
		{
			Traverse(this, (node) =>
			{
				node.State = State.Running;
			});
		}

		public abstract State OnUpdate();

		public static void Traverse(Node node, System.Action<Node> visiter)
		{
			if (node != null)
			{
				visiter.Invoke(node);
				var children = GetChildren(node);
				children.ForEach((n) => Traverse(n, visiter));
			}
		}

		public static List<Node> GetChildren(Node parent)
		{
			List<Node> children = new();

			if (parent is DecoratorNode decorator && decorator.child != null)
			{
				children.Add(decorator.child);
			}

			/*if (parent is RootNode rootNode && rootNode.child != null)
			{
				children.Add(rootNode.child);
			}*/

			if (parent is CompositeNode composite)
			{
				return composite.ChildList;
			}

			return children;
		}

	}
}