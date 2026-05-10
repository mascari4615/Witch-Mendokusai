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
		public PlayerObject CurrentObject { get; private set; }

		public bool HasPlayer => Current != null;
		public bool HasObject => CurrentObject != null;

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
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
			CurrentObject = player != null ? player.Object : null;
		}

		public void Clear()
		{
			Current = null;
			CurrentObject = null;
		}
	}
}
