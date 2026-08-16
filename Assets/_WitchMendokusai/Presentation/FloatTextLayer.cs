using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.Presentation
{
	/// <summary>
	/// 튀어오르는 글자 — 「방금 이만큼 생겼다」.
	///
	/// ★ 왜 필요한가 (실측 2026-08-16: 「전혀 클리커 같지 않다」):
	///   숫자가 <b>칸 안에서 조용히 바뀌면</b> 사람은 아무 일도 안 일어난 줄 안다.
	///   클리커의 손맛은 「눌렀더니 <b>튀어나왔다</b>」에서 온다 — 같은 값이라도 움직이면 일이 된다.
	///
	/// ★ 만들고 버리지 않는다 — 미리 만들어 두고 돌려 쓴다.
	///   초당 수십 개가 뜨는 화면이라 매번 새로 만들면 그게 곧 끊김이다.
	/// </summary>
	public sealed class FloatTextLayer
	{
		private const int POOL = 24;
		private const float LIFE = 0.9f;

		private readonly List<Label> labels = new List<Label>(POOL);
		private readonly float[] lives = new float[POOL];
		private readonly Vector2[] froms = new Vector2[POOL];
		private int next;

		public FloatTextLayer(VisualElement host)
		{
			for (int one = 0; one < POOL; one++)
			{
				Label label = new Label(string.Empty);
				label.AddToClassList("float-text");
				label.style.display = DisplayStyle.None;
				label.pickingMode = PickingMode.Ignore;
				host.Add(label);

				labels.Add(label);
				lives[one] = 0f;
			}
		}

		/// <summary>한 개 띄운다. <paramref name="at"/> 는 담은 칸 안의 자리(픽셀).</summary>
		public void Pop(string text, Vector2 at, Color color)
		{
			int index = next;
			next = (next + 1) % POOL;

			Label label = labels[index];
			label.text = text;
			label.style.color = color;
			label.style.display = DisplayStyle.Flex;
			label.style.left = at.x;
			label.style.top = at.y;

			froms[index] = at;
			lives[index] = LIFE;
		}

		/// <summary>위로 떠오르며 사라진다.</summary>
		public void Advance(float deltaSeconds)
		{
			for (int index = 0; index < labels.Count; index++)
			{
				if (lives[index] <= 0f)
				{
					continue;
				}

				lives[index] -= deltaSeconds;

				if (lives[index] <= 0f)
				{
					labels[index].style.display = DisplayStyle.None;
					continue;
				}

				float gone = 1f - lives[index] / LIFE;
				labels[index].style.top = froms[index].y - gone * 42f;
				labels[index].style.opacity = 1f - gone * gone;
			}
		}
	}
}
