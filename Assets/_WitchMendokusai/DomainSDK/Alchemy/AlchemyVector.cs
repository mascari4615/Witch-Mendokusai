using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-174 Phase 0 — 「솥 속의 지도」 효과 좌표계 2D 힘 벡터.
	// 재료 1개 = 방향+크기 1쌍. 갈기(grind) 정도가 Magnitude, 재료 종류가 방향. 합성 = 단순 덧셈.
	// 순수 readonly struct — RciDemand/CommuteMatchResult 동격, DomainSDK 안 Unity 런타임 상태 0.
	// 비전-중립: 마계 원소 축(저주/온기/시간/기억)·솥 비주얼은 스킨 — 모델은 (X,Y) float 만.
	public readonly struct AlchemyVector
	{
		public readonly float X;
		public readonly float Y;

		public AlchemyVector(float x, float y)
		{
			X = x;
			Y = y;
		}

		public static AlchemyVector Zero => new AlchemyVector(0f, 0f);

		public float Magnitude => Mathf.Sqrt(X * X + Y * Y);

		public static AlchemyVector operator +(AlchemyVector a, AlchemyVector b)
			=> new AlchemyVector(a.X + b.X, a.Y + b.Y);

		public static AlchemyVector operator -(AlchemyVector a, AlchemyVector b)
			=> new AlchemyVector(a.X - b.X, a.Y - b.Y);
	}
}
