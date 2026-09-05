using UnityEngine;
using VContainer.Unity;

namespace WitchMendokusai
{
	/// <summary>
	/// 부팅이 필요로 하는 <b>참조 한 줌</b> — 이름 조회를 대신한다 (TASK-WM-409 단계 B).
	///
	/// ★ 지금까지 조립 뿌리는 <c>Resources.Load&lt;GameObject&gt;("Singletons/RootLifetimeScope")</c> 로
	///   찾았다. 그래서 <c>Core/Resources/Singletons/</c> 전체가 <b>모든 제품 빌드</b>에 실린다
	///   (실측 2026-08-17: 그 폴더의 뿌리 하나가 최대 11.8MB).
	///
	/// ★ 이 SO 는 <b>preloaded asset</b> 으로 등록된다 — 씬과 무관하게 <b>참조로</b> 실리고,
	///   Resources 폴더가 필요 없다.
	///   ⚠ `TASK-WM-121` 이 「preloaded SO → prefab 참조가 player 빌드에 안 실린다」는
	///     유니티 고질을 적어 뒀다. 그래서 이 경로는 <b>먼저 측정</b>한다 —
	///     부팅 로그에 `[BootConfig]` 로 자기 상태를 말한다. 죽어 있으면 그렇다고 찍힌다.
	/// </summary>
	[CreateAssetMenu(fileName = "BootConfig", menuName = "WM/Boot/Boot Config")]
	public sealed class BootConfig : ScriptableObject
	{
		private static BootConfig live;

		[SerializeField] private LifetimeScope rootScopePrefab;

		public LifetimeScope RootScopePrefab => rootScopePrefab;

		/// <summary>preloaded asset 은 로드될 때 <c>OnEnable</c> 이 돈다 — 거기서 자기를 등록한다.</summary>
		private void OnEnable()
		{
			live = this;
		}

		/// <summary>
		/// 부팅이 묻는다. 없으면 null 을 주되, <b>왜 없는지</b>가 로그에 남게 한다 —
		/// 조용한 null 이 WM-121 에서 「조립루트 영구 미빌드」로 이어졌다.
		/// </summary>
		public static BootConfig Live
		{
			get
			{
				if (live == null)
				{
					Debug.LogWarning("[BootConfig] preloaded 로 안 실렸다 — 부팅이 다른 경로를 찾는다 (TASK-WM-409)");
				}
				return live;
			}
		}
	}
}
