using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// BuildingInstanceData를 관리하는 클래스
	// 처음 Init 시 계산할 데이터가 있음
	public static class BuildingUtil
	{
		// 일단 Stage에 BuildingInstanceData를 저장하는 방식으로 구현

		public static List<BuildingInstanceData> GetAllBuildingInstanceData()
		{
			return SOManager.Instance.DataSOs[typeof(WorldStage)].Values
				.Select(dataSO => dataSO as WorldStage)
				.SelectMany(worldStage => worldStage.GridData.BuildingData.Values)
				.ToList();
		}

	}
}