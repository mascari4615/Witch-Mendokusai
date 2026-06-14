using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-192 — GameEventListener 의 init-order 안전성 회귀락.
	///
	/// 회귀 배경: 흩뿌림 leaf(씬배치 Debug/Hit 캔버스)의 OnEnable 이 DI Construct(주입)보다
	/// 먼저 발화 → gameEventManager null → RegisterCallback NRE (GameEventListener.cs:29).
	///
	/// 근본 수정: DI 유지(의존성 명시) + 등록을 Construct/OnEnable 중 *늦은* 쪽에서 멱등 실행.
	/// 주입 자체는 SceneLifetimeScope 의 foreach InjectGameObject 가 전 인스턴스 보장.
	///
	/// 본 테스트는 두 init 순서(OnEnable→Construct / Construct→OnEnable)가 모두 NRE 없이
	/// 정확히 1회 등록되는지 + effectRunner 라우팅을 검증한다. GameEventManager.RegisterCallback 은
	/// EditMode(Application.isPlaying=false)서 no-op 이므로 등록 *의도* 는 registered 플래그로 관측.
	/// 결정적(EditMode, RNG/시간/FS/PlayMode 무관).
	/// </summary>
	public sealed class GameEventListenerInitOrderTest
	{
		private sealed class StubEffectRunner : IEffectRunner
		{
			public int AppliedCount;

			public void ApplyEffects(List<EffectInfo> effectInfos) { AppliedCount++; }
			public void ApplyEffects(List<EffectInfoData> effectInfoData) { }
			public void ApplyEffect(EffectInfo effectInfo) { }
			public void BindDataManager(DataManager dataManager) { }
		}

		private readonly List<GameObject> spawned = new List<GameObject>();

		[TearDown]
		public void TearDown()
		{
			foreach (GameObject gameObject in spawned)
				if (gameObject != null)
					Object.DestroyImmediate(gameObject);
			spawned.Clear();
		}

		private GameEventManager NewManager()
		{
			GameObject gameObject = new GameObject("mgr");
			gameObject.SetActive(false);
			spawned.Add(gameObject);
			return gameObject.AddComponent<GameEventManager>();
		}

		// 비활성 GO 에 AddComponent → 자동 OnEnable 미발화 (EditMode 결정성, 수동 제어).
		// Response/Effects = 런타임 AddComponent 라 직렬화 미발생 → 실제 직렬화(비-null) 모사.
		private GameEventListener NewListener()
		{
			GameObject gameObject = new GameObject("listener");
			gameObject.SetActive(false);
			spawned.Add(gameObject);
			GameEventListener listener = gameObject.AddComponent<GameEventListener>();
			SetBackingField(listener, "Response", new UnityEngine.Events.UnityEvent());
			SetBackingField(listener, "Effects", new List<EffectInfo>());
			return listener;
		}

		private static void SetBackingField(GameEventListener listener, string propertyName, object value)
		{
			FieldInfo fieldInfo = typeof(GameEventListener).GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(fieldInfo, $"{propertyName} backing field 가 존재해야 한다.");
			fieldInfo.SetValue(listener, value);
		}

		private static void Lifecycle(GameEventListener listener, string method)
		{
			MethodInfo methodInfo = typeof(GameEventListener).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(methodInfo, $"{method} 가 존재해야 한다 (lifecycle hook).");
			methodInfo.Invoke(listener, null);
		}

		private static bool Registered(GameEventListener listener)
		{
			FieldInfo fieldInfo = typeof(GameEventListener).GetField("registered", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.IsNotNull(fieldInfo, "registered 플래그 가 존재해야 한다.");
			return (bool)fieldInfo.GetValue(listener);
		}

		// 씬배치-active: OnEnable 이 Construct(주입) 보다 먼저 — 구버전 NRE 지점.
		[Test]
		public void OnEnableBeforeConstruct_NoNRE_RegistersAtConstruct()
		{
			StubEffectRunner effectRunner = new StubEffectRunner();
			GameEventManager manager = NewManager();
			GameEventListener listener = NewListener();
			listener.gameObject.SetActive(true);

			Lifecycle(listener, "OnEnable"); // 미주입 → 등록 보류, NRE 없어야 (구버전은 여기서 NRE)
			Assert.IsFalse(Registered(listener), "주입 전 OnEnable 은 등록 보류.");

			listener.Construct(manager, effectRunner); // 늦은 주입 → isActiveAndEnabled true → 등록
			Assert.IsTrue(Registered(listener), "Construct 가 늦은 경로에서 등록.");
		}

		// 런타임 inactive→inject→active: Construct 가 OnEnable 보다 먼저 + 멱등.
		[Test]
		public void ConstructBeforeOnEnable_RegistersOnEnable_Idempotent()
		{
			StubEffectRunner effectRunner = new StubEffectRunner();
			GameEventManager manager = NewManager();
			GameEventListener listener = NewListener();

			listener.Construct(manager, effectRunner); // inactive → 등록 보류
			Assert.IsFalse(Registered(listener), "inactive 일 때 Construct 는 등록 보류.");

			listener.gameObject.SetActive(true);
			Lifecycle(listener, "OnEnable");
			Assert.IsTrue(Registered(listener), "활성화 OnEnable 이 등록.");

			Lifecycle(listener, "OnEnable"); // 멱등 — 중복 등록 X
			Assert.IsTrue(Registered(listener));

			Lifecycle(listener, "OnDisable");
			Assert.IsFalse(Registered(listener), "OnDisable 이 등록 해제.");
		}

		[Test]
		public void OnEventRaised_RoutesToInjectedEffectRunner()
		{
			StubEffectRunner effectRunner = new StubEffectRunner();
			GameEventManager manager = NewManager();
			GameEventListener listener = NewListener();
			listener.Construct(manager, effectRunner);

			listener.OnEventRaised();
			Assert.AreEqual(1, effectRunner.AppliedCount, "OnEventRaised 가 주입된 effectRunner 로 effect 적용.");
		}
	}
}
