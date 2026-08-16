using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// 알갱이가 한 곳에서 다른 곳으로 <b>흘러간다</b>.
	///
	/// ★ 왜 있나 — 방치형에서 가장 안 보이는 것이 <b>흐름</b>이다. 숫자만 오르면
	///   「어디서 와서 어디로 가는지」가 안 보이고, 그러면 두 층이 물려 있다는 게 안 읽힌다.
	///   기지가 자원을 <b>위로 뱉고</b>, 전투가 장비를 <b>창고로 떨구는</b> 걸 눈으로 보여준다.
	///
	/// ★ 게임을 모른다 — 어디서 어디로, 무슨 색, 몇 각인지만 받는다. 뜻은 부르는 쪽에 있다.
	///   자리는 <b>0~1 비율</b>로 받는다 — 칸 크기가 바뀌어도 흐름이 그대로다(해상도 대응).
	/// </summary>
	public sealed class MoteStreamElement : VisualElement
	{
		/// <summary>한 번에 살아 있을 수 있는 알갱이 수 — 넘치면 가장 오래된 것부터 덮어쓴다.</summary>
		private const int CAPACITY = 48;

		private readonly Vector2[] from = new Vector2[CAPACITY];
		private readonly Vector2[] to = new Vector2[CAPACITY];
		private readonly Color[] tint = new Color[CAPACITY];
		private readonly int[] sides = new int[CAPACITY];
		private readonly float[] life = new float[CAPACITY];
		private readonly float[] speed = new float[CAPACITY];
		private int next;

		public MoteStreamElement()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;

			// 다 쓴 자리로 시작한다 — 0 으로 두면 아무도 안 띄운 알갱이가 저 혼자 흘러간다.
			for (int slot = 0; slot < CAPACITY; slot++)
			{
				life[slot] = 1f;
			}
		}

		/// <summary>알갱이 크기 (칸 짧은 변 대비 비율).</summary>
		public float Size { get; set; } = 0.035f;

		/// <summary>알갱이 하나를 띄운다. 자리는 칸 대비 0~1 비율.</summary>
		public void Send(Vector2 startRatio, Vector2 endRatio, Color color, int sideCount, float secondsToArrive)
		{
			int slot = next;
			next = (next + 1) % CAPACITY;

			from[slot] = startRatio;
			to[slot] = endRatio;
			tint[slot] = color;
			sides[slot] = sideCount < 3 ? 3 : (sideCount > 12 ? 12 : sideCount);
			life[slot] = 0.0001f;
			speed[slot] = secondsToArrive <= 0.05f ? 20f : 1f / secondsToArrive;

			MarkDirtyRepaint();
		}

		public void Advance(float deltaSeconds)
		{
			bool any = false;

			for (int slot = 0; slot < CAPACITY; slot++)
			{
				if (life[slot] >= 1f)
				{
					continue;
				}

				life[slot] += deltaSeconds * speed[slot];
				any = true;
			}

			if (any)
			{
				MarkDirtyRepaint();
			}
		}

		private void Draw(MeshGenerationContext context)
		{
			Rect box = contentRect;
			if (box.width <= 2f || box.height <= 2f)
			{
				return;
			}

			float radius = Mathf.Min(box.width, box.height) * Size;
			Painter2D painter = context.painter2D;

			for (int slot = 0; slot < CAPACITY; slot++)
			{
				float walked = life[slot];
				if (walked <= 0f || walked >= 1f)
				{
					continue;
				}

				// 끝에서 사그라든다 — 툭 사라지면 「없어진 것」이 아니라 「끊긴 것」으로 보인다.
				float alpha = walked < 0.8f ? 1f : (1f - walked) * 5f;

				Vector2 at = new Vector2(
					Mathf.Lerp(from[slot].x, to[slot].x, walked) * box.width,
					Mathf.Lerp(from[slot].y, to[slot].y, walked) * box.height);

				// 곧게 가면 기계 같다 — 가는 길이 살짝 부푼다.
				at.y -= Mathf.Sin(walked * Mathf.PI) * box.height * 0.06f;

				painter.fillColor = new Color(tint[slot].r, tint[slot].g, tint[slot].b, alpha * 0.85f);
				painter.BeginPath();

				for (int corner = 0; corner < sides[slot]; corner++)
				{
					float angle = (corner * 360f / sides[slot] - 90f + walked * 240f) * Mathf.Deg2Rad;
					Vector2 point = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

					if (corner == 0)
					{
						painter.MoveTo(point);
					}
					else
					{
						painter.LineTo(point);
					}
				}

				painter.ClosePath();
				painter.Fill();
			}
		}
	}
}
