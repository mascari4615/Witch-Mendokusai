using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 데이터 탐색 항목 한 개. (구) DiscoveryEntry 의 일반화.
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
		/// grade 테두리 USS class 변환용 (`wm-discovery-card--grade-{key}`). nullable.
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
		/// non-null = `DiscoveryPreviewController` 가 instantiate + 회전. null = 정적 Icon 표시.
		/// </summary>
		public GameObject PreviewPrefab { get; }

		/// <summary>
		/// 이 항목이 열렸나 — provider 가 채운다. 안 주면 열림 (에디터 도구는 잠금 개념이 없다).
		/// 조건은 도감이 정하지 않는다: provider 가 `DiscoveryUnlocks` 에 물어 그 답을 여기 담는다.
		/// </summary>
		public bool IsUnlocked { get; }

		public EntryDescriptor(string id, string displayName, Sprite icon, object source, string gradeKey = null, string subGroup = null, GameObject previewPrefab = null, bool isUnlocked = true)
		{
			Id = id;
			DisplayName = displayName;
			Icon = icon;
			Source = source;
			GradeKey = gradeKey;
			SubGroup = subGroup;
			PreviewPrefab = previewPrefab;
			IsUnlocked = isUnlocked;
		}
	}
}
