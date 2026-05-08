using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 단일 노드 비주얼 (런타임). H1: generic 라벨+박스 — 노드 타입 이름 표시.
	/// H3 (Provider 패턴) 진입 시: NodeRuntimeProviderRegistry lookup → 도메인별 커스텀 VisualElement 로 본문 교체.
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

		/// <summary>노드 데이터 바인딩 — 라벨 갱신. H3 진입 시 Provider 가 body 교체.</summary>
		public void Bind(NodeBase node)
		{
			Node = node;
			titleLabel.text = node == null ? "?" : node.GetType().Name;
		}

		/// <summary>H3 Provider 패턴 진입 시 — body 안에 도메인별 비주얼 주입.</summary>
		public VisualElement Body => body;
	}
}
