using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// WorldSim.cs 의 Save 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 세계 저장과 불러오기.
	public sealed partial class WorldSim
	{
		/// <summary>
		/// 세계의 기억을 뜬다 (TASK-WM-217 단계 5). 뜨는 동안 세계가 바뀌어도 안전하게 자물쇠 안에서.
		/// </summary>
		public WorldSaveData Save()
		{
			lock (gate)
			{
				BuildingSaveData[] saved = new BuildingSaveData[placed.Count];
				for (int i = 0; i < placed.Count; i++)
				{
					saved[i] = new BuildingSaveData
					{
						x = placed[i].Pivot.x,
						y = placed[i].Pivot.y,
						z = placed[i].Pivot.z,
						w = placed[i].Size.x,
						l = placed[i].Size.y,
						buildingId = placed[i].BuildingId,
					};
				}

				return new WorldSaveData
				{
					buildings = saved,
					people = SavePeopleUnlocked(),
					// 시간도 기억한다 — 껐다 켰더니 다시 아침이면 그건 이어진 세계가 아니다.
					year = Calendar.Year,
					season = Calendar.Season,
					day = Calendar.Day,
					hour = Calendar.Hour,
					minute = Calendar.Minute,
					gathered = Gatherables.Save().ToArray(),
					storages = Storages.Save().ToArray(),
					cauldrons = Cauldrons.Save().ToArray(),
				};
			}
		}

		/// <summary>
		/// 기억을 되살린다 (TASK-WM-217 단계 5). <b>지금 있는 건물은 지우고</b> 저장된 것으로 갈아끼운다.
		///
		/// 겹치는 건물은 <b>버린다</b> — 저장 파일이 망가졌거나 규칙이 바뀌었을 때
		/// 겹친 채로 되살리면 그 뒤로 짓기 판정이 영원히 이상해진다. 되살린 개수를 돌려준다.
		/// </summary>
		public int Load(WorldSaveData data) => Load(data, null);

		/// <summary>
		/// 기억을 되살린다. <paramref name="catalog"/> 는 상자 안의 물건을 알아보는 데 쓴다 —
		/// 없으면 상자는 서되 <b>안은 빈다</b>(모르는 물건을 지어내지 않는다).
		/// </summary>
		public int Load(WorldSaveData data, WorldItemCatalog catalog)
		{
			lock (gate)
			{
				placed.Clear();
				occupiedCells.Clear();

				if (data == null)
					return 0;

				Calendar.Set(data.year, data.season, data.day, data.hour, data.minute);
				LoadPeopleUnlocked(data.people);
				Gatherables.Load(data.gathered);

				// 상자는 건물을 되살린 <b>뒤에</b> 채운다 — 어느 자리가 몇 칸인지 건물이 정하기 때문이다.
				StorageSaveEntry[] storagesToRestore = data.storages;

				if (data.buildings == null)
				{
					RestoreStoragesUnlocked(storagesToRestore, catalog);
					return 0;
				}

				int restored = 0;
				for (int i = 0; i < data.buildings.Length; i++)
				{
					BuildingSaveData saved = data.buildings[i];
					if (saved == null)
						continue;

					Vector3Int pivot = new Vector3Int(saved.x, saved.y, saved.z);
					Vector2Int size = new Vector2Int(saved.w, saved.l);

					// ⚠ 여기서 <b>칸 장부를 통째로 베끼면</b> 되살리기가 제곱으로 느려진다 (TASK-WM-353):
					//   집 6만 채는 36초, 30만 채는 <b>5분이 지나도 세계가 안 떴다</b>(실측 2026-08-14).
					//   배포 때마다 그만큼 세계가 닫혀 있고, 어느 크기부터는 <b>영영 못 뜬다</b>.
					//   장부는 이미 칸→건물 사전이다 — 그걸 그대로 물어보면 한 번에 끝난다.
					if (BuildingFootprint.IsBlocked(pivot, size, occupiedCells.Keys))
						continue;

					List<Vector3Int> cells = BuildingFootprint.Cells(pivot, size);
					for (int cell = 0; cell < cells.Count; cell++)
						occupiedCells[cells[cell]] = saved.buildingId;

					placed.Add(new PlacedBuilding(pivot, size, saved.buildingId));
					BuildVersion++;
					restored++;
				}

				// 상자는 건물을 다 세운 뒤에 채운다 — 어느 자리가 몇 칸인지 건물이 정하기 때문이다.
				RestoreStoragesUnlocked(storagesToRestore, catalog);

				// 솥도 같이 되살린다 — 안 하면 지은 솥이 남아 있는데 못 젓는 세계가 된다.
				Cauldrons.Load(data.cauldrons);
				return restored;
			}
		}

		/// <summary>되살린 건물 위에 상자를 얹는다 — 그 자리에 선 건물이 상자가 아니면 버린다.</summary>
		private void RestoreStoragesUnlocked(StorageSaveEntry[] saved, WorldItemCatalog catalog)
		{
			// ⚠ 상자마다 <b>선 집을 처음부터 훑으면</b> 이것도 제곱이다 (TASK-WM-353) —
			//   집이 많은 세계에서 되살리기가 그만큼 또 느려진다. 자리→건물 사전을 한 번 만들어 쓴다.
			Dictionary<Vector3Int, int> whatStandsAt = new Dictionary<Vector3Int, int>(placed.Count);
			for (int i = 0; i < placed.Count; i++)
				whatStandsAt[placed[i].Pivot] = placed[i].BuildingId;

			Storages.Load(saved, cell => whatStandsAt.TryGetValue(cell, out int buildingId)
				? Buildables.SlotsOf(buildingId)
				: 0, catalog);
		}
	}
}


