using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-173 — 마력의 강(레이라인)을 게임에 붙이는 첫 배선.
	///
	/// ★ 왜 이 파일이 생겼나: 레이라인 계산층(거점·배선·감쇠)은 만들어진 뒤로 <b>게임 어디서도 안 불렸다.</b>
	///   아무도 안 부르는 층은 지워져도 컴파일이 안 깨져 조용히 사라진다 — 2026-08-07 에 실제로 그랬다.
	///
	/// 여기는 <b>망을 들고 있다가 「보내면 얼마나 도착하는지」를 답해 주는 자리</b>다.
	/// 무엇을 어디로 얼마나 보낼지는 이 부품이 정하지 않는다 — 부르는 쪽이 정한다.
	/// </summary>
	public class LeylineDirector : MonoBehaviour
	{
		[Tooltip("거리가 멀수록 마력이 얼마나 새는지. 0 이면 아무리 멀어도 안 샌다.")]
		[SerializeField] private float decayRate = 0.02f;

		private readonly LeylineGraph graph = new LeylineGraph();

		/// <summary>거점과 배선이 담긴 망. 마을·공방이 여기에 자기 거점을 얹는다.</summary>
		public LeylineGraph Graph => graph;

		/// <summary>마력이 도착할 때마다 알린다 — (보낸 곳, 받는 곳, 보낸 양, 도착한 양).</summary>
		public event Action<string, string, float, float> OnManaDelivered = delegate { };

		/// <summary>거점 하나 얹기. 같은 이름이 이미 있으면 계산층이 거절한다.</summary>
		public void AddNode(string id, LeylineNodeKind kind) => graph.AddNode(new LeylineNode(id, kind));

		/// <summary>배선 하나 얹기. 길이가 곧 마력이 새는 양이다.</summary>
		public void AddEdge(string fromId, string toId, float length) => graph.AddEdge(new LeylineEdge(fromId, toId, length));

		/// <summary>
		/// 보낸 양이 최단 경로를 타고 갔을 때 <b>실제로 도착하는 양</b>. 길이 없으면 0 이다(예외 아님).
		/// </summary>
		public float Send(string sourceId, string sinkId, float amount)
		{
			float arrived = ManaFlow.CalculateOnGraph(graph, sourceId, sinkId, amount, decayRate);
			OnManaDelivered.Invoke(sourceId, sinkId, amount, arrived);
			return arrived;
		}
	}
}
