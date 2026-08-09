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

		public static void CalcRuntimeData()
		{
			// HACK: 좀 더 아름다운 방법이 있을텐데
			// TODO: 어느 시점에 계산, 갱신할지

			// 세는 규칙은 판정 층에 있다 (TASK-WM-215) — 여기선 「어디를 볼지」만 정한다.
			// TODO: 4000(솥) 은 여전히 코드에 박힌 번호다 — 수치 노출 룰 대상, 별 증분에서 SO 로 뺀다.
			int potCount = BuildingCensus.CountById(GetAllBuildingInstanceData(), 4000);

			DataManager.Instance.GameStat[GameStatType.POT_COUNT] = potCount;
		}
	}
}