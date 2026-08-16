using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 도형 하나를 그린다 — <b>변의 수가 곧 등급</b> (TASK-WM-406).
	///
	/// ★ 사용자 방향: 세계관을 정하기 전이라 <b>기하학적 모양</b>으로 간다.
	///   그래서 「예쁜 그림」이 아니라 <b>읽히는 규칙</b>을 고른다 —
	///   1등급 삼각형 · 2등급 사각형 · … · 8등급 십각형.
	///   숫자를 안 읽어도 <b>변만 세면</b> 등급이 보인다. 아트가 0 이고 톤과도 맞는다.
	///
	/// ★ 코어를 모른다 — 등급·비율만 받아 그린다. 판정은 저쪽에 있다.
	/// </summary>
	public sealed class IdleShapeElement : VisualElement
	{
		/// <summary>1등급이 삼각형이 되게 — 변 = 등급 + 2.</summary>
		private const int SIDES_AT_TIER_ONE = 3;

		private int tier = 1;
		private float spin;
		private float shake;
		private float fill = 1f;
		private Color body = new Color(0.42f, 0.60f, 0.85f);

		public IdleShapeElement()
		{
			generateVisualContent += Draw;
		}

		/// <summary>등급 — 변의 수가 이걸로 정해진다.</summary>
		public int Tier
		{
			get => tier;
			set
			{
				int clamped = value < 1 ? 1 : value;
				if (clamped == tier)
				{
					return;
				}

				tier = clamped;
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

			int sides = tier + SIDES_AT_TIER_ONE - 1;
			if (sides > 12)
			{
				// 열두 변을 넘으면 사람 눈에는 그냥 원이다 — 거기서 멈춘다.
				sides = 12;
			}

			float radius = Mathf.Min(box.width, box.height) * 0.42f;
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
	/// 처치 순간 흩어지는 파편 (TASK-WM-406).
	///
	/// ★ 왜 있어야 하나 — 지금 화면은 숫자만 바뀐다. 「잡았다」가 눈에 안 보이면
	///   사람은 판이 도는지도 모른다. 자동 전투일수록 <b>일이 일어났다는 신호</b>가 필요하다.
	/// </summary>
	public sealed class IdleBurstElement : VisualElement
	{
		private const int SHARD_COUNT = 7;

		private readonly float[] directions = new float[SHARD_COUNT];
		private float life;
		private int sides = 3;
		private Color tint = Color.white;

		public IdleBurstElement()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;

			for (int shard = 0; shard < SHARD_COUNT; shard++)
			{
				directions[shard] = shard * 360f / SHARD_COUNT;
			}
		}

		public bool IsAlive => life > 0f;

		public void Fire(int tier, Color color)
		{
			sides = Mathf.Clamp(tier + 2, 3, 12);
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
