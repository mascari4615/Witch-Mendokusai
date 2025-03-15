using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "WS_", menuName = "Data/" + nameof(WorldStage))]
	public class WorldStage : Stage, ISavable<WorldStageSaveData>
	{
		[field: NonSerialized] public GridData GridData { get; private set; } = new();

		public void Load(WorldStageSaveData saveData)
		{
			GridData.Load(saveData.BuildingSaveData);
		}

		public WorldStageSaveData Save()
		{
			return new WorldStageSaveData()
			{
				BuildingSaveData = GridData.Save()
			};
		}
	}
}