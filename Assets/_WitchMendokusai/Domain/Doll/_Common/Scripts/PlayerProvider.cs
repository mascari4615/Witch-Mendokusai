using UnityEngine;

namespace WitchMendokusai
{
	public class PlayerProvider : Singleton<PlayerProvider>
	{
		public Player Current { get; private set; }
		public PlayerObject CurrentObject { get; private set; }

		public bool HasPlayer => Current != null;
		public bool HasObject => CurrentObject != null;

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
