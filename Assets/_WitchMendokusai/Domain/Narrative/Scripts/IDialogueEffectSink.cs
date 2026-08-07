using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화가 「무언가를 일으키는」 통로 (TASK-WM-052).
	///
	/// ★ 왜 <see cref="IEffectRunner"/> 를 직접 안 쓰나: 그 인터페이스는 대화가 쓸 일 없는 것들
	///   (`BindDataManager`, `EffectInfoData` 오버로드)까지 들고 있다. 좁은 구멍 하나만 뚫으면
	///   재생 상태기가 그 큰 표면 없이 서고, 시험에서 가짜로 바꿔 끼우기도 한 줄이다
	///   (= 대화 로직 검증이 DI 컨테이너를 안 끌고 온다).
	/// </summary>
	public interface IDialogueEffectSink
	{
		void Apply(IReadOnlyList<EffectInfo> effects);
	}

	/// <summary>
	/// 실제 통로 — 게임의 <see cref="IEffectRunner"/> 로 넘긴다. <see cref="DialogueRunner"/> 가 물린다.
	/// (좁은 구멍의 유일한 실사용 구현. 데드 인터페이스 아님.)
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
