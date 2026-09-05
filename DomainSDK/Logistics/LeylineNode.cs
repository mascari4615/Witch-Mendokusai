using System;

namespace WitchMendokusai
{
	// 레이라인 망의 노드. 격자 셀이 아니라 임의 위치 거점 — RoadGraph(Vector3Int 셀 + 4-이웃)
	// 와 결정적으로 다른 점: 노드는 id 로 식별되고 인접성은 명시적 엣지로만 정해진다. 그래서
	// 좌표는 노드 모델에 없음(시각/배치는 별도 layer 책임 — 6 동기 「분리」).
	//
	// id 비어있음 = boundary 위반 → FastFail throw. LeylineGraph 가 노드를 id 로 색인하므로
	// 빈 id 가 들어오면 그래프 무결성이 깨짐(중복 키 / 엣지 미해결).
	public sealed class LeylineNode
	{
		public string Id { get; }
		public LeylineNodeKind Kind { get; }

		public LeylineNode(string id, LeylineNodeKind kind)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("LeylineNode.Id 는 비어있을 수 없다", nameof(id));
			}

			Id = id;
			Kind = kind;
		}
	}
}
