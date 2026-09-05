using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 데이터 탐색 화면의 카테고리/탭 plug-in 인터페이스.
	/// Discovery (런타임 도감) + DataSOWindow (에디터 툴) 등 데이터 탐색 도구 공통 베이스.
	/// (구) IDiscoveryCategory 의 일반화 — Discovery 도메인 색 빠지고 일반 데이터 탐색 톤.
	///
	/// 데이터 중심 — provider 는 entries + 디테일 빌드만 책임. 카드/그리드/사이드바 표시는 view 가 담당.
	/// 새 탐색 영역 추가 = `IEntryProvider` 구현 1개 + 도메인 registry 등록 1줄.
	/// </summary>
	public interface IEntryProvider
	{
		/// <summary>식별자. 예: "block", "item", "entity"</summary>
		string Id { get; }

		/// <summary>UI 표시 라벨. 예: "블록", "아이템", "주민"</summary>
		string DisplayName { get; }

		/// <summary>사이드바/탭 아이콘. null 허용 (텍스트 only fallback).</summary>
		Sprite Icon { get; }

		/// <summary>세부 분류 라벨 list. null/empty 면 사이드바에 "전체" 한 항목만.</summary>
		IReadOnlyList<string> SubGroups { get; }

		/// <summary>활성화 시 한 번 호출. 데이터 reload 등 lazy 초기화 hook.</summary>
		void OnActivate();

		/// <summary>이탈 시. 정리 또는 상태 보존.</summary>
		void OnDeactivate();

		/// <summary>이 provider 의 모든 entry. OnActivate 이후 호출됨.</summary>
		IReadOnlyList<EntryDescriptor> GetEntries();

		/// <summary>entry 선택 시 디테일 영역에 attach 될 VisualElement 빌드.</summary>
		VisualElement BuildDetail(EntryDescriptor entry);
	}
}
