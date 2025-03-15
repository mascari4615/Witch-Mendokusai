using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public struct WorldStageSaveData
	{
		public List<KeyValuePair<Vector3Int, RuntimeBuildingData>> BuildingSaveData;
	}
}