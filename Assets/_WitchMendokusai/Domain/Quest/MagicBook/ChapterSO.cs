using WitchMendokusai.NodeGraph;
using UnityEngine;
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 챕터 — 퀘스트들의 dependency 그래프. <see cref="NodeGraph"/> 직접 상속 (TASK-WM-059 ★★ B, 2026-05-09).
	/// 「데이터 = 그래프」 단일 정본 (사용자 「근본의 근」 escalate) — 변환 어댑터 폐기.
	/// nodes (NodeBase List, base 의 [SerializeReference]) 안에 <see cref="QuestNode"/> wrapper 들이 들어감.
	/// </summary>
	[CreateAssetMenu(fileName = "Chapter_", menuName = "WM/Variable/ChapterSO")]
	public class ChapterSO : NodeGraphAsset
	{
		public override NodeDomain Domain => NodeDomain.MagicBook;
	}
}
