using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-109-E — cascade inject 분산(17 callsite) 통합 helper.
	/// 시점·owner 별 분산은 SRP 정합 결과로 유지(중앙 집중 X), 공통 boilerplate 만 추출.
	/// 결정 트리는 PR #WM-109-E / `DI/VCONTAINER-MECHANISM.md` § cascade 결정 트리 참고.
	/// </summary>
	public static class DiCascade
	{
		/// <summary>
		/// owner-push 패턴: <c>owner.AddComponent&lt;T&gt;()</c> + <c>container.Inject</c>.
		/// UIRoot.CreateViews / UIManager.Start / UIManager.CreateToolkitPanel 의
		/// boilerplate 통일 (TASK-WM-109-E).
		///
		/// AddComponent 가 새 컴포넌트의 Awake 를 *즉시* 트리거 (Unity 고정). owner GO 가
		/// 활성이면 Awake → OnEnable 이 Inject *전* 발화 가능 → deps null NRE. 이 helper 는
		/// 그 시점을 owner 관할 책임으로 남겨둔다 (caller 가 시점 보장 — Construct/Start 호출).
		/// </summary>
		public static T AddInjected<T>(IObjectResolver container, GameObject owner) where T : Component
		{
			T component = owner.AddComponent<T>();
			container.Inject(component);
			return component;
		}

		/// <summary>
		/// 비활성 Instantiate → 자식 전체 Inject → 활성. TASK-WM-115 R2 검증 패턴 정본.
		/// active prefab 을 그냥 Instantiate 하면 자식 OnEnable 이 Inject *전* 발화 → deps null NRE.
		/// (UIManager.InstantiateInjectedActive 흡수. ObjectPoolManager.CreateObject 와 동일 canonical.)
		/// </summary>
		public static T InstantiateInjected<T>(IObjectResolver container, T prefab, Transform parent, bool activateAfter) where T : Component
		{
			bool prefabWasActive = prefab.gameObject.activeSelf;
			if (prefabWasActive)
				prefab.gameObject.SetActive(false);

			T instance = Object.Instantiate(prefab, parent);

			if (prefabWasActive)
				prefab.gameObject.SetActive(true);

			// InjectGameObject = VContainer 표준 cascade primitive
			// (ObjectResolverUnityExtensions.cs:36-64, GetComponents 자기 + 자식 트랜스폼 재귀).
			container.InjectGameObject(instance.gameObject);

			if (activateAfter)
				instance.gameObject.SetActive(true);

			return instance;
		}

		/// <summary>
		/// self-cascade: root 의 자식 MonoBehaviour 들을 inject. root 자신은 *이미 inject 중*
		/// (예: root.Construct 안에서 호출 — VContainer 가 root 를 inject 하면서 Construct 가
		/// 발화) 라 제외. Player.Construct 의 canonical 패턴 (TASK-WM-078 2026-05-16).
		///
		/// 왜 InjectGameObject(root.gameObject) 가 아니라 foreach 인가:
		/// InjectGameObject 는 root 자체 도 다시 inject 시도 (VContainer 멱등이지만 비효율).
		/// self-cascade 는 *root 의 Construct 진행 중*이라 root 자신 재inject 의미 X.
		/// </summary>
		public static void InjectChildren(IObjectResolver container, MonoBehaviour root)
		{
			foreach (MonoBehaviour childComponent in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (childComponent != root)
					container.Inject(childComponent);
			}
		}
	}
}
