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
		/// grade 테두리 USS class 변환용 (`wm-codex-card--grade-{key}`). nullable.
		/// ItemData 만 채움 (`item.Grade.ToString().ToLowerInvariant()`). 다른 카테고리는 null = default 테두리.
		/// </summary>
		public string GradeKey { get; }

		/// <summary>
		/// 어느 세부 분류(SubGroup)에 속하는지. nullable — null 이면 "전체" 에서만 표시.
		/// 카테고리의 `SubGroups` 라벨과 일치해야 사이드바 필터 동작.
		/// </summary>
		public string SubGroup { get; }

		/// <summary>
		/// 디테일 일러스트 영역에 표시할 3D 모델 prefab. nullable.
		/// non-null = `CodexPreviewController` 가 instantiate + 회전. null = 정적 Icon 표시.
		/// 카테고리가 entry 만들 때 채움 (Block/Entity 등). Item 은 일반적으로 null.
		/// </summary>
		public GameObject PreviewPrefab { get; }

		/// <summary>
		/// 골격 단계 = 항상 true. 후속 TASK 에서 unlock 레이어 도입 시
		/// `ICodexUnlockProvider` 같은 분리 인터페이스로 위임 예정.
		/// </summary>
		public bool IsUnlocked => true;

		public CodexEntry(string id, string displayName, Sprite icon, object source, string gradeKey = null, string subGroup = null, GameObject previewPrefab = null)
		{
			Id = id;
			DisplayName = displayName;
			Icon = icon;
			Source = source;
			GradeKey = gradeKey;
			SubGroup = subGroup;
			PreviewPrefab = previewPrefab;
		}
	}
}
