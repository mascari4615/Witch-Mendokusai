using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 집. 넓히기, 가구 갖기와 놓기, 아늑함, 쉬기. 옷장도 여기
	public sealed partial class HomesteadSim
	{
		public const string REWARD_ONLY = "reward";
		private const string FIRST_PLACE = "place:";
		public const string WORKSHOP = "workshop";
		public const string TAILOR = "tailor";

		// ---- 넓히기

		public HouseTierDef HouseTier => Catalog.HouseTiers[State.HouseTier - 1];

		public bool TryGetNextHouseTier(out HouseTierDef next)
		{
			next = State.HouseTier < Catalog.HouseTiers.Count ? Catalog.HouseTiers[State.HouseTier] : null;
			return next != null;
		}

		public HomesteadResult CanExpandHouse()
		{
			if (TryGetNextHouseTier(out HouseTierDef next) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.AtMaxLevel);
			}
			return CanPay(next.Cost);
		}

		public HomesteadResult ExpandHouse()
		{
			HomesteadResult result = CanExpandHouse();
			if (result.Ok == false)
			{
				return result;
			}
			TryGetNextHouseTier(out HouseTierDef next);
			Pay(next.Cost);
			State.HouseTier = next.Tier;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// ---- 가구

		public int OwnedCount(string furnitureId) => State.OwnedFurniture.TryGetValue(furnitureId, out int count) ? count : 0;

		public int PlacedCount(string furnitureId) => State.Placed.FindAll(piece => piece.Id == furnitureId).Count;

		// 안 놓고 남은 수
		public int SpareCount(string furnitureId) => OwnedCount(furnitureId) - PlacedCount(furnitureId);

		public void AddFurniture(string furnitureId, int amount)
		{
			Catalog.Get<FurnitureDef>(furnitureId);
			State.OwnedFurniture[furnitureId] = OwnedCount(furnitureId) + amount;
			NotifyChanged();
		}

		public HomesteadResult CanMakeFurniture(string furnitureId)
		{
			FurnitureDef furniture = Catalog.Get<FurnitureDef>(furnitureId);
			if (furniture.Source == REWARD_ONLY)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, furnitureId);
			}
			if (BuildingLevel(WORKSHOP) < furniture.Level)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, WORKSHOP);
			}
			return CanPay(furniture.Cost);
		}

		public HomesteadResult MakeFurniture(string furnitureId)
		{
			HomesteadResult result = CanMakeFurniture(furnitureId);
			if (result.Ok == false)
			{
				return result;
			}
			Pay(Catalog.Get<FurnitureDef>(furnitureId).Cost);
			AddFurniture(furnitureId, 1);
			return HomesteadResult.Success;
		}

		// ---- 놓기. 격자 없이 자유 좌표, 겹침은 돌린 직사각형끼리

		public HomesteadResult CanPlace(string furnitureId, float x, float z, float yaw, int ignoreUid = 0)
		{
			FurnitureDef furniture = Catalog.Get<FurnitureDef>(furnitureId);
			if (RoomRules.IsInside(furniture, HouseTier, x, z, yaw) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NoRoom);
			}
			foreach (PlacedFurniture piece in State.Placed)
			{
				if (piece.Uid == ignoreUid)
				{
					continue;
				}
				FurnitureDef other = Catalog.Get<FurnitureDef>(piece.Id);
				// 깔개와 바닥과 벽은 서로 다른 층이라 겹쳐도 됨
				if (other.Layer == furniture.Layer && RoomRules.Overlaps(furniture, x, z, yaw, other, piece.X, piece.Z, piece.Yaw))
				{
					return HomesteadResult.Fail(HomesteadFailure.Blocked, piece.Id);
				}
			}
			return HomesteadResult.Success;
		}

		public HomesteadResult Place(string furnitureId, float x, float z, float yaw, out PlacedFurniture placed)
		{
			placed = null;
			if (SpareCount(furnitureId) < 1)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotOwned, furnitureId);
			}
			if (State.Placed.Count >= HouseTier.PieceLimit)
			{
				return HomesteadResult.Fail(HomesteadFailure.NoRoom);
			}
			HomesteadResult result = CanPlace(furnitureId, x, z, yaw);
			if (result.Ok == false)
			{
				return result;
			}
			placed = new PlacedFurniture { Uid = State.NextPieceUid++, Id = furnitureId, X = x, Z = z, Yaw = yaw };
			State.Placed.Add(placed);
			string firstKey = FIRST_PLACE + furnitureId;
			if (State.Seen.Contains(firstKey) == false)
			{
				State.Seen.Add(firstKey);
				AddGuildExp(Tuning.FirstPlaceGuildExp);
			}
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult Move(int uid, float x, float z, float yaw)
		{
			PlacedFurniture piece = State.Placed.Find(candidate => candidate.Uid == uid);
			if (piece == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown);
			}
			HomesteadResult result = CanPlace(piece.Id, x, z, yaw, uid);
			if (result.Ok == false)
			{
				return result;
			}
			piece.X = x;
			piece.Z = z;
			piece.Yaw = yaw;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult PickUp(int uid)
		{
			int removed = State.Placed.RemoveAll(piece => piece.Uid == uid);
			NotifyChanged();
			return removed > 0 ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.Unknown);
		}

		// 되돌리기. 앞 배치로 통째로
		public void RestorePlacement(List<PlacedFurniture> placement)
		{
			State.Placed.Clear();
			State.Placed.AddRange(placement);
			NotifyChanged();
		}

		public void PickUpAll()
		{
			State.Placed.Clear();
			NotifyChanged();
		}

		// ---- 아늑함. 같은 가구를 거듭 놓으면 덜 오르고, 한 결로 여럿이면 덤

		public int Comfort => RoomRules.Comfort(Catalog, State.Placed);

		public int ComfortRank
		{
			get
			{
				int comfort = Comfort;
				int rank = 0;
				for (int i = 1; i < Tuning.ComfortSteps.Count; i++)
				{
					if (comfort >= Tuning.ComfortSteps[i])
					{
						rank = i;
					}
				}
				return rank;
			}
		}

		public bool IsRested => State.RestedUntil > State.Minutes;

		// 아침까지 쉼. 흐를 게임 분을 돌려줌. 시계를 미는 것은 부르는 쪽
		public int MinutesUntilMorning(int hour, int minute)
		{
			int now = hour * 60 + minute;
			int morning = Tuning.MorningHour * 60;
			int left = morning - now;
			return left > 0 ? left : left + Tuning.MinutesPerDay;
		}

		// 하늘 달력의 하루에 한 번. 되풀이해 자며 시간을 공짜로 벌지 못하게
		public bool CanRest(long calendarDay) => State.RestedOn != calendarDay;

		// 시간을 민 뒤 부름. 아늑한 만큼 하루 동안 이득
		public HomesteadResult Rest(long calendarDay, int minutes)
		{
			if (CanRest(calendarDay) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			long dayBefore = Day;
			AdvanceMinutes(minutes);
			if (Day == dayBefore)
			{
				State.ExtraDays++;
			}
			State.RestedOn = calendarDay;
			State.RestedRank = ComfortRank;
			State.RestedUntil = State.Minutes + Tuning.MinutesPerDay;
			RefreshRequests();
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// ---- 옷장. 겉모습만, 성능 없음

		public bool OwnsCostume(string costumeId) => State.OwnedCostumes.Contains(costumeId);

		public void AddCostume(string costumeId)
		{
			Catalog.Get<CostumeDef>(costumeId);
			if (OwnsCostume(costumeId) == false)
			{
				State.OwnedCostumes.Add(costumeId);
			}
		}

		public HomesteadResult CanMakeCostume(string costumeId)
		{
			CostumeDef costume = Catalog.Get<CostumeDef>(costumeId);
			if (OwnsCostume(costumeId))
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			if (costume.Source == REWARD_ONLY)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, costumeId);
			}
			if (BuildingLevel(TAILOR) < costume.Level)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, TAILOR);
			}
			return CanPay(costume.Cost);
		}

		public HomesteadResult MakeCostume(string costumeId)
		{
			HomesteadResult result = CanMakeCostume(costumeId);
			if (result.Ok == false)
			{
				return result;
			}
			Pay(Catalog.Get<CostumeDef>(costumeId).Cost);
			AddCostume(costumeId);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public string WornIn(CostumeSlot slot) => State.Worn.TryGetValue(slot, out string worn) ? worn : string.Empty;

		// 입은 것을 다시 고르면 벗음
		public HomesteadResult Wear(string costumeId)
		{
			CostumeDef costume = Catalog.Get<CostumeDef>(costumeId);
			if (OwnsCostume(costumeId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotOwned, costumeId);
			}
			if (WornIn(costume.Slot) == costumeId)
			{
				State.Worn.Remove(costume.Slot);
			}
			else
			{
				State.Worn[costume.Slot] = costumeId;
			}
			NotifyChanged();
			return HomesteadResult.Success;
		}
	}
}
