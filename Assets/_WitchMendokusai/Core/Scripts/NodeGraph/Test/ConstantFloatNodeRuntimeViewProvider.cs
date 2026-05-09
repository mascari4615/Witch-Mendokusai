using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.NodeGraph.Runtime;

namespace WitchMendokusai.NodeGraph
{
	/// <summary>
	/// <see cref="ConstantFloatNode"/> 의 런타임 비주얼 Provider — 첫 사용처 (TASK-WM-034 H3 데드 인터페이스 방지).
	/// body 안에 `= 1.23` 형식 라벨 1줄 표시. 후속 도메인 Provider (QuestNode 등) 의 패턴 demo.
	/// </summary>
	[NodeRuntimeView(typeof(ConstantFloatNode))]
	public sealed class ConstantFloatNodeRuntimeViewProvider : INodeRuntimeViewProvider
	{
		private static readonly Color VALUE_COLOR = new Color(0.7f, 0.9f, 0.7f, 1f);

		public VisualElement Build(NodeBase node)
		{
			if (node is ConstantFloatNode constantNode == false)
				return null;

			Label valueLabel = new Label
			{
				text = $"= {constantNode.Value:F2}"
			};
			valueLabel.style.color = VALUE_COLOR;
			valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
			return valueLabel;
		}
	}
}
