using System.Collections.Generic;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 내 안의 세계로 이어진 줄 (TASK-WM-217).
	///
	/// 「혼자 하기」가 이것이다 — 하지만 <b>별도 모드가 아니다.</b> 멀리 있는 세계에 못 붙었을 때
	/// 게임이 자기 안에서 같은 세계(<see cref="WorldSim"/>)를 돌리고 거기 붙는다.
	/// 게임 쪽 코드는 어느 쪽에 붙었는지 묻지 않는다(<see cref="IWorldLink"/>).
	///
	/// 서버 프로세스를 따로 띄우지 않으므로 배포에 실행파일이 늘지 않는다.
	/// </summary>
	public sealed class LocalWorldLink : IWorldLink
	{
		private readonly WorldSim world;
		private readonly Doll me;

		public LocalWorldLink(WorldSim world)
		{
			this.world = world;
			me = world.Join();
		}

		public LocalWorldLink() : this(new WorldSim())
		{
		}

		/// <summary>내 안의 세계 본체 — 저장·불러오기 같은 일이 여기로 온다.</summary>
		public WorldSim World => world;

		public int MyDollId => me.Id;

		public bool IsLinked => true;

		public DollView[] Dolls
		{
			get
			{
				Doll[] snapshot = world.Snapshot();
				DollView[] views = new DollView[snapshot.Length];
				for (int i = 0; i < snapshot.Length; i++)
				{
					views[i] = new DollView
					{
						id = snapshot[i].Id,
						x = snapshot[i].Position.x,
						z = snapshot[i].Position.z,
					};
				}

				return views;
			}
		}

		public BuildingView[] Buildings
		{
			get
			{
				PlacedBuilding[] placed = world.Buildings();
				BuildingView[] views = new BuildingView[placed.Length];
				for (int i = 0; i < placed.Length; i++)
				{
					views[i] = new BuildingView
					{
						x = placed[i].Pivot.x,
						y = placed[i].Pivot.y,
						z = placed[i].Pivot.z,
						w = placed[i].Size.x,
						l = placed[i].Size.y,
						buildingId = placed[i].BuildingId,
					};
				}

				return views;
			}
		}

		public void RequestMove(float x, float z)
		{
			world.TryMove(me.Id, new Numerics.Vector3(x, 0f, z));
		}

		public void RequestPlace(int cellX, int cellY, int cellZ, int width, int length, int buildingId)
		{
			world.TryPlaceBuilding(
				new Numerics.Vector3Int(cellX, cellY, cellZ),
				new Numerics.Vector2Int(width, length),
				buildingId);
		}

		public void RequestGather(int itemId, int amount)
		{
			world.TryGather(me.Id, ItemCatalog.Find(itemId), amount);
		}

		/// <summary>내 가방 — 화면이 읽어 간다.</summary>
		public int BagCount(int itemId) => world.BagCount(me.Id, itemId);
	}

	/// <summary>
	/// 지금 세계가 아는 아이템 목록 — 씨앗 (TASK-WM-217).
	/// ⚠ 진짜 목록은 게임 데이터(ItemData 에셋)에서 뽑아 와야 한다. 그 다리는 후속.
	/// </summary>
	public static class ItemCatalog
	{
		private sealed class SeedItem : IItemData
		{
			public SeedItem(int id, int maxAmount)
			{
				ID = id;
				MaxAmount = maxAmount;
			}

			public int ID { get; }
			public int MaxAmount { get; }
			public ItemType Type => default;
			public ItemGrade Grade => default;
		}

		private static readonly Dictionary<int, SeedItem> byId = new Dictionary<int, SeedItem>
		{
			{ 1, new SeedItem(1, 99) },
			{ 2, new SeedItem(2, 20) },
		};

		public static IItemData Find(int itemId) => byId.TryGetValue(itemId, out SeedItem item) ? item : null;
	}
}
