using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct BuildingInstanceData
	{
		public int BuildingID;
		public BuildingState State;
		public int Level;
		public string RuntimeData;

		public BuildingInstanceData(int buildingID, BuildingState state = BuildingState.Placed, int level = 1, string runtimeData = "")
		{
			BuildingID = buildingID;
			State = state;
			Level = level;
			RuntimeData = runtimeData;
		}
	}
}
