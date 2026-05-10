using System.Collections.Generic;
using WitchMendokusai.NodeGraph;
using UnityEngine;
using NodeGraphAsset = WitchMendokusai.NodeGraph.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 챕터 — 퀘스트들의 dependency 그래프. <see cref="NodeGraph"/> 직접 상속 (TASK-WM-059 ★★ B, 2026-05-09).
	/// 「데이터 = 그래프」 단일 정본 (사용자 「근본의 근」 escalate) — 변환 어댑터 폐기.
	/// nodes (NodeBase List, base 의 [SerializeReference]) 안에 <see cref="QuestNode"/> wrapper 들이 들어감.
	///
	/// TASK-WM-059 polish B (2026-05-10) — Connections override.
	/// 디자이너 SerializeField 박은 connections (base) + UnlockEffects 자동 도출 connections 합산.
	/// EffectType.UnlockQuest 효과 (effect.Data = 다음 QuestSO) → from QuestNode → to QuestNode 자동 edge.
	/// </summary>
	[CreateAssetMenu(fileName = "Chapter_", menuName = "WM/Variable/ChapterSO")]
	public class ChapterSO : NodeGraphAsset
	{
		public override NodeDomain Domain => NodeDomain.MagicBook;

		public override IReadOnlyList<NodeConnection> Connections
		{
			get
			{
				List<NodeConnection> all = new(base.Connections);
				HashSet<(string, string)> seen = new();

				foreach (NodeConnection existing in base.Connections)
					seen.Add((existing.SourceNodeId, existing.TargetNodeId));

				foreach (NodeBase node in Nodes)
				{
					if (node is QuestNode questNode == false)
						continue;

					QuestSO source = questNode.Target;
					if (source == null || source.Data.UnlockEffects == null)
						continue;

					foreach (EffectInfo effect in source.Data.UnlockEffects)
					{
						if (effect.Type != EffectType.UnlockQuest)
							continue;

						QuestSO targetSO = effect.Data as QuestSO;
						if (targetSO == null)
							continue;

						NodeBase targetNode = FindQuestNode(targetSO);
						if (targetNode == null)
							continue;

						(string, string) key = (node.Id, targetNode.Id);
						if (seen.Add(key))
							all.Add(new NodeConnection(node.Id, string.Empty, targetNode.Id, string.Empty));
					}
				}

				return all;
			}
		}

		private NodeBase FindQuestNode(QuestSO target)
		{
			foreach (NodeBase node in Nodes)
			{
				if (node is QuestNode questNode == false)
					continue;
				if (questNode.Target == target)
					return node;
			}
			return null;
		}
	}
}
