using UnityEngine;

namespace WitchMendokusai
{
	public class PlayerProvider : Singleton<PlayerProvider>
	{
		public Player Current { get; private set; }

		public bool HasPlayer => Current != null;

		public void SetCurrent(Player player) => Current = player;
		public void Clear() => Current = null;
	}
}
