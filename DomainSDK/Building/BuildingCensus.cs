using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 세워진 건물 세기 (TASK-WM-215).
	///
	/// 「솥이 몇 개냐」 같은 물음은 게임 규칙이다 — 세는 일이 씬 관리자 안에 있으면
	/// 서버가 같은 답을 낼 수 없다. 모으는 일(어느 무대의 격자를 볼지)은 호스트가 하고,
	/// <b>세는 규칙</b>만 여기 둔다.
	/// </summary>
	public static class BuildingCensus
	{
		public static int CountById(IEnumerable<BuildingInstanceData> buildings, int buildingId)
		{
			if (buildings == null)
				return 0;

			int count = 0;
			foreach (BuildingInstanceData building in buildings)
			{
				if (building.BuildingID == buildingId)
					count++;
			}

			return count;
		}

		/// <summary>건물 번호별 개수 — 한 번 훑어 전부 센다(같은 목록을 여러 번 도는 것보다 싸다).</summary>
		public static Dictionary<int, int> CountAll(IEnumerable<BuildingInstanceData> buildings)
		{
			Dictionary<int, int> counts = new Dictionary<int, int>();
			if (buildings == null)
				return counts;

			foreach (BuildingInstanceData building in buildings)
			{
				counts.TryGetValue(building.BuildingID, out int current);
				counts[building.BuildingID] = current + 1;
			}

			return counts;
		}
	}
}
