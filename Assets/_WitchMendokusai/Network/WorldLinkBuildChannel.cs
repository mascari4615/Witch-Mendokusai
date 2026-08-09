using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Building;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임의 건설이 <b>세계로 이어진 줄</b>을 타게 하는 어댑터 (TASK-WM-217 단계 4).
	///
	/// 게임(BuildManager)은 이미 「공유 건설 채널」이라는 구멍(<see cref="ISharedBuildChannel"/>)으로
	/// 말하게 되어 있다 — FishNet 이 그 구멍을 채우고 있었을 뿐이다. 같은 구멍을 줄이 채우면
	/// <b>게임 쪽 코드는 한 줄도 안 바뀌고</b> 통로만 갈린다. 그래서 FishNet 을 뺄 수 있다.
	///
	/// 「낡았나」는 <b>지금 서 있는 건물 목록에서 뽑는다</b> — 서버가 판 번호를 따로 안 보내도
	/// 목록이 달라지면 수가 달라진다(먼 세계·내 안의 세계 둘 다 같은 방식).
	/// </summary>
	public sealed class WorldLinkBuildChannel : ISharedBuildChannel
	{
		private readonly IWorldLink link;

		public WorldLinkBuildChannel(IWorldLink link)
		{
			this.link = link;
		}

		public bool IsActive => link != null && link.IsLinked;

		public int Version
		{
			get
			{
				BuildingView[] buildings = link?.Buildings;
				if (buildings == null)
					return 0;

				// 순서·개수·내용이 바뀌면 달라지는 수. 같은 세계를 보는 창들은 같은 수를 얻는다.
				unchecked
				{
					int hash = 17;
					for (int i = 0; i < buildings.Length; i++)
					{
						BuildingView view = buildings[i];
						if (view == null)
							continue;

						hash = hash * 31 + view.x;
						hash = hash * 31 + view.y;
						hash = hash * 31 + view.z;
						hash = hash * 31 + view.w;
						hash = hash * 31 + view.l;
						hash = hash * 31 + view.buildingId;
					}

					return hash;
				}
			}
		}

		public void PlaceBuilding(int cellX, int cellY, int cellZ, int buildingId)
		{
			PlaceBuilding(cellX, cellY, cellZ, 1, 1, buildingId);
		}

		public void PlaceBuilding(int cellX, int cellY, int cellZ, int width, int length, int buildingId)
		{
			// 크기를 그대로 나른다 — 세계의 겹침 판정이 게임 화면과 같은 넓이를 본다.
			// 크기는 안 보낸다 — 세계가 안다 (TASK-WM-217).
			link?.RequestPlace(cellX, cellY, cellZ, buildingId);
		}

		public void RemoveBuilding(int cellX, int cellY, int cellZ)
		{
			link?.RequestRemove(cellX, cellY, cellZ);
		}

		public void ReadPlacements(List<BuildingPlacement> buffer)
		{
			if (buffer == null)
				return;

			buffer.Clear();

			BuildingView[] buildings = link?.Buildings;
			if (buildings == null)
				return;

			for (int i = 0; i < buildings.Length; i++)
			{
				BuildingView view = buildings[i];
				if (view == null)
					continue;

				buffer.Add(new BuildingPlacement
				{
					CellX = view.x,
					CellY = view.y,
					CellZ = view.z,
					BuildingId = view.buildingId,
				});
			}
		}
	}
}
