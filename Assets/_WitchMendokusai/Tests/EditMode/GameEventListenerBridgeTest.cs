using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-192 — GameEventListener 의 OnEnable NRE 회귀락.
	///
	/// 회귀 배경: GameEventListener 는 여러 중첩 프리팹에 흩뿌려진 leaf 컴포넌트인데
	/// <c>[Inject] Construct(GameEventManager, IEffectRunner)</c> 로 DI 주입받아 OnEnable 에
	/// <c>gameEventManager</c> 사용 → ① SceneLifetimeScope 의 RegisterComponentInHierarchy 는
	/// 다중 인스턴스 중 첫 1개만 Resolve ② 씬배치-active 의 OnEnable 은 build-callback 주입보다
	/// 먼저 발화 가능 → gameEventManager null → NRE (GameEventListener.cs:29).
	///
	/// 근본 수정: DI 제거 + GameEventBridge/EffectRunnerBridge 우회 (root 부트서 등록 보장).
	///
	/// 본 테스트는 *주입 한 번도 안 받은* GameEventListener 가 OnEnable→raise→OnDisable 전 구간을
	/// NRE 없이 통과하고 bridge 로 정확히 라우팅되는지 검증한다 (= 미주입 인스턴스 = 크래시 시나리오 재현).
	/// 결정적(EditMode, RNG/시간/FS/PlayMode 무관) — stub bridge 로 Application.isPlaying 게이트 우회.
	/// </summary>
	public sealed class GameEventListenerBridgeTest
	{
		private sealed class StubEventBridge : IGameEventBridge
		{
			public Action Captured = delegate { };
			public int RegisterCount;
			public int UnregisterCount;

			public void Raise(GameEventType gameEventType) { }
			public void RegisterCallback(GameEventType gameEventType, Action action) { Captured += action; RegisterCount++; }
			public void UnregisterCallback(GameEventType gameEventType, Action action) { Captured -= action; UnregisterCount++; }
		}

		private sealed class StubEffectRunner : IEffectRunner
		{
			public int AppliedCount;

			public void ApplyEffects(List<EffectInfo> effectInfos) { AppliedCount++; }
			public void ApplyEffects(List<EffectInfoData> effectInfoData) { }
			public void ApplyEffect(EffectInfo effectInfo) { }
			public void BindDataManager(DataManager dataManager) { }
		}

		private static void Invoke(GameEventListener listener, string method)
		{
			MethodInfo methodInfo = typeof(GameEventListener).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(methodInfo, $"{method} 가 존재해야 한다 (lifecycle hook).");
			methodInfo.Invoke(listener, null);
		}

		// [field: SerializeField] auto-property 의 backing field 직접 세팅 — 런타임 AddComponent 는
		// Unity 직렬화 패스가 없어 Response/Effects 가 null. 실제 씬·프리팹 직렬화 상태(비-null)를 모사.
		private static void SetBackingField(GameEventListener listener, string propertyName, object value)
		{
			FieldInfo fieldInfo = typeof(GameEventListener).GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(fieldInfo, $"{propertyName} backing field 가 존재해야 한다.");
			fieldInfo.SetValue(listener, value);
		}

		[Test]
		public void UninjectedListener_FullLifecycle_NoNRE_RoutesViaBridge()
		{
			StubEventBridge eventBridge = new StubEventBridge();
			StubEffectRunner effectRunner = new StubEffectRunner();
			GameEventBridge.Register(eventBridge);
			EffectRunnerBridge.Register(effectRunner);

			// 주입 한 번도 안 받은 인스턴스 (씬배치/런타임 미주입 = 크래시 시나리오).
			// 비활성 GO 에 AddComponent → 자동 OnEnable 미발화 (EditMode 결정성) → 수동 invoke 로 1회 보장.
			GameObject gameObject = new GameObject("listener");
			gameObject.SetActive(false);
			GameEventListener listener = gameObject.AddComponent<GameEventListener>();
			SetBackingField(listener, "Response", new UnityEngine.Events.UnityEvent());
			SetBackingField(listener, "Effects", new List<EffectInfo>());

			try
			{
				// OnEnable — 구버전이면 여기서 NRE (gameEventManager null).
				Invoke(listener, "OnEnable");
				Assert.AreEqual(1, eventBridge.RegisterCount, "OnEnable 은 bridge 로 콜백 등록해야 한다.");

				// raise → OnEventRaised → effect 적용도 bridge 라우팅.
				eventBridge.Captured.Invoke();
				Assert.AreEqual(1, effectRunner.AppliedCount, "OnEventRaised 는 EffectRunnerBridge 로 effect 적용해야 한다.");

				Invoke(listener, "OnDisable");
				Assert.AreEqual(1, eventBridge.UnregisterCount, "OnDisable 은 bridge 로 콜백 해제해야 한다.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(gameObject);
			}
		}

		[Test]
		public void GameEventListener_HasNoInjectDependency()
		{
			// 아키텍처 락 — leaf 가 DI 그래프에 다시 묶이면(주입 의존 부활) 미주입 NRE 재발.
			foreach (MethodInfo methodInfo in typeof(GameEventListener).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				Assert.IsNull(methodInfo.GetCustomAttribute<VContainer.InjectAttribute>(),
					$"GameEventListener.{methodInfo.Name} 에 [Inject] 금지 — 흩뿌림 leaf 는 Bridge 우회 (TASK-WM-192).");

			foreach (FieldInfo fieldInfo in typeof(GameEventListener).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
				Assert.IsNull(fieldInfo.GetCustomAttribute<VContainer.InjectAttribute>(),
					$"GameEventListener.{fieldInfo.Name} 에 [Inject] 금지 (TASK-WM-192).");
		}
	}
}
