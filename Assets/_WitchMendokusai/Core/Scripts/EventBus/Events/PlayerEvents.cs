using UnityEngine;

namespace WitchMendokusai
{
	public struct PlayerSpawnedEvent
	{
		public Transform Transform;
		public Transform CameraPosition;
		public Transform SpritePosition;
	}

	public struct PlayerDespawnedEvent
	{
	}
}
