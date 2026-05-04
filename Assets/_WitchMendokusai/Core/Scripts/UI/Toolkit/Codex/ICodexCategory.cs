using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도감 카테고리 plug-in 인터페이스. 새 카테고리 = 구현 1개 + CodexCategoryRegistry.Register 1줄.
	///
	/// DevMode (`IDevMode`) 와 다른 점: 임의 UI 가 아니라 *데이터 + 디테일 빌드* 만 강제.
	/// 윈도우(`CodexWindow`) 가 사이드바·그리드·디테일 영역 다 만들어줌.
	/// 카테고리는 "어떤 항목들" + "선택 시 어떤 디테일" 만 알면 됨.
	/// </summary>
	public interface ICodexCategory
	{
		/// <summary>카테고리 식별자. 예: "block", "item", "entity"</summary>
		string Id { get; }

		/// <summary>사이드바에 표시될 라벨. 예: "블록", "아이템", "주민"</summary>
		string DisplayName { get; }

		/// <summary>Root 화면 큰 주제 버튼 아이콘. null 허용 (텍스트 only fallback).</summary>
		Sprite Icon { get; }

		/// <summary>
		/// Category 모드 좌측 사이드바에 표시할 세부 분류 라벨. null/empty 면 "전체" 한 항목만.
		/// 사이드바 클릭 시 entry.SubGroup 가 일치하는 것만 그리드 표시.
		/// </summary>
		IReadOnlyList<string> SubGroups { get; }

		/// <summary>카테고리 활성화 시 한 번 호출. 데이터 reload 등 lazy 초기화 hook.</summary>
		void OnActivate();

		/// <summary>카테고리 이탈 시. 정리 또는 상태 보존.</summary>
		void OnDeactivate();

		/// <summary>카테고리에 속하는 모든 항목. OnActivate 이후 호출됨.</summary>
		IReadOnlyList<CodexEntry> GetEntries();

		/// <summary>항목 선택 시 우측 디테일 영역에 attach 될 VisualElement 빌드.</summary>
		VisualElement BuildDetail(CodexEntry entry);
	}
}
