using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 자식들 중 false가 나올 때까지 연속으로 순회하는 노드 </summary>
	public class SequenceNode : CompositeNode
	{
		public SequenceNode(params Node[] nodes) : base(nodes) { }
		protected int current = 0;

		public override BTState OnUpdate()
		{
			while (current < ChildList.Count)
			{
				Debug.Log($"SequenceNode Current : {current}");
				Node child = ChildList[current];
				BTState result = child.UpdateBT();

				switch (result)
				{
					case BTState.Running:
						// Running이면 현재 상태 유지하고 Running 반환
						return BTState.Running;

					case BTState.Failure:
						// Failure면 즉시 실패로 완료, 다음번을 위해 리셋
						current = 0;
						return BTState.Failure;

					case BTState.Success:
						// Success면 다음 자식으로 넘어감
						current++;
						continue; // while 루프에서는 안전
				}
			}

			// 모든 자식이 성공했으면 Success 반환하고 리셋
			current = 0;
			return BTState.Success;
		}
	}
}