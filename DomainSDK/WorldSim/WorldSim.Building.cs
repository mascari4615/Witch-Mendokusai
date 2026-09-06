using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// WorldSim.cs 의 Building 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 건축.
	public sealed partial class WorldSim
	{
		/// <summary>
		/// 움직임 요청을 <b>서버가 판정한다.</b> 클라가 보낸 값을 그대로 믿지 않는다 —
		/// 한 번에 갈 수 있는 거리로 잘라낸다(믿으면 순간이동이 공짜가 된다).
		/// </summary>
		/// <summary>
		/// 짓기 요청을 <b>서버가 판정한다</b> — 겹치면 거절.
		/// 겹침 규칙은 게임과 같은 것(<see cref="BuildingFootprint"/>)을 쓴다.
		/// </summary>
		/// <summary>
		/// 짓는다 — <b>크기는 세계가 정한다</b> (TASK-WM-217).
		///
		/// ★ 왜: 전에는 창이 크기를 같이 보냈고 세계는 그대로 믿었다. 그러면 창을 고친 사람이
		///   「이건 1×1 이다」라고 우기며 남의 집에 겹쳐 지을 수 있고, 게임 창과 웹 창이 같은 집을
		///   다른 크기로 그린다. 세계가 모르는 건물은 아예 서지 않는다.
		/// </summary>
		public bool TryPlaceBuilding(Vector3Int pivot, int buildingId, WorldBuildingCatalog catalog)
		{
			if (catalog == null || catalog.TrySize(buildingId, out int width, out int length) == false)
				return false;

			if (TryPlaceBuilding(pivot, new Vector2Int(width, length), buildingId) == false)
				return false;

			// 상자면 그 자리에 빈 상자를 놓는다 — 지은 것이 쓸모를 갖는 자리다.
			Storages.Place(pivot, catalog.SlotsOf(buildingId));

			// 솥이면 그 자리에 빈 솥을 놓는다 — 지은 사람이 자기 솥에서 젓는다.
			if (buildingId == CAULDRON_BUILDING_ID)
				Cauldrons.Place(pivot);

			return true;
		}

		public bool TryPlaceBuilding(Vector3Int pivot, Vector2Int size, int buildingId)
		{
			lock (gate)
			{
				HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(occupiedCells.Keys);
				if (BuildingFootprint.IsBlocked(pivot, size, occupied))
					return false;

				List<Vector3Int> cells = BuildingFootprint.Cells(pivot, size);
				for (int i = 0; i < cells.Count; i++)
					occupiedCells[cells[i]] = buildingId;

				placed.Add(new PlacedBuilding(pivot, size, buildingId));
				BuildVersion++;
				return true;
			}
		}

		/// <summary>
		/// 놓은 것을 <b>부순다</b> — 그 칸을 물고 있는 건물을 통째로 지운다 (TASK-WM-217).
		/// 모서리를 찍든 가운데를 찍든 같은 건물이 지워진다(사람은 「건물」을 부수지 「칸」을 부수지 않는다).
		/// </summary>
		public bool TryRemoveBuilding(Vector3Int cell) => TryRemoveBuilding(cell, out int _);

		/// <summary>
		/// 부순다 — <b>무엇이었는지</b>도 알려 준다 (TASK-WM-217).
		/// 재료를 얼마쯤 돌려주려면 부르는 쪽이 「그게 뭐였나」를 알아야 한다.
		/// </summary>
		public bool TryRemoveBuilding(Vector3Int cell, out int removedBuildingId)
		{
			removedBuildingId = 0;
			lock (gate)
			{
				if (occupiedCells.ContainsKey(cell) == false)
					return false;

				// 상자였으면 상자도 같이 사라진다(안에 든 것도) — 창이 사람에게 먼저 물어야 한다.
				Storages.Remove(cell);
				Cauldrons.Remove(cell);

				for (int i = 0; i < placed.Count; i++)
				{
					List<Vector3Int> cells = BuildingFootprint.Cells(placed[i].Pivot, placed[i].Size);
					if (cells.Contains(cell) == false)
						continue;

					for (int c = 0; c < cells.Count; c++)
						occupiedCells.Remove(cells[c]);

					removedBuildingId = placed[i].BuildingId;
					placed.RemoveAt(i);
					BuildVersion++;
					return true;
				}

				// 칸은 물려 있는데 주인이 없다 = 장부가 어긋난 것. 그냥 두면 그 칸에 영영 못 짓는다.
				occupiedCells.Remove(cell);
				BuildVersion++;
				return true;
			}
		}

		/// <summary>지어지거나 부서질 때마다 오른다 — 창이 「내 화면이 낡았나」를 이 수로 안다.</summary>
		public int BuildVersion { get; private set; }

		/// <summary>세워진 건물들 — 훑는 동안 바뀌어도 안전하게 사본으로.</summary>
		public PlacedBuilding[] Buildings()
		{
			lock (gate)
			{
				return placed.ToArray();
			}
		}

		/// <summary>어느 건물이 몇 개 서 있나 — 세는 규칙도 게임과 같은 것.</summary>
		public int CountBuildings(int buildingId)
		{
			lock (gate)
			{
				List<BuildingInstanceData> instances = new List<BuildingInstanceData>();
				for (int i = 0; i < placed.Count; i++)
					instances.Add(new BuildingInstanceData(placed[i].BuildingId));

				return BuildingCensus.CountById(instances, buildingId);
			}
		}
	}
}


