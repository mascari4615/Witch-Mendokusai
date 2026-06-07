using System;

namespace WitchMendokusai
{
	// 두 노드를 잇는 배선 한 가닥. RoadGraph 의 4-이웃 인접(엣지=인접 → 길이 항상 1)과 달리,
	// 레이라인 엣지는 ① 사용자가 명시적으로 깔고 ② Length 가중치를 가진다. Length 가 곧 마력
	// 노화 거리(Freshness.DecayByDistance) 의 입력이 되어 「긴 우회 vs 짧은 직선」 퍼즐을 만든다.
	//
	// Directional — 마계 → 마을 흐름은 한 방향(역방향 흐름은 호출자가 두 엣지로 표현). v1 미스
	// 큐: 양방향 표현 시 LeylineGraph.AddEdge 호출 2회로 명시 (모델 내부 자동 미러링 X — 의미
	// 결정을 모델이 추측 안 함).
	//
	// Length <= 0 = boundary 위반 → FastFail. 0 길이 엣지는 노화 모델을 망가뜨림(거리 0 = 무손
	// 텔레포트 vs 의도된 비용이 헷갈림 — 정말 무손이면 별도 TeleportLink 타입을 first-use 에 추가).
	public sealed class LeylineEdge
	{
		public string FromId { get; }
		public string ToId { get; }
		public float Length { get; }

		public LeylineEdge(string fromId, string toId, float length)
		{
			if (string.IsNullOrEmpty(fromId))
			{
				throw new ArgumentException("LeylineEdge.FromId 는 비어있을 수 없다", nameof(fromId));
			}

			if (string.IsNullOrEmpty(toId))
			{
				throw new ArgumentException("LeylineEdge.ToId 는 비어있을 수 없다", nameof(toId));
			}

			if (length <= 0f)
			{
				throw new ArgumentOutOfRangeException(nameof(length), length, "LeylineEdge.Length 는 양수여야 한다");
			}

			FromId = fromId;
			ToId = toId;
			Length = length;
		}
	}
}
