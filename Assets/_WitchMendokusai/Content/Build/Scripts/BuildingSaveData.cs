using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	[Serializable]
	public struct RuntimeBuildingData
	{
		public BuildingState State;
		public int SOID;

		public RuntimeBuildingData(BuildingState state, int so_id)
		{
			State = state;
			SOID = so_id;
		}
	}
}