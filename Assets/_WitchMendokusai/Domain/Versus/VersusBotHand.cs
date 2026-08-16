namespace WitchMendokusai
{
	/// <summary> 봇 손 — 머리는 <see cref="VersusBotPolicy"/>(순수 코어)에 있고 여기는 꽂는 자리만 맡는다. </summary>
	public sealed class VersusBotHand : IVersusInput
	{
		private readonly VersusBotPolicy policy;

		public VersusBotHand(VersusBotTuning tuning, float halfWidth, float halfDepth, int seed)
		{
			VersusRandom random = new VersusRandom(seed);
			policy = new VersusBotPolicy(tuning, halfWidth, halfDepth, random.NextInt(2) == 0 ? 1f : -1f, 0f);
		}

		// 화면 앞의 사람과 겨루는 상대라 조준을 조금 흔들어 준다 — 완벽 조준은 상대할 맛이 없다.
		public VersusInputFrame Read(VersusRoundState state, int selfIndex, float deltaTime)
		{
			return policy.Decide(state, selfIndex, deltaTime, 0.09f);
		}
	}
}
