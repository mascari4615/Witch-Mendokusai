using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 진 쪽에게 내미는 카드 후보를 뽑는다(ROUNDS 의 「패자가 뽑는다」). 서로 다른 카드 N 장 —
	/// 같은 카드가 두 장 뜨면 선택지가 실제로는 하나뿐이라 「고르는 맛」이 사라진다.
	/// </summary>
	public static class VersusCardDraft
	{
		/// <summary>
		/// <paramref name="count"/> 장을 중복 없이 뽑는다. 카드 종류보다 많이 달라고 하면 있는 만큼만 준다.
		/// 같은 씨앗 = 같은 결과(엔진 밖 시험 · 미래 원격 대전 동기화).
		/// </summary>
		public static List<VersusCardKind> Draw(ref VersusRandom random, int count)
		{
			List<VersusCardKind> pool = new List<VersusCardKind>(VersusCards.All);
			List<VersusCardKind> drawn = new List<VersusCardKind>();

			int wanted = count < pool.Count ? count : pool.Count;
			for (int index = 0; index < wanted; index++)
			{
				int picked = random.NextInt(pool.Count);
				drawn.Add(pool[picked]);
				pool.RemoveAt(picked);
			}

			return drawn;
		}
	}
}
