using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace WitchMendokusai
{
	// 씬·프리팹 어디든 붙이는 leaf 컴포넌트 (Debug/Hit 캔버스=씬배치, DungeonRuntime=런타임 인스턴스화).
	// 흩뿌림 + OnEnable 시점 의존 → DI 주입을 모든 인스턴스화 경로/타이밍에서 보장 불가
	// (씬배치-active 의 OnEnable 은 SceneLifetimeScope build-callback 주입보다 먼저 발화 가능,
	//  RegisterComponentInHierarchy 는 다중 인스턴스 중 첫 1개만 Resolve). → DI 제거하고 Bridge 우회.
	// GameEventBridge/EffectRunnerBridge = root 부트서 등록 (World 씬보다 먼저) → null 영구 면역.
	public class GameEventListener : MonoBehaviour
	{
		[field: SerializeField] public GameEventType EventType { get; private set; }

		[field: SerializeField] public UnityEvent Response { get; private set; }
		[field: SerializeField] public List<EffectInfo> Effects { get; private set; }

		private void OnEnable()
		{
			GameEventBridge.RegisterCallback(EventType, OnEventRaised);
		}

		private void OnDisable()
		{
			GameEventBridge.UnregisterCallback(EventType, OnEventRaised);
		}

		public void OnEventRaised()
		{
			Response.Invoke();
			EffectRunnerBridge.ApplyEffects(Effects);
		}
	}
}
