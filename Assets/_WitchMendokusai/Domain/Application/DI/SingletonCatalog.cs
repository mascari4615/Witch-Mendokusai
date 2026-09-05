using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 본편 조립이 세우는 것들의 <b>명시적 목록</b> (TASK-WM-409 단계 A).
	///
	/// ★ 예전에는 <c>Resources.Load&lt;T&gt;($"Singletons/{typeof(T).Name}")</c> —
	///   <b>타입 이름으로</b> 찾았다. 편했지만 대가가 둘이다:
	///   ① 이름이 계약이라 클래스명을 바꾸면 <b>런타임에</b> 깨진다(컴파일러가 못 잡는다).
	///   ② <c>Resources/</c> 안에 있어야 하므로 <b>모든 제품 빌드에</b> 그 그래프째로 실린다 —
	///      실측 2026-08-17: 싱글톤 뿌리 하나가 최대 11.8MB 를 끌고 왔다(StageManager).
	///
	/// ★ 그래서 <b>참조</b>로 바꾼다. 목록을 들고 있는 것은 조립 뿌리(`RootLifetimeScope`)뿐이고,
	///   조립이 안 서는 제품(방치형)에서는 아무것도 안 딸려온다.
	///   ⚠ 단계 A 자체로는 빌드가 안 줄어든다 — 조립 뿌리가 아직 `Resources/` 에 있기 때문이다.
	///     줄어드는 것은 단계 B(뿌리를 본편 씬에 배치)부터다. A 는 그 준비다.
	/// </summary>
	[CreateAssetMenu(fileName = "SingletonCatalog", menuName = "WM/Boot/Singleton Catalog")]
	public sealed class SingletonCatalog : ScriptableObject
	{
		[SerializeField] private GameObject[] prefabs = new GameObject[0];
		[SerializeField] private SOManager soManager;

		public GameObject[] Prefabs => prefabs;
		public SOManager SOManager => soManager;

		/// <summary>
		/// 그 타입의 컴포넌트를 가진 프리팹을 준다. 없으면 <b>그렇다고 말한다</b> —
		/// null 을 조용히 흘리면 조립이 한참 뒤 엉뚱한 곳에서 죽는다(FastFail 룰).
		/// </summary>
		public T Get<T>() where T : MonoBehaviour
		{
			for (int i = 0; i < prefabs.Length; i++)
			{
				if (prefabs[i] == null) { continue; }
				T found = prefabs[i].GetComponent<T>();
				if (found != null) { return found; }
			}
			throw new InvalidOperationException(
				"[SingletonCatalog] " + typeof(T).Name + " 프리팹이 목록에 없다 — "
				+ "카탈로그 배선을 확인할 것 (TASK-WM-409). 목록 크기 " + prefabs.Length);
		}

#if UNITY_EDITOR
		public void EditorFill(GameObject[] found, SOManager manager)
		{
			prefabs = found;
			soManager = manager;
			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
	}
}
