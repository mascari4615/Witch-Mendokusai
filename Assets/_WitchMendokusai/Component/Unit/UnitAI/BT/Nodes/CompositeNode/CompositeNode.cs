using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary> 
	/// <para/> 자식들을 순회하는 노드
	/// </summary>
	public abstract class CompositeNode : Node
	{
		public List<Node> ChildList { get; protected set; }

		// 생성자
		public CompositeNode(params Node[] nodes) => ChildList = new List<Node>(nodes);

		// 자식 노드 추가
		public CompositeNode Add(Node node)
		{
			ChildList.Add(node);
			return this;
		}
	}
}