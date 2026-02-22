using System.Collections.Generic;

namespace WitchMendokusai
{
	public enum BTState
	{
		Success,
		Running,
		Failure,
	}

	/// <summary> 행동트리 최상위 인터페이스 </summary>
	public abstract class Node
	{
		public BTState State { get; private set; } = BTState.Running;

		public BTState UpdateBT()
		{
			State = OnUpdate();
			return State;
		}

		public void Abort()
		{
			Traverse(this, (node) =>
			{
				node.State = BTState.Running;
			});
		}

		public abstract BTState OnUpdate();

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