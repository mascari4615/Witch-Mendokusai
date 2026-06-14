using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using VContainer;

namespace WitchMendokusai
{
	// 씬·프리팹 흩뿌림 leaf. deps 는 DI [Inject] (의존성 타입에 명시 — 전역 정적 X).
	// init-order 안전: 씬배치-active 는 OnEnable 이 Construct(주입)보다 먼저 발화 가능 →
	// 등록을 둘 중 *늦은* 쪽에서 멱등 실행 (constructed/registered 플래그). 주입 자체는
	// SceneLifetimeScope 의 foreach InjectGameObject 가 전 인스턴스 보장 (Monster/NPCObject 동형).
	public class GameEventListener : MonoBehaviour
	{
		[field: SerializeField] public GameEventType EventType { get; private set; }

		[field: SerializeField] public UnityEvent Response { get; private set; }
		[field: SerializeField] public List<EffectInfo> Effects { get; private set; }

		private GameEventManager gameEventManager;
		private IEffectRunner effectRunner;
		private bool constructed;
		private bool registered;

		[Inject]
		public void Construct(GameEventManager gameEventManager, IEffectRunner effectRunner)
		{
			this.gameEventManager = gameEventManager;
			this.effectRunner = effectRunner;
			constructed = true;
			// 주입이 OnEnable 보다 늦은 경로(씬배치-active): 이미 활성이면 지금 등록.
			if (isActiveAndEnabled)
				Register();
		}

		private void OnEnable()
		{
			// 주입이 OnEnable 보다 빠른 경로(런타임 inactive→inject→active): 즉시 등록.
			// 아직 미주입이면 Construct 가 isActiveAndEnabled 보고 등록 (no-op return).
			if (constructed)
				Register();
		}

		private void OnDisable()
		{
			if (registered == false)
				return;
			gameEventManager.UnregisterCallback(EventType, OnEventRaised);
			registered = false;
		}

		// constructed 보장된 경로에서만 호출 — null check 불요(FastFail 유지). 멱등.
		private void Register()
		{
			if (registered)
				return;
			gameEventManager.RegisterCallback(EventType, OnEventRaised);
			registered = true;
		}

		public void OnEventRaised()
		{
			Response.Invoke();
			effectRunner.ApplyEffects(Effects);
		}
	}
}
