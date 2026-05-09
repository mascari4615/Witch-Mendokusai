using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 단일 노드 비주얼 (런타임). 타이틀 라벨 (노드 타입 이름) + body (Provider 가 채움).
	/// H3 (2026-05-09): Bind 시 <see cref="NodeRuntimeProviderRegistry"/> lookup → Provider.Build 결과를 body 에 주입.
	/// 미등록 타입은 <see cref="DefaultNodeRuntimeViewProvider"/> 가 null 반환 → body 비어있음 (타이틀만).
	/// </summary>
	public class NodeRuntimeElement : VisualElement
	{
		public const string USS_CLASS = "wm-node-runtime";
		public const string USS_TITLE = "wm-node-runtime__title";
		public const string USS_BODY = "wm-node-runtime__body";

		private readonly Label titleLabel;
		private readonly VisualElement body;

		public NodeBase Node { get; private set; }

		public NodeRuntimeElement()
		{
			AddToClassList(USS_CLASS);

			style.minWidth = 100;
			style.minHeight = 30;
			style.paddingTop = 4;
			style.paddingBottom = 4;
			style.paddingLeft = 8;
			style.paddingRight = 8;
			style.backgroundColor = new Color(0.18f, 0.18f, 0.22f, 0.95f);
			style.borderTopWidth = 1;
			style.borderBottomWidth = 1;
			style.borderLeftWidth = 1;
			style.borderRightWidth = 1;
			Color borderColor = new Color(0.4f, 0.4f, 0.5f, 1f);
			style.borderTopColor = borderColor;
			style.borderBottomColor = borderColor;
			style.borderLeftColor = borderColor;
			style.borderRightColor = borderColor;
			style.borderTopLeftRadius = 4;
			style.borderTopRightRadius = 4;
			style.borderBottomLeftRadius = 4;
			style.borderBottomRightRadius = 4;

			titleLabel = new Label();
			titleLabel.AddToClassList(USS_TITLE);
			titleLabel.style.color = new Color(0.9f, 0.9f, 0.95f, 1f);
			titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			Add(titleLabel);

			body = new VisualElement();
			body.AddToClassList(USS_BODY);
			Add(body);
		}

		/// <summary>노드 데이터 바인딩 — 타이틀 라벨 + Provider 로 body 채움. 호출마다 body 전체 재구성.</summary>
		public void Bind(NodeBase node)
		{
			Node = node;
			titleLabel.text = node == null ? "?" : node.GetType().Name;

			body.Clear();
			if (node == null)
				return;

			INodeRuntimeViewProvider provider = NodeRuntimeProviderRegistry.GetProvider(node.GetType());
			VisualElement bodyView = provider.Build(node);
			if (bodyView != null)
				body.Add(bodyView);
		}

		/// <summary>외부에서 body 직접 조작 시 (Provider 우회) — 일반 사용처는 Bind 로.</summary>
		public VisualElement Body => body;
	}
}
