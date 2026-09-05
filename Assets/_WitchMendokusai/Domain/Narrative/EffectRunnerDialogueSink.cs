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

		public void ApplyData(IReadOnlyList<EffectInfoData> effects)
		{
			// 게임 쪽 목록형 API 가 번호 → 자산 해석까지 해 준다(그 로직을 여기서 베끼지 않는다).
			effectRunner.ApplyEffects(new List<EffectInfoData>(effects));
		}
	}
}
