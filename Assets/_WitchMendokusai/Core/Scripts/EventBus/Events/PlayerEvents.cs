using UnityEngine;

namespace WitchMendokusai
{
	public struct PlayerSpawnedEvent : IStickyEvent
	{
		public Transform Transform;
		public Transform CameraPosition;
		public Transform SpritePosition;
	}

	public struct PlayerDespawnedEvent
	{
	}

	public struct PlayerObjectBoundEvent : IStickyEvent
	{
		public UnitStat UnitStat;
		public Unit UnitData;
		public Transform Transform;
		public UnitObject Object;
	}
}
