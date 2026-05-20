using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	/// <summary>
	/// IObjectResolver 계층 주입 헬퍼 — self-cascade 무한 재귀 guard 표준화 (TASK-WM-109-B).
	///
	/// 배경: <c>container.InjectGameObject(go)</c> 는 go 계층의 모든 MonoBehaviour 에 Inject 를
	/// 재귀 적용한다 (VContainer 1.17.0 <c>ObjectResolverUnityExtensions.InjectGameObjectRecursive</c>,
	/// DI/VCONTAINER-MECHANISM.md §5). caller 컴포넌트의 <c>[Inject] Construct</c> 안에서 자기
	/// gameObject 를 그대로 InjectGameObject 하면 → 자기 자신도 다시 Inject → Construct
	/// 재호출 → 무한 재귀 → StackOverflow → Unity crash.
	/// (commit ad920ca2 도입 → bcb59577 revert. TASK-WM-109 이슈 2.
	///  `process.md § 황금의 정신 — 설계 자가 검토` 위반 사례.)
	///
	/// 본 헬퍼 = VContainer <c>InjectGameObject</c> 와 동일한 계층-재귀 의미(자식·inactive 포함)
	/// 이되, 지정한 self 컴포넌트 1개만 제외한다. caller 의 Construct 내부에서 형제·자식
	/// cascade 를 안전하게 수행하는 정본 진입점.
	///
	/// 계약(중요): 한 prefab/계층에서 cascade 를 트리거하는 root 컴포넌트는 *하나* 여야 한다.
	/// self 만 제외하므로, 두 컴포넌트가 서로 본 헬퍼를 자기 Construct 안에서 호출하면 상호
	/// 재귀(A→B→A…)가 다시 성립한다. 자식 컴포넌트는 cascade 를 재트리거하지 말고
	/// <c>[Inject] Construct</c> 로 deps 만 받는다 (Player.prefab = Player 단일 root, 자식
	/// PlayerObject/PlayerRotation/UnitMovement 등은 deps-only).
	///
	/// 사용처: <c>Player.Construct</c> (TASK-WM-108/115). 향후 "자기 Construct 안에서 자기
	/// 계층 cascade" 가 필요한 root 컴포넌트는 raw InjectGameObject 대신 본 헬퍼를 사용한다.
	/// raw <c>InjectGameObject</c> 는 caller 가 계층 *밖* 일 때만 안전
	/// (ObjectPoolManager / SceneLifetimeScope / UIManager — 자기 자신을 주입하지 않음).
	/// </summary>
	public static class ObjectResolverHierarchyExtensions
	{
		/// <summary>
		/// <paramref name="root"/> 계층의 모든 MonoBehaviour(자식·inactive 포함)에 Inject 를
		/// 적용하되, <paramref name="self"/> 컴포넌트 1개는 제외한다.
		///
		/// caller 의 <c>[Inject] Construct</c> 내부에서 <c>this</c> 를 self 로 넘겨 호출하면
		/// 자기 Construct 재진입이 차단되어 무한 재귀가 발생하지 않는다.
		/// </summary>
		/// <param name="resolver">VContainer 컨테이너 (caller 의 Construct 가 주입받은 IObjectResolver).</param>
		/// <param name="root">cascade 대상 계층의 루트 GameObject (보통 caller 의 gameObject).</param>
		/// <param name="self">제외할 caller 컴포넌트 (보통 <c>this</c>).</param>
		public static void InjectGameObjectExcludingSelf(
			this IObjectResolver resolver, GameObject root, MonoBehaviour self)
		{
			// GetComponentsInChildren<MonoBehaviour>(true) = VContainer InjectGameObject 와
			// 동일 집합(루트+모든 자손, inactive 포함). 주입은 컴포넌트 단위 멱등이라 순서 무관.
			foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
			{
				if (component != self)
					resolver.Inject(component);
			}
		}
	}
}
