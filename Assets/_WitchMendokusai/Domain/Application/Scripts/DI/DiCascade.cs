using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>
	/// TASK-WM-109-E — cascade inject 분산(17 callsite) 통합 helper.
	/// 시점·owner 별 분산은 SRP 정합 결과로 유지(중앙 집중 X), 공통 boilerplate 만 추출.
	///
	/// 결정 트리 (새 컴포넌트 추가 시):
	/// <list type="bullet">
	/// <item>씬 정적 배치 → <c>SceneLifetimeScope.RegisterInHierarchyIfPresent</c> / foreach <c>InjectGameObject</c></item>
	/// <item>런타임 spawn (pool) → <c>ObjectPoolManager</c> (자동, <c>InjectGameObject</c>)</item>
	/// <item>런타임 spawn (명시) → <c>DiCascade.InstantiateInjected</c></item>
	/// <item>code spawn (AddComponent) → <c>DiCascade.AddInjected</c></item>
	/// <item>자기 자식 cascade (Construct 안) → <c>container.InjectGameObjectExcludingSelf(gameObject, this)</c>
	///   (TASK-WM-109-B / <c>ObjectResolverHierarchyExtensions</c>)</item>
	/// </list>
	/// 동등성 인증: <c>InjectGameObject(go)</c> ≡ <c>foreach GetComponentsInChildren&lt;MonoBehaviour&gt;(true) Inject(mb)</c>
	/// (DI/VCONTAINER-MECHANISM.md §5).
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
	}
}
