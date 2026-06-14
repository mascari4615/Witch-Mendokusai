using System.Collections.Generic;

namespace WitchMendokusai
{
	// GameEventBridge 와 동형 — DI-거주 IEffectRunner 를 DI 그래프 밖 leaf(GameEventListener 등)가
	// 호출하게 하는 정적 우회. EffectRunner ctor 가 root 부트(RootLifetimeScope, World 씬보다 먼저)에
	// Register → null check 제거(FastFail, Bootstrap 후 호출 보장). 정본 = CLAUDE.md § Bridge 패턴.
	public static class EffectRunnerBridge
	{
		private static IEffectRunner instance;

		public static void Register(IEffectRunner runner)
		{
			instance = runner;
		}

		public static void ApplyEffects(List<EffectInfo> effectInfos)
		{
			instance.ApplyEffects(effectInfos);
		}
	}
}
