using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>세워진 건물 하나 — 서버가 기억하는 최소 (TASK-WM-216).</summary>
	public sealed class PlacedBuilding
	{
		public PlacedBuilding(Vector3Int pivot, Vector2Int size, int buildingId)
		{
			Pivot = pivot;
			Size = size;
			BuildingId = buildingId;
		}

		public Vector3Int Pivot { get; }
		public Vector2Int Size { get; }
		public int BuildingId { get; }
	}
}


