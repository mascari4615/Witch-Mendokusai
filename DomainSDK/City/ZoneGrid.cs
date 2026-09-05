using System.Collections.Generic;
using System.Linq;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// 존 페인트 격자. GridData(건물) / RoadGraph(도로) 와 형제 — 같은 셀 좌표계(Vector3Int, z=0)
	// 공유, 책임 분리. 한 셀이 zone(R/C/I) 이면서 building 미점유 가능(자동성장 전 상태)이라
	// GridData 와 합치지 X (셀당 의미 혼재 방지, 6 동기 「분리」).
	//
	// CountByType = RciDemandModel 입력원 (현재 R/C/I 셀 수 집계).
	public class ZoneGrid : ISavable<List<KeyValuePair<Vector3Int, ZoneCellData>>>
	{
		public Dictionary<Vector3Int, ZoneCellData> ZoneData { get; private set; } = new();

		public bool HasZone(Vector3Int cell)
		{
			return ZoneData.ContainsKey(cell);
		}

		// 미페인트 셀 = Empty (키 부재로 표현). FastFail 아님 — 빈 셀 질의는 정상 경로(lot 판정).
		public ZoneType GetZone(Vector3Int cell)
		{
			return ZoneData.TryGetValue(cell, out ZoneCellData data) ? data.Type : ZoneType.Empty;
		}

		// 존 페인트 = 멱등 덮어쓰기 (RoadGraph.AddRoad 동형, 페인트 캔버스).
		// Empty 로 페인트 = 존 해제 (빈 셀은 키 부재로 표현 — 메모리 절약 + Empty 카운트 0 유지).
		public void Paint(Vector3Int cell, ZoneType type)
		{
			if (type == ZoneType.Empty)
			{
				ZoneData.Remove(cell);
				return;
			}

			ZoneData[cell] = new ZoneCellData(type);
		}

		public void Clear(Vector3Int cell)
		{
			ZoneData.Remove(cell);
		}

		// RciDemandModel 입력 — 타입별 셀 수. Empty 는 키 부재라 항상 0.
		public int CountByType(ZoneType type)
		{
			int count = 0;
			foreach (ZoneCellData data in ZoneData.Values)
			{
				if (data.Type == type)
				{
					count++;
				}
			}

			return count;
		}

		// ISavable — GridData / RoadGraph 1:1 미러 (덮어쓰기 머지).
		public void Load(List<KeyValuePair<Vector3Int, ZoneCellData>> saveData)
		{
			foreach ((Vector3Int key, ZoneCellData value) in saveData)
			{
				ZoneData[key] = value;
			}
		}

		public List<KeyValuePair<Vector3Int, ZoneCellData>> Save()
		{
			return ZoneData.ToList();
		}
	}
}
