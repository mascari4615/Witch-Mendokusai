using UnityEngine;

namespace WitchMendokusai
{
	public class PlayerProvider : MonoBehaviour
	{
		public static PlayerProvider Instance { get; private set; }

		public static bool TryGetExistingInstance(out PlayerProvider mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public Player Current { get; private set; }
		// TASK-WM-115 R3a — live 파생 (stale 스냅샷 X). SetCurrent 시점에 player.Object 가
		// 아직 null(Player.Awake<Construct 순서, 데이터 입증)이어도, 소비 시점엔 valid.
		public PlayerObject CurrentObject => Current != null ? Current.Object : null;

		public bool HasPlayer => Current != null;
		public bool HasObject => CurrentObject != null;

		private void Awake()
		{
			if (Instance != null && Instance != this)
			{
				Destroy(gameObject);
				return;
			}
			Instance = this;
		}

		private void OnDestroy()
		{
			if (Instance == this)
				Instance = null;
		}

		public void SetCurrent(Player player)
		{
			Current = player;
		}

		public void Clear()
		{
			Current = null;
		}
	}
}
