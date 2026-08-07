using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화 효과의 실제 통로 — 게임의 <see cref="IEffectRunner"/> 로 넘긴다.
	/// <see cref="DialogueRunner"/> 가 물린다. (좁은 구멍 <see cref="IDialogueEffectSink"/> 의
	/// 유일한 실사용 구현 — 데드 인터페이스 아님.)
	/// </summary>
	public sealed class EffectRunnerDialogueSink : IDialogueEffectSink
	{
		private readonly IEffectRunner effectRunner;

		public EffectRunnerDialogueSink(IEffectRunner effectRunner)
		{
			this.effectRunner = effectRunner;
		}

		public void Apply(IReadOnlyList<EffectInfo> effects)
		{
			for (int i = 0; i < effects.Count; i++)
			{
				effectRunner.ApplyEffect(effects[i]);
			}
		}
	}
}
