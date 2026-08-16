using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// 은은한 격자 바닥.
	///
	/// ★ 왜 있나 — <b>밋밋한 검정은 「꺼진 화면」처럼 보인다</b>. 아무것도 안 그린 곳과
	///   일부러 비워 둔 곳이 눈에는 똑같아서, 화면 전체가 미완성처럼 읽힌다.
	///   선 몇 줄이면 「여기가 판이다」가 생긴다 — 아트가 0 이고 기하학 톤과도 맞는다.
	///
	/// ★ <b>아주 흐리게</b>. 바닥이 눈에 띄면 그건 바닥이 아니라 무늬다.
	///   위에서 도는 도형이 주인공이고 여기는 그 자리를 잡아 주기만 한다.
	///
	/// ★ 천천히 흐른다 — 멈춘 배경은 정지 화면처럼 보인다. 다만 <b>알아채기 직전</b>의 속도로.
	/// </summary>
	public sealed class GridBackdropElement : VisualElement
	{
		private float drift;

		public GridBackdropElement()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
		}

		/// <summary>칸 하나의 크기(픽셀).</summary>
		public float Spacing { get; set; } = 26f;

		/// <summary>선 색 — 바탕과 거의 같아야 한다.</summary>
		public Color Line { get; set; } = new Color(1f, 1f, 1f, 0.035f);

		/// <summary>초당 몇 픽셀 흐르나.</summary>
		public float DriftPerSecond { get; set; } = 3f;

		public void Advance(float deltaSeconds)
		{
			drift += deltaSeconds * DriftPerSecond;
			if (drift >= Spacing)
			{
				drift -= Spacing;
			}

			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			Rect box = contentRect;
			if (box.width <= 4f || box.height <= 4f || Spacing <= 2f)
			{
				return;
			}

			Painter2D painter = context.painter2D;
			painter.strokeColor = Line;
			painter.lineWidth = 1f;
			painter.BeginPath();

			for (float x = -drift; x < box.width; x += Spacing)
			{
				painter.MoveTo(new Vector2(x, 0f));
				painter.LineTo(new Vector2(x, box.height));
			}

			for (float y = -drift; y < box.height; y += Spacing)
			{
				painter.MoveTo(new Vector2(0f, y));
				painter.LineTo(new Vector2(box.width, y));
			}

			painter.Stroke();
		}
	}
}
