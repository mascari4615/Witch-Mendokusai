using System.Collections.Generic;
using VContainer;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 2A — POCO Effect dispatch 의 DI-managed 진입점.
	// static Effect.ApplyEffect 의 우회(Bridge) 대체: 주입 deps 를 EffectContext 로 Effect 에 전달.
	// EffectContext 미사용 Effect 는 IEffect 구 경로로 자동 fallback (점진 마이그).
	public interface IEffectRunner
	{
		void ApplyEffects(List<EffectInfo> effectInfos);
		void ApplyEffects(List<EffectInfoData> effectInfoData);
		void ApplyEffect(EffectInfo effectInfo);
	}

	public class EffectRunner : IEffectRunner
	{
		private readonly EffectContext context;

		[Inject]
		public EffectRunner(SOManager soManager)
		{
			context = new EffectContext(soManager);
		}

		public void ApplyEffects(List<EffectInfoData> effectInfoData)
		{
			foreach (EffectInfoData data in effectInfoData)
				ApplyEffect(Effect.ResolveEffectInfo(data));
		}

		public void ApplyEffects(List<EffectInfo> effectInfos)
		{
			foreach (EffectInfo effectInfo in effectInfos)
				ApplyEffect(effectInfo);
		}

		public void ApplyEffect(EffectInfo effectInfo)
		{
			IEffect effect = Effect.CreateEffect(effectInfo.Type);

			if (effect == null)
				return;

			if (effect is IContextualEffect contextualEffect)
				contextualEffect.Apply(effectInfo, context);
			else
				effect.Apply(effectInfo);
		}
	}
}
