namespace WitchMendokusai
{
	/// <summary>
	/// 씨앗을 주면 항상 같은 순서를 내는 난수(xorshift32). UnityEngine.Random 을 쓰면 ① 엔진 밖 시험이 못 돌고
	/// ② 나중에 원격 대전을 붙일 때 두 대의 뽑기가 갈린다. 그래서 처음부터 판정 안에 둔다.
	/// </summary>
	public struct VersusRandom
	{
		private uint state;

		public VersusRandom(int seed)
		{
			// 0 은 xorshift 의 고정점(영원히 0) — 씨앗 0 을 그냥 받으면 뽑기가 멈춘다.
			state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
		}

		/// <summary> 다음 난수(0 제외). </summary>
		public uint NextUInt()
		{
			state ^= state << 13;
			state ^= state >> 17;
			state ^= state << 5;
			return state;
		}

		/// <summary> [0, exclusiveMax) 정수. exclusiveMax <= 0 이면 0. </summary>
		public int NextInt(int exclusiveMax)
		{
			if (exclusiveMax <= 0)
				return 0;

			return (int)(NextUInt() % (uint)exclusiveMax);
		}
	}
}
