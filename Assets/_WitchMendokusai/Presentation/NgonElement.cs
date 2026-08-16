using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// 정다각형 하나를 그린다 — <b>변의 수</b>로 말한다.
	///
	/// ★ 아트 없이 <b>읽히는 규칙</b>을 만드는 가장 싼 방법이다.
	///   부르는 쪽이 「무엇을 변의 수에 태울지」 정한다(방치형은 장비 등급을 태운다).
	///   숫자를 안 읽어도 <b>변만 세면</b> 안다.
	///
	/// ★ 게임을 모른다 — 변의 수·채움·색만 받는다. 뜻은 부르는 쪽에 있다.
	/// </summary>
	public sealed class NgonElement : VisualElement
	{
		private const int FEWEST_SIDES = 3;

		private int sides = FEWEST_SIDES;
		private float spin;
		private float shake;
		private float fill = 1f;

		/// <summary>등장 중 (0 → 1). 새 대상이 <b>커지며 나타난다</b> — 툭 바뀌면 바뀐 줄 모른다.</summary>
		private float born = 1f;
		private Color body = new Color(0.42f, 0.60f, 0.85f);

		public NgonElement()
		{
			generateVisualContent += Draw;
		}

		/// <summary>변의 수. 셋 미만은 도형이 아니고, 열둘을 넘으면 눈에는 그냥 원이다.</summary>
		public int Sides
		{
			get => sides;
			set
			{
				int clamped = value < FEWEST_SIDES ? FEWEST_SIDES : (value > 12 ? 12 : value);
				if (clamped == sides)
				{
					return;
				}

				sides = clamped;
				born = 0f;
				MarkDirtyRepaint();
			}
		}

		/// <summary>0~1. 남은 몫 — 도형이 그만큼 차 있다.</summary>
		public float Fill
		{
			get => fill;
			set
			{
				float clamped = Mathf.Clamp01(value);
				if (Mathf.Approximately(clamped, fill))
				{
					return;
				}

				fill = clamped;
				MarkDirtyRepaint();
			}
		}

		public Color Body
		{
			get => body;
			set
			{
				body = value;
				MarkDirtyRepaint();
			}
		}

		/// <summary>돈다 — 살아 있다는 신호. 멈춘 화면은 죽은 화면이다.</summary>
		public void Advance(float deltaSeconds, float turnsPerSecond)
		{
			spin += deltaSeconds * turnsPerSecond * 360f;
			if (spin > 360f)
			{
				spin -= 360f;
			}

			if (shake > 0f)
			{
				shake -= deltaSeconds * 4f;
				if (shake < 0f)
				{
					shake = 0f;
				}
			}

			if (born < 1f)
			{
				born += deltaSeconds * 4f;
				if (born > 1f)
				{
					born = 1f;
				}
			}

			MarkDirtyRepaint();
		}

		/// <summary>맞았다 — 잠깐 흔들린다. 「지금 뭔가 일어났다」를 눈으로 알려준다.</summary>
		public void Hit()
		{
			shake = 1f;
		}

		private void Draw(MeshGenerationContext context)
		{
			Rect box = contentRect;
			if (box.width <= 2f || box.height <= 2f)
			{
				return;
			}

			// 등장할 때 살짝 넘쳤다 제자리로 — 딱 맞게 커지면 밋밋하다.
			float pop = born < 1f ? Mathf.Sin(born * Mathf.PI * 0.5f) * (1f + (1f - born) * 0.25f) : 1f;
			float radius = Mathf.Min(box.width, box.height) * 0.42f * pop;
			Vector2 middle = box.center;

			if (shake > 0f)
			{
				float wobble = shake * radius * 0.10f;
				middle += new Vector2(Mathf.Sin(spin * 0.7f) * wobble, Mathf.Cos(spin * 1.1f) * wobble);
			}

			Painter2D painter = context.painter2D;

			// 채운 몸통 — 남은 몫만큼 진하다.
			painter.fillColor = new Color(body.r, body.g, body.b, 0.18f + 0.62f * fill);
			TracePolygon(painter, middle, radius, sides, spin);
			painter.Fill();

			// 테두리 — 변을 세기 쉽게 또렷하게.
			painter.strokeColor = new Color(body.r, body.g, body.b, 0.95f);
			painter.lineWidth = 2f;
			TracePolygon(painter, middle, radius, sides, spin);
			painter.Stroke();
		}

		private static void TracePolygon(Painter2D painter, Vector2 middle, float radius, int sides, float degrees)
		{
			painter.BeginPath();

			for (int corner = 0; corner < sides; corner++)
			{
				// 위쪽 꼭짓점부터 — 삼각형이 「위를 보는」 모양이라야 도형으로 읽힌다.
				float angle = (degrees - 90f + corner * 360f / sides) * Mathf.Deg2Rad;
				Vector2 point = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

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
		}
	}

	/// <summary>
	/// 흩어지는 파편 — <b>무언가 끝났다</b>는 신호.
	///
	/// ★ 숫자만 바뀌면 사람은 일이 일어난 줄 모른다. 자동으로 도는 화면일수록 더 그렇다.
	/// </summary>
	public sealed class NgonBurstElement : VisualElement
	{
		private const int SHARD_COUNT = 7;

		private readonly float[] directions = new float[SHARD_COUNT];
		private float life;
		private int sides = 3;
		private Color tint = Color.white;

		public NgonBurstElement()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;

			for (int shard = 0; shard < SHARD_COUNT; shard++)
			{
				directions[shard] = shard * 360f / SHARD_COUNT;
			}
		}

		public bool IsAlive => life > 0f;

		public void Fire(int sideCount, Color color)
		{
			sides = Mathf.Clamp(sideCount, 3, 12);
			tint = color;
			life = 1f;
			MarkDirtyRepaint();
		}

		public void Advance(float deltaSeconds)
		{
			if (life <= 0f)
			{
				return;
			}

			life -= deltaSeconds * 2.2f;
			if (life < 0f)
			{
				life = 0f;
			}

			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			if (life <= 0f)
			{
				return;
			}

			Rect box = contentRect;
			Vector2 middle = box.center;
			float spread = Mathf.Min(box.width, box.height) * 0.42f * (1f - life);
			float size = Mathf.Min(box.width, box.height) * 0.05f * life;

			Painter2D painter = context.painter2D;
			painter.fillColor = new Color(tint.r, tint.g, tint.b, life * 0.8f);

			for (int shard = 0; shard < SHARD_COUNT; shard++)
			{
				float angle = directions[shard] * Mathf.Deg2Rad;
				Vector2 at = middle + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spread;

				painter.BeginPath();
				for (int corner = 0; corner < sides; corner++)
				{
					float corner_angle = (corner * 360f / sides - 90f) * Mathf.Deg2Rad;
					Vector2 point = at + new Vector2(Mathf.Cos(corner_angle), Mathf.Sin(corner_angle)) * size;

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
