using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIChapter : UIBase
	{
		[SerializeField] private Transform nodesRoot;
		[SerializeField] private Transform edgesRoot;
		[SerializeField] private UIQuestNode nodePrefab;
		[SerializeField] private UIQuestEdge edgePrefab;
		[SerializeField] private RectTransform content;

		private readonly List<UIQuestNode> nodes = new();
		private readonly List<UIQuestEdge> edges = new();
		private ToolTip toolTip;
		private UIQuestToolTip questToolTip;

		public override void Init() { }

		public void SetData(ChapterSO chapterSO)
		{
			foreach (UIQuestNode node in nodes)
				Destroy(node.gameObject);
			nodes.Clear();

			foreach (UIQuestEdge edge in edges)
				Destroy(edge.gameObject);
			edges.Clear();

			foreach (QuestNodeData nodeData in chapterSO.Nodes)
			{
				UIQuestNode node = Instantiate(nodePrefab, nodesRoot);
				node.SetNode(nodeData.Quest, nodeData.Position);
				node.Init();
				nodes.Add(node);
			}

			BuildEdges();

			if (toolTip != null)
				ApplyToolTip();
		}

		private void BuildEdges()
		{
			if (edgePrefab == null || edgesRoot == null)
				return;

			foreach (UIQuestNode fromNode in nodes)
			{
				QuestSO questSO = fromNode.DataSO as QuestSO;
				if (questSO == null)
					continue;

				foreach (EffectInfo effect in questSO.Data.CompleteEffects)
				{
					if (effect.Type != EffectType.UnlockQuest)
						continue;

					UIQuestNode toNode = null;
					foreach (UIQuestNode n in nodes)
					{
						if (n.DataSO == effect.Data)
						{
							toNode = n;
							break;
						}
					}

					if (toNode == null)
						continue;

					UIQuestEdge edge = Instantiate(edgePrefab, edgesRoot);
					edge.SetEdge((RectTransform)fromNode.transform, (RectTransform)toNode.transform);
					edge.SetFromNode(fromNode);
					edge.transform.SetAsFirstSibling();
					edges.Add(edge);
				}
			}
		}

		public void SetToolTip(ToolTip toolTip, UIQuestToolTip questToolTip)
		{
			this.toolTip = toolTip;
			this.questToolTip = questToolTip;
			ApplyToolTip();
		}

		private void ApplyToolTip()
		{
			foreach (UIQuestNode node in nodes)
			{
				node.ToolTipTrigger.SetClickToolTip(toolTip);
				node.SetClickAction((slot) =>
				{
					slot.ToolTipTrigger.ClickToolTip.gameObject.SetActive(true);
					toolTip.SetToolTipContent(slot.Data);
					RuntimeQuest quest = QuestManager.Instance.GetQuest(slot.DataSO as QuestSO);
					questToolTip.SetQuest(quest);
					questToolTip.UpdateUI();
				});
			}
		}

		protected override void OnOpen()
		{
			content.anchoredPosition = Vector2.zero;
		}

		public override void UpdateUI()
		{
			foreach (UIQuestNode node in nodes)
			{
				RuntimeQuest runtimeQuest = QuestManager.Instance.GetQuest(node.DataSO as QuestSO);

				node.SetDisable(false);

				if (runtimeQuest != null)
				{
					node.SetRuntimeQuestState(runtimeQuest.State);
					node.SetQuest(runtimeQuest);
				}
				else
				{
					QuestSO questData = node.DataSO as QuestSO;
					QuestState state = QuestManager.Instance.GetQuestState(questData.ID);
					node.SetDisable(state == QuestState.Locked);
				}

				node.UpdateUI();
			}

			foreach (UIQuestEdge edge in edges)
			{
				QuestSO fromQuestSO = edge.FromNode.DataSO as QuestSO;
				RuntimeQuest rq = QuestManager.Instance.GetQuest(fromQuestSO);
				QuestState state = rq != null ? rq.State : QuestManager.Instance.GetQuestState(fromQuestSO.ID);
				edge.SetState(state);
			}
		}
	}
}
