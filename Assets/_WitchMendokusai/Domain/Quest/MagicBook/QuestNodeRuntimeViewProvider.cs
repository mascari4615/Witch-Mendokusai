using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.NodeGraph;
using WitchMendokusai.NodeGraph.Runtime;

namespace WitchMendokusai
{
	/// <summary>
	/// <see cref="QuestNode"/> 의 런타임 비주얼 + 인터랙션 Provider — TASK-WM-059 B 첫 사용처 (데드 hook 방지).
	/// body = QuestSO Sprite + Name 라벨. OnClicked = <see cref="QuestDetailRequestedEvent"/> 발행
	/// (TASK-WM-059 D, 2026-05-10) — Provider ↔ Host 결합 0, EventBus (086 IEvent 인프라) 첫 도메인 사용처.
	/// </summary>
	[NodeRuntimeView(typeof(QuestNode))]
	public sealed class QuestNodeRuntimeViewProvider : INodeRuntimeViewProvider
	{
		private static readonly Color NAME_COLOR = new(0.85f, 0.85f, 0.95f, 1f);
		private static readonly Color HIGHLIGHT_CAN_WORK = new(1f, 0.85f, 0.4f, 1f);
		private static readonly Color HIGHLIGHT_CAN_COMPLETE = new(0.45f, 0.95f, 0.55f, 1f);
		private static readonly Color LOCKED_TINT = new(0.45f, 0.45f, 0.5f, 1f);
		private const float OPACITY_LOCKED = 0.35f;
		private const float OPACITY_COMPLETED = 0.55f;

		public VisualElement Build(NodeBase node)
		{
			if (node is QuestNode questNode == false)
				return null;

			QuestSO target = questNode.Target;
			if (target == null)
				return null;

			QuestState gateState = TryGetQuestState(target.ID);
			RuntimeQuest runtimeQuest = TryGetRuntimeQuest(target);

			bool isLocked = gateState == QuestState.Locked;
			bool isCompleted = gateState == QuestState.Completed
				|| (runtimeQuest != null && runtimeQuest.State == RuntimeQuestState.Completed);

			VisualElement container = new();
			container.style.flexDirection = FlexDirection.Row;
			container.style.alignItems = Align.Center;

			if (isLocked)
				container.style.opacity = OPACITY_LOCKED;
			else if (isCompleted)
				container.style.opacity = OPACITY_COMPLETED;

			if (target.Sprite != null)
			{
				Image icon = new();
				icon.sprite = target.Sprite;
				icon.style.width = 24;
				icon.style.height = 24;
				icon.style.marginRight = 4;
				if (isLocked)
					icon.tintColor = LOCKED_TINT;
				container.Add(icon);
			}

			Color nameColor = NAME_COLOR;
			if (runtimeQuest != null)
			{
				if (runtimeQuest.State == RuntimeQuestState.CanWork)
					nameColor = HIGHLIGHT_CAN_WORK;
				else if (runtimeQuest.State == RuntimeQuestState.CanComplete)
					nameColor = HIGHLIGHT_CAN_COMPLETE;
			}

			Label nameLabel = new(target.Name);
			nameLabel.style.color = nameColor;
			container.Add(nameLabel);

			return container;
		}

		private static QuestState TryGetQuestState(int questID)
		{
			if (QuestManager.Instance == null)
				return QuestState.Locked;
			Dictionary<int, QuestState> states = QuestManager.Instance.GetQuestStates();
			if (states == null)
				return QuestState.Locked;
			if (states.TryGetValue(questID, out QuestState state))
				return state;
			return QuestState.Locked;
		}

		private static RuntimeQuest TryGetRuntimeQuest(QuestSO target)
		{
			if (QuestManager.Instance == null)
				return null;
			return QuestManager.Instance.GetQuest(target);
		}

		public void OnClicked(NodeBase node)
		{
			if (node is QuestNode questNode == false)
				return;

			QuestSO target = questNode.Target;
			if (target == null)
				return;

			EventBus.Instance.Publish(new QuestDetailRequestedEvent(target));
		}
	}
}
