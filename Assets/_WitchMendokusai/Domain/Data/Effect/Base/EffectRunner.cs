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
		// TASK-WM-107 Slice 3-2 — 사이클-브레이크 seam (QuestManager.BindDataManager 와 동일 패턴).
		void BindDataManager(DataManager dataManager);
	}

	public class EffectRunner : IEffectRunner
	{
		private readonly SOManager soManager;
		private readonly PlayerProvider playerProvider;
		private readonly ObjectPoolManager objectPoolManager;
		private EffectContext context;

		[Inject]
		public EffectRunner(SOManager soManager, PlayerProvider playerProvider, ObjectPoolManager objectPoolManager)
		{
			this.soManager = soManager;
			this.playerProvider = playerProvider;
			this.objectPoolManager = objectPoolManager;
			context = new EffectContext(soManager, playerProvider, objectPoolManager, null);
			// DI 그래프 밖 leaf(씬·프리팹 흩뿌림 GameEventListener)가 effect 적용을 정적 우회로 도달.
			// root 부트서 1회 등록 (DataManager.Construct eager 가 본 ctor 트리거 → World 씬보다 먼저).
			EffectRunnerBridge.Register(this);
			// TASK-WM-107 Slice 3-4a — card 효과 dispatch 권위를 SO 에 push (static Effect 파사드 폐기).
			// SelectedCardBuffer = 코드 내 유일 effect-dispatch CardBuffer. 순환無(soManager=RegisterInstance).
			soManager.SelectedCardBuffer.BindEffectRunner(this);
		}

		// DataManager↔QuestManager↔IEffectRunner 순환 회피: [Inject] pull 대신 소유자(DataManager.Construct) push.
		// DataManager.Construct 가 IEffectRunner 주입(3-1 후 EffectRunner↛DataManager 라 비순환)받아 호출.
		public void BindDataManager(DataManager dataManager)
		{
			context = new EffectContext(soManager, playerProvider, objectPoolManager, dataManager);
		}

		public void ApplyEffects(List<EffectInfoData> effectInfoData)
		{
			foreach (EffectInfoData data in effectInfoData)
				ApplyEffect(Effect.ResolveEffectInfo(data, context));
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

			effect.Apply(effectInfo, context);
		}
	}
}
