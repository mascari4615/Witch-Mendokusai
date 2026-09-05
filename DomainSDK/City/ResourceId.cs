using System;

namespace WitchMendokusai
{
	// 자원 품목 식별자 — **enum 아닌 데이터 주도** id (6 동기 모딩/UGC: 모드·UGC 가 새 자원 무한 추가).
	// Value 의 의미(어떤 게 마나/약재/노동력인지)·표시명·스프라이트는 Domain ResourceSO 카탈로그가 추후
	// 부여(스킨 deferred). 순수 식별자라 DomainSDK 거주(references=[]) — 시뮬 모델은 id+rate 만 셔플.
	// readonly struct = 값 동등성 + Dictionary 키 사용(GetHashCode).
	public readonly struct ResourceId : IEquatable<ResourceId>
	{
		public readonly int Value;

		public ResourceId(int value)
		{
			Value = value;
		}

		public bool Equals(ResourceId other) => Value == other.Value;

		public override bool Equals(object obj) => obj is ResourceId other && Equals(other);

		public override int GetHashCode() => Value;

		public override string ToString() => $"Resource({Value})";
	}
}
