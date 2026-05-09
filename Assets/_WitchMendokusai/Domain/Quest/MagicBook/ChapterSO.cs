using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using WitchMendokusai.NodeGraph;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WitchMendokusai
{
	/// <summary>
	/// 마도서 챕터 — 퀘스트들의 dependency 그래프. <see cref="NodeGraph"/> 직접 상속 (TASK-WM-059 ★★ B, 2026-05-09).
	/// 「데이터 = 그래프」 단일 정본 (사용자 「근본의 근」 escalate) — 변환 어댑터 폐기.
	/// nodes (NodeBase List, base 의 [SerializeReference]) 안에 <see cref="QuestNode"/> wrapper 들이 들어감.
	/// </summary>
	[CreateAssetMenu(fileName = "Chapter_", menuName = "WM/Variable/ChapterSO")]
	public class ChapterSO : NodeGraph
	{
		public override NodeDomain Domain => NodeDomain.MagicBook;

		// === 자산 마이그 호환 (TASK-WM-059 B, 2026-05-09) — B-cleanup commit 에서 제거 ===
		// 옛 schema: ChapterSO.Nodes (List<QuestNodeData>) — 자산 (Chapter_*.asset) load 시 nodesLegacy 로 매핑되어
		// OnEnable 에서 자동 nodes (NodeBase List, QuestNode wrapper) 로 변환. 변환 후 nodesLegacy.Clear + SetDirty.

		[Serializable]
		public struct QuestNodeData
		{
			public QuestSO Quest;
			public Vector2 Position;
		}

		[SerializeField]
		[FormerlySerializedAs("<Nodes>k__BackingField")]
		private List<QuestNodeData> nodesLegacy = new();

#if UNITY_EDITOR
		private void OnEnable()
		{
			MigrateLegacyIfNeeded();
		}

		private void MigrateLegacyIfNeeded()
		{
			if (nodesLegacy == null || nodesLegacy.Count == 0)
				return;

			int migratedCount = 0;
			foreach (QuestNodeData legacyData in nodesLegacy)
			{
				if (legacyData.Quest == null)
					continue;

				QuestNode questNode = new QuestNode();
				questNode.Target = legacyData.Quest;
				questNode.EditorPosition = legacyData.Position;
				AddNode(questNode);
				migratedCount++;
			}

			nodesLegacy.Clear();
			EditorUtility.SetDirty(this);
			Debug.Log($"[ChapterSO] {name} — auto-migrated {migratedCount} legacy nodes → QuestNode wrappers");
		}
#endif
	}
}
