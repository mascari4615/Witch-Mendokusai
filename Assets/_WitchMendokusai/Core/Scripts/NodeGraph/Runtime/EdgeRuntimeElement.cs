using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.NodeGraph.Runtime
{
	/// <summary>
	/// 두 노드 사이 엣지 라인 (런타임). Painter2D 로 LineTo 그림 — 곡선/화살표 자유.
	/// 엔드포인트 (source/target VisualElement) 의 layout 변동 시 자동 MarkDirtyRepaint.
	///
	/// H1: 직선 라인 + 단일 색. 후속 단계: H3 Provider 가 도메인별 색/굵기/곡선/화살표 결정.
	/// </summary>
	public class EdgeRuntimeElement : VisualElement
	{
		public const string USS_CLASS = "wm-edge-runtime";

		private VisualElement sourceElement;
		private VisualElement targetElement;
		private Color lineColor = new Color(0.7f, 0.7f, 0.8f, 1f);
		private float lineWidth = 2f;

		public EdgeRuntimeElement()
		{
			AddToClassList(USS_CLASS);

			pickingMode = PickingMode.Ignore;
			style.position = Position.Absolute;
			style.left = 0;
			style.top = 0;
			style.right = 0;
			style.bottom = 0;

			generateVisualContent += OnGenerateVisualContent;
			RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
		}

		/// <summary>엔드포인트 설정 — source / target 의 layout 변동 시 자동 repaint.</summary>
		public void SetEndpoints(VisualElement source, VisualElement target)
		{
			UnregisterEndpointCallbacks();

			sourceElement = source;
			targetElement = target;

			if (sourceElement != null)
				sourceElement.RegisterCallback<GeometryChangedEvent>(OnEndpointGeometryChanged);

			if (targetElement != null)
				targetElement.RegisterCallback<GeometryChangedEvent>(OnEndpointGeometryChanged);

			MarkDirtyRepaint();
		}

		public void SetLineColor(Color color)
		{
			lineColor = color;
			MarkDirtyRepaint();
		}

		public void SetLineWidth(float width)
		{
			lineWidth = width;
			MarkDirtyRepaint();
		}

		private void OnEndpointGeometryChanged(GeometryChangedEvent evt) => MarkDirtyRepaint();

		private void OnDetachFromPanel(DetachFromPanelEvent evt) => UnregisterEndpointCallbacks();

		private void UnregisterEndpointCallbacks()
		{
			if (sourceElement != null)
				sourceElement.UnregisterCallback<GeometryChangedEvent>(OnEndpointGeometryChanged);

			if (targetElement != null)
				targetElement.UnregisterCallback<GeometryChangedEvent>(OnEndpointGeometryChanged);
		}

		private void OnGenerateVisualContent(MeshGenerationContext context)
		{
			if (sourceElement == null || targetElement == null)
				return;

			Rect sourceLayout = sourceElement.layout;
			Rect targetLayout = targetElement.layout;

			if (sourceLayout.width <= 0f || targetLayout.width <= 0f)
				return;

			Vector2 fromCenter = sourceLayout.center;
			Vector2 toCenter = targetLayout.center;

			Painter2D painter = context.painter2D;
			painter.strokeColor = lineColor;
			painter.lineWidth = lineWidth;
			painter.lineCap = LineCap.Round;

			painter.BeginPath();
			painter.MoveTo(fromCenter);
			painter.LineTo(toCenter);
			painter.Stroke();
		}
	}
}
