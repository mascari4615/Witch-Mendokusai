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
		private static readonly Color NAME_COLOR = new Color(0.85f, 0.85f, 0.95f, 1f);

		public VisualElement Build(NodeBase node)
		{
			if (node is QuestNode questNode == false)
				return null;

			QuestSO target = questNode.Target;
			if (target == null)
				return null;

			VisualElement container = new VisualElement();
			container.style.flexDirection = FlexDirection.Row;
			container.style.alignItems = Align.Center;

			if (target.Sprite != null)
			{
				Image icon = new Image();
				icon.sprite = target.Sprite;
				icon.style.width = 24;
				icon.style.height = 24;
				icon.style.marginRight = 4;
				container.Add(icon);
			}

			Label nameLabel = new Label(target.Name);
			nameLabel.style.color = NAME_COLOR;
			container.Add(nameLabel);

			return container;
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
