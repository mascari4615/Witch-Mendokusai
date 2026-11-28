using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 마을 축제. 다른 놀이의 결과를 모아 내가 고른 날에 엶
	// - 준비: 칸마다 갈래와 수. 나눠 바쳐도 남음. 선택 칸 (음식) 은 비어도 열림
	// - 그날: 연 날 하루. 주민에게 축하 인사 한 번씩. 날이 바뀌면 끝 (안 해도 잃는 것 없음)
	// - 다시 열기: RepeatDays 뒤. 기한 없음
	public sealed partial class HomesteadSim
	{
		public event Action<string> FestivalOpened = delegate { };

		public FestivalState FestivalOf(string festivalId)
		{
			if (State.Festivals.TryGetValue(festivalId, out FestivalState state) == false)
			{
				state = new FestivalState();
				State.Festivals.Add(festivalId, state);
			}
			return state;
		}

		public int ArrivedCount => Catalog.Residents.FindAll(resident => HasArrived(resident.Id)).Count;

		public int DecorCount => State.Village.FindAll(piece => PlaceableOf(piece).Kind == PlaceableKind.Decor).Count;

		public List<FestivalNeed> FestivalNeeds(FestivalDef festival)
		{
			return new List<FestivalNeed>
			{
				new FestivalNeed(FestivalNeedKind.TownHall, State.TownHall, festival.TownHall),
				new FestivalNeed(FestivalNeedKind.Residents, ArrivedCount, festival.Residents),
				new FestivalNeed(FestivalNeedKind.Decor, DecorCount, festival.Decor),
			};
		}

		public int Given(string festivalId, int slot) => FestivalOf(festivalId).Given.TryGetValue(Catalog.Get<FestivalDef>(festivalId).Slots[slot].Kind, out int amount) ? amount : 0;

		public bool IsSlotFull(string festivalId, int slot) => Given(festivalId, slot) >= Catalog.Get<FestivalDef>(festivalId).Slots[slot].Amount;

		// 바칠 물건. 싼 것부터 써서 귀한 것을 아낌
		private List<GoodsDef> OfferableGoods(GoodsKind kind)
		{
			List<GoodsDef> goods = Catalog.Goods.FindAll(candidate => candidate.Kind == kind && Count(candidate.Id) > 0);
			goods.Sort((left, right) => left.Price != right.Price ? left.Price.CompareTo(right.Price) : string.CompareOrdinal(left.Id, right.Id));
			return goods;
		}

		public HomesteadResult CanOffer(string festivalId, int slot)
		{
			FestivalDef festival = Catalog.Get<FestivalDef>(festivalId);
			if (IsSlotFull(festivalId, slot))
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			return OfferableGoods(festival.Slots[slot].Kind).Count == 0 ? HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods) : HomesteadResult.Success;
		}

		// 가진 만큼 채움. 모자라면 넣은 만큼 남고 다음에 이어 바침
		public HomesteadResult Offer(string festivalId, int slot)
		{
			HomesteadResult result = CanOffer(festivalId, slot);
			if (result.Ok == false)
			{
				return result;
			}
			FestivalSlotDef def = Catalog.Get<FestivalDef>(festivalId).Slots[slot];
			int missing = def.Amount - Given(festivalId, slot);
			foreach (GoodsDef goods in OfferableGoods(def.Kind))
			{
				int amount = Math.Min(missing, Count(goods.Id));
				Take(goods.Id, amount);
				missing -= amount;
				if (missing == 0)
				{
					break;
				}
			}
			FestivalOf(festivalId).Given[def.Kind] = def.Amount - missing;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public bool TryGetFestivalToday(out FestivalDef today)
		{
			today = Catalog.Festivals.Find(festival => FestivalOf(festival.Id).HeldDay == Day);
			return today != null;
		}

		public long FestivalReadyDay(string festivalId)
		{
			FestivalState state = FestivalOf(festivalId);
			return state.Times == 0 ? 0 : state.HeldDay + Catalog.Get<FestivalDef>(festivalId).RepeatDays;
		}

		public HomesteadResult CanOpenFestival(string festivalId)
		{
			FestivalDef festival = Catalog.Get<FestivalDef>(festivalId);
			if (TryGetFestivalToday(out FestivalDef _))
			{
				return HomesteadResult.Fail(HomesteadFailure.DailyLimit);
			}
			if (Day < FestivalReadyDay(festivalId))
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			foreach (FestivalNeed need in FestivalNeeds(festival))
			{
				if (need.IsMet == false)
				{
					return HomesteadResult.Fail(need.Kind == FestivalNeedKind.TownHall ? HomesteadFailure.TownHallTooLow : need.Kind == FestivalNeedKind.Residents ? HomesteadFailure.NobodyThere : HomesteadFailure.Locked);
				}
			}
			for (int slot = 0; slot < festival.Slots.Count; slot++)
			{
				if (festival.Slots[slot].Optional == false && IsSlotFull(festivalId, slot) == false)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods);
				}
			}
			return HomesteadResult.Success;
		}

		public HomesteadResult OpenFestival(string festivalId)
		{
			HomesteadResult result = CanOpenFestival(festivalId);
			if (result.Ok == false)
			{
				return result;
			}
			FestivalDef festival = Catalog.Get<FestivalDef>(festivalId);
			FestivalState state = FestivalOf(festivalId);
			bool feast = IsFeastReady(festival);
			for (int slot = 0; slot < festival.Slots.Count; slot++)
			{
				if (festival.Slots[slot].Optional == false || IsSlotFull(festivalId, slot))
				{
					state.Given.Remove(festival.Slots[slot].Kind);
				}
			}
			state.Cheered.Clear();
			state.HeldDay = Day;
			Grant(state.Times == 0 ? festival.FirstReward : festival.Reward);
			state.Times++;
			if (feast)
			{
				foreach (ResidentDef resident in Catalog.Residents)
				{
					if (HasArrived(resident.Id))
					{
						AddAffinity(resident.Id, festival.FeastPoints);
					}
				}
			}
			FestivalOpened(festivalId);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 선택 칸이 있고 그것까지 다 찼음
		public bool IsFeastReady(FestivalDef festival)
		{
			bool hasOptional = false;
			for (int slot = 0; slot < festival.Slots.Count; slot++)
			{
				if (IsSlotFull(festival.Id, slot) == false)
				{
					return false;
				}
				hasOptional |= festival.Slots[slot].Optional;
			}
			return hasOptional;
		}

		public bool CanCheer(string residentId) => TryGetFestivalToday(out FestivalDef today) && HasArrived(residentId) && FestivalOf(today.Id).Cheered.Contains(residentId) == false;

		// 축제 날 주민에게 말을 걸면 한 번. 선물과 따로 셈
		public HomesteadResult Cheer(string residentId)
		{
			if (TryGetFestivalToday(out FestivalDef today) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			if (HasArrived(residentId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NobodyThere, residentId);
			}
			FestivalState state = FestivalOf(today.Id);
			if (state.Cheered.Contains(residentId))
			{
				return HomesteadResult.Fail(HomesteadFailure.DailyLimit);
			}
			state.Cheered.Add(residentId);
			AddAffinity(residentId, today.CheerPoints);
			NotifyChanged();
			return HomesteadResult.Success;
		}
	}
}
