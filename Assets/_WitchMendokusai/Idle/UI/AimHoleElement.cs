using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Idle.UI
{
	/// <summary>
	/// 대상 지정 중 화면을 어둡게 덮되 커서 아래 바닥 타원만 밝게 남기는 층 (피드백 11, 참고 실측 2026-09-22:
	/// 밖 24%, 안은 밖의 1.9배, 바닥에 붙은 타원, 테두리 한 줄).
	///
	/// ★ USS 는 구멍을 못 뚫는다. Painter2D 의 홀짝 채우기로 화면 사각에서 타원을 뺀다.
	///   색, 반지름, 눌린 비율, 선 굵기는 USS 사용자 속성 (--aim-*) 에서 읽는다. 코드에 수치 없음
	/// </summary>
	internal sealed class AimHoleElement : VisualElement
	{
		private static readonly CustomStyleProperty<Color> DIM_COLOR = new CustomStyleProperty<Color>("--aim-dim-color");
		private static readonly CustomStyleProperty<Color> RING_COLOR = new CustomStyleProperty<Color>("--aim-ring-color");
		private static readonly CustomStyleProperty<Color> RING_READY_COLOR = new CustomStyleProperty<Color>("--aim-ring-ready-color");
		private static readonly CustomStyleProperty<float> RING_WIDTH = new CustomStyleProperty<float>("--aim-ring-width");
		private static readonly CustomStyleProperty<float> RADIUS = new CustomStyleProperty<float>("--aim-radius");
		private static readonly CustomStyleProperty<float> SQUASH = new CustomStyleProperty<float>("--aim-squash");

		/// <summary>4분원을 3차 베지어로 그릴 때의 손잡이 길이 비율</summary>
		private const float KAPPA = 0.5522847f;

		private Color dimColor;
		private Color ringColor;
		private Color ringReadyColor;
		private float ringWidth;
		private float radius;
		private float squash;
		private Vector2 center;
		private bool ready;

		public AimHoleElement()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
		}

		/// <summary>타원 중심 (이 요소의 좌표). 커서 아래</summary>
		public void SetCenter(Vector2 local)
		{
			center = local;
			MarkDirtyRepaint();
		}

		/// <summary>지금 놓으면 나가나. 테두리 색이 답한다</summary>
		public void SetReady(bool value)
		{
			if (ready == value)
			{
				return;
			}

			ready = value;
			MarkDirtyRepaint();
		}

		/// <summary>화면 반지름 (px). 적 짚기 반지름과 같게 두면 타원 안이 곧 맞는 범위</summary>
		public float Radius => radius;

		private void OnStyleResolved(CustomStyleResolvedEvent moment)
		{
			ICustomStyle style = moment.customStyle;
			style.TryGetValue(DIM_COLOR, out dimColor);
			style.TryGetValue(RING_COLOR, out ringColor);
			style.TryGetValue(RING_READY_COLOR, out ringReadyColor);
			style.TryGetValue(RING_WIDTH, out ringWidth);
			style.TryGetValue(RADIUS, out radius);
			style.TryGetValue(SQUASH, out squash);
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;
			Rect box = contentRect;
			float radiusY = radius * squash;

			// 덮개: 화면 사각에서 타원을 뺀다
			painter.fillColor = dimColor;
			painter.BeginPath();
			painter.MoveTo(new Vector2(box.xMin, box.yMin));
			painter.LineTo(new Vector2(box.xMax, box.yMin));
			painter.LineTo(new Vector2(box.xMax, box.yMax));
			painter.LineTo(new Vector2(box.xMin, box.yMax));
			painter.ClosePath();
			Ellipse(painter, center, radius, radiusY);
			painter.Fill(FillRule.OddEven);

			// 테두리 한 줄
			painter.strokeColor = ready ? ringReadyColor : ringColor;
			painter.lineWidth = ringWidth;
			painter.BeginPath();
			Ellipse(painter, center, radius, radiusY);
			painter.Stroke();
		}

		private static void Ellipse(Painter2D painter, Vector2 at, float rx, float ry)
		{
			float hx = rx * KAPPA;
			float hy = ry * KAPPA;
			painter.MoveTo(new Vector2(at.x + rx, at.y));
			painter.BezierCurveTo(new Vector2(at.x + rx, at.y + hy), new Vector2(at.x + hx, at.y + ry), new Vector2(at.x, at.y + ry));
			painter.BezierCurveTo(new Vector2(at.x - hx, at.y + ry), new Vector2(at.x - rx, at.y + hy), new Vector2(at.x - rx, at.y));
			painter.BezierCurveTo(new Vector2(at.x - rx, at.y - hy), new Vector2(at.x - hx, at.y - ry), new Vector2(at.x, at.y - ry));
			painter.BezierCurveTo(new Vector2(at.x + hx, at.y - ry), new Vector2(at.x + rx, at.y - hy), new Vector2(at.x + rx, at.y));
			painter.ClosePath();
		}
	}
}
