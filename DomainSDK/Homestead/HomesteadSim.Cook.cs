using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 집 부엌과 식탁
	// - 요리는 손으로 한 번에 하나, 시간 없음. 공작소의 시간 제작 (Village) 과 따로
	// - 식탁 한 칸: 음식 하나를 올려 두면 다음 던전이 시작될 때 먹음 (EatMeal). 한 판만
	public sealed partial class HomesteadSim
	{
		// 처음 얻은 목록 (Known) 은 창고에 들어온 것만. 던전 가방에만 있는 재료도 지금 가지고 있으면 열림
		public bool IsCookOpen(CookDef cook) => cook.Start || IsKnown(cook.UnlockBy) || Count(cook.UnlockBy) > 0;

		public HomesteadResult CanCook(string cookId)
		{
			CookDef cook = Catalog.Get<CookDef>(cookId);
			if (IsCookOpen(cook) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, cook.UnlockBy);
			}
			foreach (KeyValuePair<string, int> pair in cook.Inputs)
			{
				if (Count(pair.Key) < pair.Value)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, pair.Key);
				}
			}
			return HomesteadResult.Success;
		}

		// 바늘 하나. 재료는 Cook 에서 냄 (굽다 그만두면 그대로)
		public CookTiming StartCookTiming() => new CookTiming(Tuning, random);

		public HomesteadResult Cook(string cookId, bool wellDone)
		{
			HomesteadResult result = CanCook(cookId);
			if (result.Ok == false)
			{
				return result;
			}
			CookDef cook = Catalog.Get<CookDef>(cookId);
			foreach (KeyValuePair<string, int> pair in cook.Inputs)
			{
				Take(pair.Key, pair.Value);
			}
			Give(cook.Output, wellDone ? 1 + Tuning.CookWellDoneBonus : 1);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 음식의 버프. 요리법이 없는 물건은 식탁에 못 올림
		public bool TryGetMeal(string goodsId, out CookDef meal)
		{
			meal = null;
			foreach (CookDef cook in Catalog.Cooks)
			{
				if (cook.Output == goodsId)
				{
					meal = cook;
					return true;
				}
			}
			return false;
		}

		public bool HasMeal => State.Meal.Length > 0;

		public HomesteadResult CanSetMeal(string goodsId)
		{
			if (TryGetMeal(goodsId, out CookDef _) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, goodsId);
			}
			if (State.Meal == goodsId)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone, goodsId);
			}
			return Count(goodsId) < 1 ? HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId) : HomesteadResult.Success;
		}

		// 바꿔 올리면 먼저 올린 것은 창고로 돌아감
		public HomesteadResult SetMeal(string goodsId)
		{
			HomesteadResult result = CanSetMeal(goodsId);
			if (result.Ok == false)
			{
				return result;
			}
			ReturnMeal();
			Take(goodsId, 1);
			State.Meal = goodsId;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult ClearMeal()
		{
			if (HasMeal == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			ReturnMeal();
			NotifyChanged();
			return HomesteadResult.Success;
		}

		private void ReturnMeal()
		{
			if (HasMeal == false)
			{
				return;
			}
			State.Store[State.Meal] = StoredCount(State.Meal) + 1;
			State.Meal = string.Empty;
		}

		// 던전 시작에 부름. 먹은 음식의 요리법 (버프), 빈 식탁이면 null
		public CookDef EatMeal()
		{
			if (HasMeal == false)
			{
				return null;
			}
			TryGetMeal(State.Meal, out CookDef meal);
			State.Meal = string.Empty;
			NotifyChanged();
			return meal;
		}
	}
}
