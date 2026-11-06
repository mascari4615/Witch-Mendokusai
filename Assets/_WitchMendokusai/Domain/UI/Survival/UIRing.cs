using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 텍스처 없는 메시 고리
	[RequireComponent(typeof(CanvasRenderer))]
	public class UIRing : MaskableGraphic
	{
		[SerializeField, Range(0f, 1f)] private float fill = 1f;
		[SerializeField, Range(0f, 1f)] private float preview = 0f;
		[SerializeField] private float thickness = 10f;
		[SerializeField] private int segmentCount = 0;
		[SerializeField] private float segmentGapDegrees = 3f;
		[SerializeField] private float edgeFeather = 1f;
		[SerializeField] private float degreesPerStep = 4f;
		[SerializeField] private Color trackColor = new(0f, 0f, 0f, 0.65f);
		[SerializeField] private Color previewColor = new(1f, 1f, 1f, 0.55f);

		public float Fill => fill;

		public void SetFill(float newFill, float newPreview = 0f)
		{
			fill = Mathf.Clamp01(newFill);
			preview = Mathf.Clamp01(newPreview);
			SetVerticesDirty();
		}

		protected override void OnPopulateMesh(VertexHelper vertexHelper)
		{
			vertexHelper.Clear();
			Rect area = GetPixelAdjustedRect();
			float outer = Mathf.Min(area.width, area.height) * 0.5f;
			float inner = Mathf.Max(0f, outer - thickness);
			Vector2 center = area.center;

			if (segmentCount <= 0)
			{
				AddArc(vertexHelper, center, inner, outer, 0f, 360f, trackColor);
				if (preview > fill)
					AddArc(vertexHelper, center, inner, outer, 0f, preview * 360f, previewColor);
				if (fill > 0f)
					AddArc(vertexHelper, center, inner, outer, 0f, fill * 360f, color);
				return;
			}

			float step = 360f / segmentCount;
			for (int i = 0; i < segmentCount; i++)
			{
				float ratio = (i + 0.5f) / segmentCount;
				Color segmentColor = ratio < fill ? color : ratio < preview ? previewColor : trackColor;
				AddArc(vertexHelper, center, inner, outer, i * step + segmentGapDegrees * 0.5f, (i + 1) * step - segmentGapDegrees * 0.5f, segmentColor);
			}
		}

		private void AddArc(VertexHelper vertexHelper, Vector2 center, float inner, float outer, float startDegrees, float endDegrees, Color arcColor)
		{
			int steps = Mathf.Max(1, Mathf.CeilToInt((endDegrees - startDegrees) / degreesPerStep));
			AddStrip(vertexHelper, center, inner - edgeFeather, inner, startDegrees, endDegrees, steps, arcColor, 0f, 1f);
			AddStrip(vertexHelper, center, inner, outer, startDegrees, endDegrees, steps, arcColor, 1f, 1f);
			AddStrip(vertexHelper, center, outer, outer + edgeFeather, startDegrees, endDegrees, steps, arcColor, 1f, 0f);
		}

		private static void AddStrip(VertexHelper vertexHelper, Vector2 center, float radiusA, float radiusB, float startDegrees, float endDegrees, int steps, Color stripColor, float alphaA, float alphaB)
		{
			if (radiusA < 0f)
				radiusA = 0f;
			Color32 colorA = new Color(stripColor.r, stripColor.g, stripColor.b, stripColor.a * alphaA);
			Color32 colorB = new Color(stripColor.r, stripColor.g, stripColor.b, stripColor.a * alphaB);
			int baseIndex = vertexHelper.currentVertCount;
			for (int i = 0; i <= steps; i++)
			{
				float degrees = Mathf.Lerp(startDegrees, endDegrees, (float)i / steps);
				float radians = (90f - degrees) * Mathf.Deg2Rad;
				Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
				vertexHelper.AddVert(center + direction * radiusA, colorA, Vector2.zero);
				vertexHelper.AddVert(center + direction * radiusB, colorB, Vector2.zero);
				if (i < steps)
				{
					int current = baseIndex + i * 2;
					vertexHelper.AddTriangle(current, current + 1, current + 3);
					vertexHelper.AddTriangle(current, current + 3, current + 2);
				}
			}
		}
	}
}
