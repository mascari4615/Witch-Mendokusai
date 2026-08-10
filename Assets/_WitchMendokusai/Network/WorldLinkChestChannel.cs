using System;
using System.Collections.Generic;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 화면의 상자 손잡이를 <b>세계에 잇는 줄</b> (TASK-WM-217).
	/// 어느 상자인지는 <b>내가 선 자리</b>에서 고른다 — 창은 좌표를 몰라도 된다.
	/// </summary>
	public sealed class WorldLinkChestChannel : IWorldChestChannel
	{
		/// <summary>이보다 멀면 손이 안 닿는다 — 진짜 판정은 세계가 다시 한다.</summary>
		private const float REACH = 3f;

		private readonly IWorldLink link;
		private readonly List<ChestSlot> contents = new List<ChestSlot>();

		private int openX;
		private int openY;
		private int openZ;
		private bool opened;

		public WorldLinkChestChannel(IWorldLink link)
		{
			this.link = link;
		}

		public bool IsActive => link != null;

		public IReadOnlyList<ChestSlot> Contents
		{
			get
			{
				contents.Clear();

				ChestView chest = link?.Chest;
				if (chest == null || chest.items == null)
					return contents;

				// 지금 열어 둔 자리의 상자가 아니면 안 보여 준다 — 남의 상자 안을 내 화면에 그리면 안 된다.
				if (opened && (chest.x != openX || chest.y != openY || chest.z != openZ))
					return contents;

				for (int i = 0; i < chest.items.Length; i++)
					contents.Add(new ChestSlot { ItemId = chest.items[i].itemId, Amount = chest.items[i].amount });

				return contents;
			}
		}

		public bool TryOpenNearby()
		{
			if (link == null)
				return false;

			// 세계가 아는 건물 중 <b>상자</b>만 본다 — 크기는 세계가 정본이라 pivot 으로 충분하다.
			BuildingView[] buildings = link.Buildings;
			if (buildings == null)
				return false;

			float meX = 0f;
			float meZ = 0f;
			WorldDollView[] dolls = link.Dolls;
			for (int i = 0; i < dolls.Length; i++)
			{
				if (dolls[i].id != link.MyDollId)
					continue;

				meX = dolls[i].x;
				meZ = dolls[i].z;
				break;
			}

			BuildingView nearest = null;
			float best = REACH * REACH;
			for (int i = 0; i < buildings.Length; i++)
			{
				if (buildings[i] == null || buildings[i].buildingId != CHEST_BUILDING_ID)
					continue;

				float dx = buildings[i].x - meX;
				float dz = buildings[i].z - meZ;
				float distance = (dx * dx) + (dz * dz);
				if (distance > best)
					continue;

				best = distance;
				nearest = buildings[i];
			}

			if (nearest == null)
				return false;

			openX = nearest.x;
			openY = nearest.y;
			openZ = nearest.z;
			opened = true;
			link.RequestChest(openX, openY, openZ);
			return true;
		}

		public void Put(int itemId, int amount)
		{
			if (opened == false)
				return;

			link.RequestChestPut(openX, openY, openZ, itemId, Math.Max(1, amount));
		}

		public void Take(int itemId, int amount)
		{
			if (opened == false)
				return;

			link.RequestChestTake(openX, openY, openZ, itemId, Math.Max(1, amount));
		}

		/// <summary>세계가 아는 상자 번호 — 씨앗·자산 양쪽에서 같은 값이다.</summary>
		private const int CHEST_BUILDING_ID = 4005;
	}

	/// <summary>게임 화면의 이름 손잡이를 세계에 잇는 줄 (TASK-WM-218).</summary>
	public sealed class WorldLinkNameChannel : IWorldNameChannel
	{
		private readonly IWorldLink link;

		public WorldLinkNameChannel(IWorldLink link)
		{
			this.link = link;
		}

		public bool IsActive => link != null;

		/// <summary>세계가 지금 나를 뭐라고 부르나 — 그림에 실려 오는 이름이 정본이다.</summary>
		public string MyName
		{
			get
			{
				WorldDollView[] dolls = link?.Dolls;
				if (dolls == null)
					return string.Empty;

				for (int i = 0; i < dolls.Length; i++)
				{
					if (dolls[i].id == link.MyDollId)
						return dolls[i].name ?? string.Empty;
				}

				return string.Empty;
			}
		}

		public void Rename(string name) => link?.RequestRename(name);
	}
}
