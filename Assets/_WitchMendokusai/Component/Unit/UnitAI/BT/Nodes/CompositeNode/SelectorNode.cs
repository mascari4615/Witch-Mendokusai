namespace WitchMendokusai
{
	/// <summary> 자식들을 순회하며 true인 것 하나만 수행하는 노드 </summary>
	public class SelectorNode : CompositeNode
	{
		public SelectorNode(params Node[] nodes) : base(nodes) { }
		protected int current = 0;

		public override BTState OnUpdate()
		{
			while (current < ChildList.Count)
			{
				Node child = ChildList[current];
				BTState result = child.UpdateBT();
				
				switch (result)
				{
					case BTState.Running:
						// Running이면 현재 상태 유지하고 Running 반환
						return BTState.Running;
						
					case BTState.Success:
						// Success면 성공으로 완료, 다음번을 위해 리셋
						current = 0;
						return BTState.Success;
						
					case BTState.Failure:
						// Failure면 다음 자식으로 넘어감
						current++;
						continue;
				}
			}

			// 모든 자식이 실패했으면 Failure 반환하고 리셋
			current = 0;
			return BTState.Failure;
		}
	}
}