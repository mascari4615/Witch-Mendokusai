using UnityEngine;

namespace WitchMendokusai
{
	public struct PlayerSpawnedEvent
	{
		public Transform Transform;
		public Transform CameraPosition;
		public Transform HeadAnchor;
	}

	public struct PlayerDespawnedEvent
	{
	}

	public struct PlayerObjectBoundEvent
	{
		public UnitStat UnitStat;
		public Unit UnitData;
		public Transform Transform;
		public UnitObject Object;
	}
}
