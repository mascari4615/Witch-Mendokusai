using System;

namespace WitchMendokusai.DomainSDK.Workshop
{
	/// <summary>
	/// TASK-WM-170 Phase 0 — 공방 재료 식별자(데이터주도). 마계 전리품·재배·구매 등 어떤 입력원이든
	/// 동일한 정수 키로 다룬다. 모드/UGC 가 정의한 새 재료도 그대로 흐름 (6 동기 모딩/UGC).
	///
	/// City.ResourceId 와 같은 패턴 — 다만 City 경제와 공방 경제는 의도적으로 분리(스케일 다름).
	/// 본격 슬라이스에서 ItemData 와 매핑 SO 가 추가될 수 있음.
	/// </summary>
	public readonly struct MaterialId : IEquatable<MaterialId>
	{
		public readonly int Value;

		public MaterialId(int value)
		{
			Value = value;
		}

		public bool Equals(MaterialId other) => Value == other.Value;

		public override bool Equals(object obj) => obj is MaterialId other && Equals(other);

		public override int GetHashCode() => Value;

		public override string ToString() => $"Material({Value})";

		public static bool operator ==(MaterialId left, MaterialId right) => left.Equals(right);

		public static bool operator !=(MaterialId left, MaterialId right) => left.Equals(right) == false;
	}
}
