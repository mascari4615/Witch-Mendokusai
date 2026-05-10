using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 단일 노드 비주얼 (런타임). 타이틀 라벨 (노드 타입 이름) + body (Provider 가 채움).
	/// H3 (2026-05-09): Bind 시 <see cref="NodeRuntimeProviderRegistry"/> lookup → Provider.Build 결과를 body 에 주입.
	/// H4 (2026-05-09): Pointer event (좌클릭 / hover) → 자체 <see cref="Clicked"/> / <see cref="Hovered"/> / <see cref="Unhovered"/> event 발사.
	///                    USS class (`is-hovered`, `is-selected`) 토글 + inline border color 3색.
	///                    실제 도메인 행동 dispatch + selection 관리는 <see cref="NodeGraphRuntimeView"/> 가 책임.
	/// </summary>
	public class NodeRuntimeElement : VisualElement
	{
		public const string USS_CLASS = "wm-node-runtime";
		public const string USS_TITLE = "wm-node-runtime__title";
		public const string USS_BODY = "wm-node-runtime__body";
		public const string USS_HOVERED = "wm-node-runtime--hovered";
		public const string USS_SELECTED = "wm-node-runtime--selected";

		private static readonly Color DEFAULT_BORDER_COLOR = new Color(0.4f, 0.4f, 0.5f, 1f);
		private static readonly Color HOVER_BORDER_COLOR = new Color(0.6f, 0.6f, 0.8f, 1f);
		private static readonly Color SELECTED_BORDER_COLOR = new Color(0.4f, 0.7f, 1.0f, 1f);

		private readonly Label titleLabel;
		private readonly VisualElement body;

		private bool isHovered;
		private bool isSelected;

		public NodeBase Node { get; private set; }

		public bool IsSelected => isSelected;

		/// <summary>좌클릭 발생. NodeGraphRuntimeView 가 구독해 Provider 호출 + selection 관리.</summary>
		public event Action<NodeRuntimeElement> Clicked = delegate { };
		/// <summary>Hover 시작.</summary>
		public event Action<NodeRuntimeElement> Hovered = delegate { };
		/// <summary>Hover 해제.</summary>
		public event Action<NodeRuntimeElement> Unhovered = delegate { };

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
			style.borderTopLeftRadius = 4;
			style.borderTopRightRadius = 4;
			style.borderBottomLeftRadius = 4;
			style.borderBottomRightRadius = 4;
			ApplyBorderColor();

			titleLabel = new Label();
			titleLabel.AddToClassList(USS_TITLE);
			titleLabel.style.color = new Color(0.9f, 0.9f, 0.95f, 1f);
			titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
			Add(titleLabel);

			body = new VisualElement();
			body.AddToClassList(USS_BODY);
			Add(body);

			RegisterCallback<PointerDownEvent>(OnPointerDown);
			RegisterCallback<PointerEnterEvent>(OnPointerEnter);
			RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
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

		/// <summary>NodeGraphRuntimeView 가 selection 변경 시 호출. USS class + 테두리 색 갱신.</summary>
		public void SetSelected(bool selected)
		{
			if (isSelected == selected)
				return;

			isSelected = selected;
			if (selected)
				AddToClassList(USS_SELECTED);
			else
				RemoveFromClassList(USS_SELECTED);

			ApplyBorderColor();
		}

		private void OnPointerDown(PointerDownEvent evt)
		{
			if (evt.button != 0)
				return;

			Clicked.Invoke(this);
			evt.StopPropagation();
		}

		private void OnPointerEnter(PointerEnterEvent evt)
		{
			isHovered = true;
			AddToClassList(USS_HOVERED);
			ApplyBorderColor();
			Hovered.Invoke(this);
		}

		private void OnPointerLeave(PointerLeaveEvent evt)
		{
			isHovered = false;
			RemoveFromClassList(USS_HOVERED);
			ApplyBorderColor();
			Unhovered.Invoke(this);
		}

		private void ApplyBorderColor()
		{
			Color borderColor = isSelected ? SELECTED_BORDER_COLOR
				: isHovered ? HOVER_BORDER_COLOR
				: DEFAULT_BORDER_COLOR;
			style.borderTopColor = borderColor;
			style.borderBottomColor = borderColor;
			style.borderLeftColor = borderColor;
			style.borderRightColor = borderColor;
		}
	}
}
