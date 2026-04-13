using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class UIChapter : UIBase
	{
		[SerializeField] private Transform nodesRoot;
		[SerializeField] private UIQuestNode nodePrefab;
		[SerializeField] private RectTransform content;

		private readonly List<UIQuestNode> nodes = new();
		private ToolTip toolTip;
		private UIQuestToolTip questToolTip;

		public override void Init() { }

		public void SetData(ChapterSO chapterSO)
		{
			foreach (UIQuestNode node in nodes)
				Destroy(node.gameObject);
			nodes.Clear();

			foreach (QuestNodeData nodeData in chapterSO.Nodes)
			{
				UIQuestNode node = Instantiate(nodePrefab, nodesRoot);
				node.SetNode(nodeData.Quest, nodeData.Position);
				node.Init();
				nodes.Add(node);
			}

			if (toolTip != null)
				ApplyToolTip();
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
		}
	}
}
