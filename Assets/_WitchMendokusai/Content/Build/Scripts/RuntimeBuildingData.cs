using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class RuntimeBuildingData : ISavable<BuildingSaveData>
	{
		public Guid? Guid { get; private set; }
		public RuntimeBuildingState State { get; private set; }

		public BuildingData SO { get; private set; }

		public RuntimeBuildingData(BuildingSaveData data)
		{
			Load(data);
		}

		public RuntimeBuildingData(BuildingData so)
		{
			SO = so;
			Initialize();
		}

		private void Initialize()
		{
			Guid = System.Guid.NewGuid();
			State = RuntimeBuildingState.Building;
		}

		public void Load(BuildingSaveData data)
		{
			Guid = data.Guid;
			State = data.State;
			SO = Get<BuildingData>(data.SO_ID);
		}

		public BuildingSaveData Save()
		{
			return new BuildingSaveData()
			{
				Guid = Guid,
				State = RuntimeBuildingState.Building,
				SO_ID = SO != null ? SO.ID : DataSO.NONE_ID
			};
		}
	}
}