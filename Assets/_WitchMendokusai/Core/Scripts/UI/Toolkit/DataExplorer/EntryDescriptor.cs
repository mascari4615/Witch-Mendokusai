using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 데이터 탐색 항목 한 개. (구) CodexEntry 의 일반화.
	/// Provider 가 자체 데이터(DataSO 등) → EntryDescriptor list 변환해 노출.
	///
	/// Source 는 의도적으로 untyped — provider 내부에서만 어떤 타입인지 알면 됨.
	/// BuildDetail 에서 provider 가 typecheck 해서 처리.
	/// </summary>
	public class EntryDescriptor
	{
		public string Id { get; }
		public string DisplayName { get; }
		public Sprite Icon { get; }

		/// <summary>provider 내부 원본 데이터. BuildDetail 에서 cast 해서 사용.</summary>
		public object Source { get; }

		/// <summary>
		/// grade 테두리 USS class 변환용 (`wm-codex-card--grade-{key}`). nullable.
		/// ItemData 만 채움 (`item.Grade.ToString().ToLowerInvariant()`). 다른 provider 는 null = default 테두리.
		/// </summary>
		public string GradeKey { get; }

		/// <summary>
		/// 어느 세부 분류(SubGroup)에 속하는지. nullable — null 이면 "전체" 에서만 표시.
		/// provider 의 `SubGroups` 라벨과 일치해야 사이드바 필터 동작.
		/// </summary>
		public string SubGroup { get; }

		/// <summary>
		/// 디테일 일러스트 영역에 표시할 3D 모델 prefab. nullable.
		/// non-null = `CodexPreviewController` 가 instantiate + 회전. null = 정적 Icon 표시.
		/// </summary>
		public GameObject PreviewPrefab { get; }

		/// <summary>
		/// 골격 단계 = 항상 true. 후속 TASK 에서 unlock 레이어 도입 시
		/// `IUnlockProvider` 같은 분리 인터페이스로 위임 예정.
		/// </summary>
		public bool IsUnlocked => true;

		public EntryDescriptor(string id, string displayName, Sprite icon, object source, string gradeKey = null, string subGroup = null, GameObject previewPrefab = null)
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
