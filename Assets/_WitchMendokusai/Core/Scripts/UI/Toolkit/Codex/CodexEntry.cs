using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 항목 한 개. 카테고리가 자체 데이터(DataSO 등) → CodexEntry list 로 변환해 노출.
	///
	/// Source 는 의도적으로 untyped — 카테고리 내부에서만 어떤 타입인지 알면 됨.
	/// BuildDetail 에서 카테고리가 typecheck 해서 처리.
	/// </summary>
	public class CodexEntry
	{
		public string Id { get; }
		public string DisplayName { get; }
		public Sprite Icon { get; }

		/// <summary>카테고리 내부 원본 데이터. BuildDetail 에서 cast 해서 사용.</summary>
		public object Source { get; }

		/// <summary>
		/// 골격 단계 = 항상 true. 후속 TASK 에서 unlock 레이어 도입 시
		/// `ICodexUnlockProvider` 같은 분리 인터페이스로 위임 예정.
		/// </summary>
		public bool IsUnlocked => true;

		public CodexEntry(string id, string displayName, Sprite icon, object source)
		{
			Id = id;
			DisplayName = displayName;
			Icon = icon;
			Source = source;
		}
	}
}
