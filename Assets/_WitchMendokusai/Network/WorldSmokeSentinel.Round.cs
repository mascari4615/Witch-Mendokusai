using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	// WorldSmokeSentinel 의 한 판 돌기 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 WorldSmokeSentinel.cs 를 본다.
	public sealed partial class WorldSmokeSentinel : MonoBehaviour
	{
		/// <summary>루프 한 바퀴 — 가장 가까운 것으로 걸어가 줍고, 그걸 솥에 넣고, 완성을 가져간다.</summary>
		private void PlayOneRound(IWorldLink link)
		{
			stepCooldown -= Time.unscaledDeltaTime;
			if (stepCooldown > 0f)
				return;

			stepCooldown = STEP_SECONDS;

			// 이름부터 정한다 — 남의 화면에서 「손님 3」으로 남으면 누가 누군지 알 수 없다.
			if (named == false)
			{
				myName = "파수꾼" + (link.MyDollId % 100);
				WorldNameBridge.Channel.Rename(myName);
				named = true;
				return;
			}

			if (gathered == false)
			{
				WalkAndGather(link);
				return;
			}

			// 주운 것으로 먼저 상자 왕복을 해 본다 — 넣고, 그대로 다시 꺼낸다.
			// ★ 두 판이 같은 자리에 지으면 한쪽은 영영 상자가 없다 — 각자 자기가 선 자리에 짓는다.
			if (chestPlaced == false)
			{
				WhereIStand(link, out float standX, out float standZ);
				chestX = Mathf.RoundToInt(standX);
				chestZ = Mathf.RoundToInt(standZ);
				DomainSDK.Building.SharedBuildChannelBridge.Channel.PlaceBuilding(chestX, 0, chestZ, CHEST_BUILDING_ID);
				chestPlaced = true;
				return;
			}

			// ★ 상자는 <b>게임 화면이 쓰는 길</b>로 쓴다 (TASK-WM-217): 게임의 상자 손잡이는
			//   브릿지(WorldChestBridge)를 거친다. 파수꾼만 줄을 직접 부르면, 그 손잡이가
			//   실제로 도는지는 <b>아무도 재지 않은 채</b> 초록만 남는다 — 게임 창에 상자가
			//   아예 없던 시절에도 관문은 초록이었다(그 길을 안 지나갔으니까).
			if (chestOpened == false)
			{
				if (WorldChestBridge.IsActive == false || WorldChestBridge.Channel.TryOpenNearby() == false)
					return; // 아직 그 자리에 상자가 안 섰다 — 다음 걸음에 다시 연다.

				chestOpened = true;
				return;
			}

			if (chestFilled == false)
			{
				WorldChestBridge.Channel.Put(gatheredItemId, 1);
				chestFilled = true;
				return;
			}

			if (chestSeenAmount == 0)
			{
				// 상자가 정말 받았나 — 받았으면 도로 꺼내 가방으로 되돌린다.
				System.Collections.Generic.IReadOnlyList<ChestSlot> inside = WorldChestBridge.Channel.Contents;
				if (inside.Count > 0)
				{
					chestSeenAmount = inside[0].Amount;
					WorldChestBridge.Channel.Take(inside[0].ItemId, chestSeenAmount);
					return;
				}

				WorldChestBridge.Channel.TryOpenNearby();
				return;
			}

			// 솥도 짓는다 — 세계에 솥이 없으면 아무도 조리할 수 없다(전역 솥은 폐기됐다).
			if (potPlaced == false)
			{
				DomainSDK.Building.SharedBuildChannelBridge.Channel.PlaceBuilding(chestX + 1, 0, chestZ, WorldSim.CAULDRON_BUILDING_ID);
				potPlaced = true;
				return;
			}

			if (brewed == false)
			{
				// ⚠ 솥이 <b>서기도 전에</b> 젓겠다고 한 번 쏘고 끝냈다 (2026-08-20). 짓기는 줄을 타고 가는데
				//   바로 다음 걸음(0.1초)에 저었으니, 그 말은 받을 솥이 없어 버려진다. 그리고 다시 안 쏜다.
				//   서 있는 것을 <b>보고</b> 젓는다 — 안 보이면 다음 걸음에 또 본다.
				CauldronView[] standing = link?.Cauldrons;
				if (standing == null || standing.Length == 0)
					return;

				DomainSDK.Alchemy.SharedBrewChannelBridge.Channel.TryUseNearbyCauldron(gatheredItemId);
				brewed = true;
				return;
			}

			// 솥이 실제로 섰나 · 저은 자국이 남았나 — 완성이 0 일 때 <b>어디서 끊겼는지</b>를 남긴다
			// (실측 2026-08-10: potion=0 인데 이유가 「남이 가져갔다」로 나와 혼자 판에서도 오해를 샀다).
			CauldronView[] pots = link.Cauldrons;
			potsSeen = pots == null ? 0 : pots.Length;
			if (pots != null)
			{
				for (int i = 0; i < pots.Length; i++)
				{
					if (pots[i].x != chestX + 1 || pots[i].z != chestZ)
						continue;

					myPotSteps = pots[i].steps;
					break;
				}
			}

			// ★ <b>여러 칸 건물</b>도 한 채 지어 본다 (TASK-WM-217): 「2×2 가 한 칸으로 접히는가」는
			//   판정 층 시험과 따라그리기 시험이 덮지만, <b>진짜 판에서 세계가 몇 칸으로 아는지</b>는
			//   아무도 안 쟀다. 세계가 돌려주는 크기를 그대로 결과지에 적는다.
			if (bigPlaced == false)
			{
				DomainSDK.Building.SharedBuildChannelBridge.Channel.PlaceBuilding(chestX, 0, chestZ + 3, BIG_BUILDING_ID);
				bigPlaced = true;
				return;
			}

			if (bigWidth == 0)
			{
				BuildingView[] standing = link.Buildings;
				for (int i = 0; standing != null && i < standing.Length; i++)
				{
					if (standing[i] == null || standing[i].buildingId != BIG_BUILDING_ID)
						continue;

					bigWidth = standing[i].w;
					bigLength = standing[i].l;
					break;
				}

				return; // 아직 세계가 안 세웠다 — 다음 걸음에 다시 본다.
			}

			// ★ <b>부수기</b>도 진짜 판으로 잰다 (TASK-WM-217 ④): 짓기만 재고 부수기를 안 재면,
			//   「지은 것을 무를 수 없는 세계」가 초록으로 지나간다. 부순 자리는 <b>비어야</b> 하고
			//   (안 그러면 그 칸에 영영 못 짓는다) 재료도 절반 돌아와야 한다.
			if (bigRemoved == false)
			{
				DomainSDK.Building.SharedBuildChannelBridge.Channel.RemoveBuilding(chestX, 0, chestZ + 3);
				bigRemoved = true;
				return;
			}

			if (bigGone == false)
			{
				bool stillThere = false;
				BuildingView[] standing = link.Buildings;
				for (int i = 0; standing != null && i < standing.Length; i++)
				{
					if (standing[i] != null && standing[i].buildingId == BIG_BUILDING_ID)
						stillThere = true;
				}

				if (stillThere)
					return; // 세계가 아직 안 지웠다 — 기다린다(거절이면 마감이 잡는다).

				bigGone = true;
				return;
			}

			// ★ 제작은 <b>솥에 넣은 뒤에</b> 청한다 (실측 2026-08-10): 조리보다 먼저 청했더니
			//   제작이 조리에 쓸 나무를 먼저 먹어 <b>솥에 넣을 것이 없었다</b> — 저은 자국 0,
			//   완성 0. 관문은 「어디서 끊겼는지」를 potsteps 로 말해 줬고, 그게 이 순서를 정했다.
			// ★ 제작도 <b>게임이 쓰는 길</b>로 청한다 (TASK-WM-217): 게임의 제작 화면은 브릿지
			//   (WorldCraftBridge)를 거친다. 파수꾼만 줄을 직접 부르면 그 길이 실제로 도는지는
			//   아무도 안 잰 채 초록만 남는다 — 상자에서 이미 그 자리를 밟았다.
			if (askedCraft == false)
			{
				if (WorldCraftBridge.IsActive == false)
					return; // 아직 줄이 안 꽂혔다 — 다음 걸음에 다시 본다.

				System.Collections.Generic.IReadOnlyList<CraftRecipeEntry> book = WorldCraftBridge.Channel.Recipes;
				if (book != null && book.Count > 0)
					WorldCraftBridge.Channel.Request(book[0].id);

				askedCraft = true;
				return;
			}

			// ⚠ 「만들었나」를 번호로 판단하지 않는다: 게임의 <b>나무가 0번</b>이라, 나무를 만든 판이
			//   「아무것도 안 만들었다」로 읽힌다(줍기에서 이미 한 번 밟은 함정이다).
			if (crafted == false)
			{
				if (WorldCraftBridge.Channel.TryTakeResult(out CraftResult made))
				{
					// 왜 못 만들었는지도 적는다 — 「0」만 남으면 재료가 없던 건지 주사위를 진 건지 모른다.
					craftWhy = made.Succeeded ? "made" : (made.Attempted ? "lost the dice" : made.Denied);
					if (made.Succeeded)
					{
						crafted = true;
						craftedItemId = made.ResultItemId;
					}
				}
			}

			// 완성은 세계가 내준다 — 못 받으면 다음 걸음에 다시 청한다(남이 먼저 가져갔을 수도 있다).
			DomainSDK.Alchemy.SharedBrewChannelBridge.Channel.RequestCompletion();

			WorldBrewView taken = link.TakeCompletedBrew();
			// 완성했나 — 여기도 번호가 아니라 「받았나」로 본다(0번 물건도 진짜 결과다).
			if (taken != null && taken.recipe != null && taken.grade > 0)
			{
				completedItemId = taken.itemId;
				completed = true;
			}
		}

		/// <summary>지난 판이 상자에 넣어 둔 것이 아직 있나 — 자리는 환경변수로 받는다.</summary>
		private void CheckKept(IWorldLink link)
		{
			stepCooldown -= Time.unscaledDeltaTime;
			if (stepCooldown > 0f)
				return;

			stepCooldown = STEP_SECONDS;

			int cellX = ReadNumber("WM_WORLD_SMOKE_CHEST_X");
			int cellZ = ReadNumber("WM_WORLD_SMOKE_CHEST_Z");

			if (link.Chest != null && link.Chest.items != null && link.Chest.items.Length > 0)
			{
				chestSeenAmount = link.Chest.items[0].amount;
				gatheredItemId = link.Chest.items[0].itemId;
				return;
			}

			link.RequestChest(cellX, 0, cellZ);
		}

		/// <summary>세계가 그 번호의 건물을 몇 채 알고 있나 — 「청했다」와 「섰다」를 가르는 자리.</summary>
		private static int CountBuildings(IWorldLink link, int buildingId)
		{
			BuildingView[] all = link?.Buildings;
			if (all == null)
				return 0;

			int found = 0;
			for (int i = 0; i < all.Length; i++)
			{
				if (all[i] != null && all[i].buildingId == buildingId)
					found += 1;
			}

			return found;
		}

		/// <summary>내가 지금 선 자리 — 없으면 원점.</summary>
		private static void WhereIStand(IWorldLink link, out float x, out float z)
		{
			x = 0f;
			z = 0f;
			WorldDollView[] dolls = link.Dolls;
			for (int i = 0; i < dolls.Length; i++)
			{
				if (dolls[i].id != link.MyDollId)
					continue;

				x = dolls[i].x;
				z = dolls[i].z;
				return;
			}
		}

		private void WalkAndGather(IWorldLink link)
		{
			GatherableView[] alive = link.Gatherables;
			if (alive == null || alive.Length == 0)
				return;

			// 내가 선 자리 — 걸음은 「이쪽으로」라 지금 자리에서 뺀 방향을 보내야 한다.
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

			// ★ 아무거나 줍지 않는다 (TASK-WM-217): 짓기에 <b>나무</b>가 드는데 철광석만 주우면
			//   상자를 못 지어 관문이 그 자리에서 죽는다. 아직 재료가 모자라면 나무부터 찾는다.
			bool needWood = gatheredWood < WOOD_NEEDED;

			GatherableView nearest = null;
			float best = float.MaxValue;
			for (int i = 0; i < alive.Length; i++)
			{
				if (needWood && alive[i].itemId != WorldSeeds.WOOD)
					continue;

				float dx = alive[i].x - meX;
				float dz = alive[i].z - meZ;
				float distance = dx * dx + dz * dz;
				if (distance >= best)
					continue;

				best = distance;
				nearest = alive[i];
			}

			if (nearest == null)
				return;

			if (best <= 2.0f * 2.0f)
			{
				link.RequestGather(nearest.id);
				gatheredItemId = nearest.itemId;
				gatheredAmount = nearest.amount;

				if (nearest.itemId == WorldSeeds.WOOD)
					gatheredWood += nearest.amount;

				// 지을 재료(나무)가 찼을 때 비로소 「주웠다」 — 그전엔 계속 나무를 찾는다.
				gathered = gatheredWood >= WOOD_NEEDED;
				return;
			}

			link.RequestMove(nearest.x - meX, nearest.z - meZ);
		}

		/// <summary>세계가 지금 나를 뭐라고 부르나 — 정한 이름이 그림에 실렸는지 본다.</summary>
		private static string NameInWorld(IWorldLink link)
		{
			if (link?.Dolls == null)
				return string.Empty;

			for (int i = 0; i < link.Dolls.Length; i++)
			{
				if (link.Dolls[i].id == link.MyDollId)
					return link.Dolls[i].name ?? string.Empty;
			}

			return string.Empty;
		}
	}
}
