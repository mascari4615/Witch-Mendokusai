using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	// 희귀 카드 뒤 회전 빛줄기. 그림 없이 삼각형으로 그림, 실제 시간 기준
	[RequireComponent(typeof(CanvasRenderer))]
	public class UISlotRays : MaskableGraphic
	{
		// 수치 정본은 CardRevealStyle, Configure 로 받음
		private int rayCount = 1;
		private float rayWidth;
		private float rotationSpeed;

		public static UISlotRays Create(Transform parent, int siblingIndex)
		{
			GameObject rayObject = new("[Rays] Reveal", typeof(RectTransform), typeof(CanvasRenderer), typeof(UISlotRays), typeof(LayoutElement));
			rayObject.GetComponent<LayoutElement>().ignoreLayout = true;
			RectTransform rect = (RectTransform)rayObject.transform;
			rect.SetParent(parent, false);
			rect.SetSiblingIndex(siblingIndex);
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			UISlotRays rays = rayObject.GetComponent<UISlotRays>();
			rays.raycastTarget = false;
			rayObject.SetActive(false);
			return rays;
		}

		public void Configure(int count, float width, float speed)
		{
			rayCount = count;
			rayWidth = width;
			rotationSpeed = speed;
			SetVerticesDirty();
		}

		private void Update() => rectTransform.Rotate(0f, 0f, -rotationSpeed * Time.unscaledDeltaTime);

		protected override void OnPopulateMesh(VertexHelper vertexHelper)
		{
			vertexHelper.Clear();
			Rect rect = GetPixelAdjustedRect();
			Vector2 center = rect.center;
			float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
			float halfAngle = Mathf.PI / rayCount * rayWidth;
			Color32 innerColor = color;
			Color32 outerColor = new Color(color.r, color.g, color.b, 0f);
			for (int i = 0; i < rayCount; i++)
			{
				float angle = i * Mathf.PI * 2f / rayCount;
				Vector2 left = new(Mathf.Cos(angle - halfAngle), Mathf.Sin(angle - halfAngle));
				Vector2 right = new(Mathf.Cos(angle + halfAngle), Mathf.Sin(angle + halfAngle));
				int baseIndex = vertexHelper.currentVertCount;
				vertexHelper.AddVert(center, innerColor, Vector2.zero);
				vertexHelper.AddVert(center + left * radius, outerColor, Vector2.zero);
				vertexHelper.AddVert(center + right * radius, outerColor, Vector2.zero);
				vertexHelper.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
			}
		}
	}
}
