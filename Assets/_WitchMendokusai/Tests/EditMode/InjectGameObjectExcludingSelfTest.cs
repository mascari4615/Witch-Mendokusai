using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VContainer;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-109-B — <see cref="ObjectResolverHierarchyExtensions.InjectGameObjectExcludingSelf"/>
	/// self-cascade 무한 재귀 guard 검증.
	///
	/// 회귀 배경: Player.Construct 가 raw <c>container.InjectGameObject(gameObject)</c> 를
	/// 호출 → Player 자신도 재주입 → Player.Construct 재호출 → 무한 재귀 → StackOverflow →
	/// Unity crash (commit ad920ca2 도입 → bcb59577 revert. TASK-WM-109 이슈 2).
	///
	/// 본 테스트는 *실제 VContainer 컨테이너* 로 그 시나리오를 재현한다:
	///   ① 계층 전체(자식·inactive·손자 포함)가 주입되는지 + self 만 제외되는지
	///   ② caller 의 Construct 안에서 헬퍼를 호출해도(= Player 패턴) 무한 재귀 없이
	///      caller Construct 가 정확히 1회만 실행되는지 (guard 의 본질).
	///
	/// 결정적(ms, RNG/시간/FS/씬/PlayMode 무관) — EditMode 신뢰 루프.
	/// </summary>
	public sealed class InjectGameObjectExcludingSelfTest
	{
		private readonly List<GameObject> spawned = new List<GameObject>();

		[TearDown]
		public void TearDown()
		{
			foreach (GameObject go in spawned)
			{
				if (go != null)
					Object.DestroyImmediate(go);
			}
			spawned.Clear();
		}

		private GameObject NewGameObject(string name, GameObject parent = null, bool active = true)
		{
			GameObject go = new GameObject(name);
			if (parent != null)
				go.transform.SetParent(parent.transform);
			go.SetActive(active);
			spawned.Add(go);
			return go;
		}

		private static IObjectResolver BuildEmptyContainer()
		{
			// 등록 0 — Construct 가 파라미터 없는 메서드라 resolve 불필요.
			// BuildRegistry + 인스턴스화 경로를 정상 통과(Composition Root 와 동일 API).
			return new ContainerBuilder().Build();
		}

		// 파라미터 없는 [Inject] Construct — VContainer 가 주입 시 1회 호출, 카운트 누적.
		public sealed class ConstructCounter : MonoBehaviour
		{
			public int ConstructCount;

			[Inject]
			public void Construct() => ConstructCount++;
		}

		// Player 패턴 재현: 자기 Construct 안에서 자기 계층을 self 제외 cascade.
		public sealed class SelfCascadingRoot : MonoBehaviour
		{
			public int ConstructCount;

			[Inject]
			public void Construct(IObjectResolver container)
			{
				ConstructCount++;
				container.InjectGameObjectExcludingSelf(gameObject, this);
			}
		}

		[Test]
		public void InjectsAllDescendants_ExceptSelf()
		{
			IObjectResolver container = BuildEmptyContainer();

			GameObject root = NewGameObject("Root");
			ConstructCounter rootSelf = root.AddComponent<ConstructCounter>();

			GameObject activeChild = NewGameObject("ActiveChild", root, active: true);
			ConstructCounter activeChildCounter = activeChild.AddComponent<ConstructCounter>();

			GameObject inactiveChild = NewGameObject("InactiveChild", root, active: false);
			ConstructCounter inactiveChildCounter = inactiveChild.AddComponent<ConstructCounter>();

			GameObject grandChild = NewGameObject("GrandChild", activeChild, active: true);
			ConstructCounter grandChildCounter = grandChild.AddComponent<ConstructCounter>();

			container.InjectGameObjectExcludingSelf(root, rootSelf);

			Assert.That(rootSelf.ConstructCount, Is.EqualTo(0),
				"self 컴포넌트는 제외돼야 한다 (caller Construct 재진입 차단)");
			Assert.That(activeChildCounter.ConstructCount, Is.EqualTo(1),
				"active 자식은 1회 주입돼야 한다");
			Assert.That(inactiveChildCounter.ConstructCount, Is.EqualTo(1),
				"inactive 자식도 1회 주입돼야 한다 (VContainer InjectGameObject 와 동일 의미)");
			Assert.That(grandChildCounter.ConstructCount, Is.EqualTo(1),
				"손자(재귀)도 1회 주입돼야 한다");
		}

		[Test]
		public void FromWithinSelfConstruct_DoesNotRecurseInfinitely()
		{
			// TASK-WM-109 이슈 2 시나리오 정확 재현: caller 가 자기 Construct 안에서
			// 자기 계층을 cascade. guard 없으면 여기서 StackOverflow → 프로세스 crash.
			IObjectResolver container = BuildEmptyContainer();

			GameObject root = NewGameObject("Root");
			SelfCascadingRoot selfCascading = root.AddComponent<SelfCascadingRoot>();

			GameObject child = NewGameObject("Child", root, active: true);
			ConstructCounter childCounter = child.AddComponent<ConstructCounter>();

			// Player 가 SceneLifetimeScope 에서 주입되는 경로(container.Inject(player)) 동일.
			container.Inject(selfCascading);

			Assert.That(selfCascading.ConstructCount, Is.EqualTo(1),
				"caller Construct 는 정확히 1회 — self-exclude 로 재진입 차단 (무한 재귀 X)");
			Assert.That(childCounter.ConstructCount, Is.EqualTo(1),
				"자식은 caller Construct 의 cascade 로 1회 주입");
		}
	}
}
