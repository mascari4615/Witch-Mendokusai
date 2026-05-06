using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 도메인 노드 그래프 — 한 챕터 1개 = 한 그래프 자산. 노드 = 마법(페이지),
	/// edge = 진행 의존성 (앞 노드 완료 → 다음 언락, A3 단계). `Domain = MagicBook` 으로
	/// `NodeGraphView` 카탈로그 필터 통과 — 마도서 노드 (`IngredientNode`, `SpellNode`) 만 보임.
	///
	/// 평가 모델: Pull (지형 그래프와 동일 foundation 재사용).
	/// `IngredientNode` 가 재료 충족 비율 (0~1) 출력 → `SpellNode` 가 입력 + threshold 비교 후 진척 출력.
	/// 진행 매니저 (`ResearchProgressManager`, A 후속) 가 SpellNode 평가 결과 polling.
	/// </summary>
	[CreateAssetMenu(fileName = nameof(MagicBookGraph), menuName = "WM/MagicBook/" + nameof(MagicBookGraph))]
	public class MagicBookGraph : WitchMendokusai.NodeGraph.NodeGraph
	{
		public override NodeDomain Domain => NodeDomain.MagicBook;
	}
}
