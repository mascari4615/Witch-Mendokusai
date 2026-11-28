using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 집 솥과 물약 선반
	// - 솥: 재료를 하나씩 넣어 솥 지도 위 물약 칸에 닿게 함. 길 계산은 게임 쪽이 DomainSDK.Alchemy 로 하고 결과만 여기로
	// - 재료는 다 만든 때 한 번에 냄. 다시 젓기와 닫기는 아무것도 잃지 않음
	// - 실패 없음: 어느 칸에도 못 닿으면 흐린 물약 (Fallback) 하나와 마지막 재료 하나를 돌려받음
	// - 처음 닿은 물약은 처방 노트에 적히고, 그다음부터는 노트대로 바로 만듦 (Rebrew)
	// - 선반 한 칸: 물약 하나를 올려 두면 다음 던전이 시작될 때 마심 (DrinkPotion). 한 판만. 식탁과 같이 걸림
	public sealed partial class HomesteadSim
	{
		public BrewDef FallbackBrew => Catalog.Brews.Find(brew => brew.Fallback);

		public bool IsBrewKnown(string brewId) => State.BrewNotes.ContainsKey(brewId);

		public bool TryGetNote(string brewId, out BrewNote note) => State.BrewNotes.TryGetValue(brewId, out note);

		// 물약 칸에 닿았을 때 받는 수
		public int BrewAmount(bool masterwork) => masterwork ? 1 + Tuning.BrewMasterworkBonus : 1;

		// 못 닿았을 때 마지막 재료 하나를 돌려받나 (넣은 수가 문턱 이상)
		public bool BrewMissKeeps(int usedCount) => usedCount >= Tuning.BrewMissReturnFrom;

		// 솥에 하나 더 넣을 수 있나. used 는 이번 솥에 이미 넣은 것 (아직 창고에서 안 냄)
		public HomesteadResult CanAddBrewIngredient(string goodsId, IReadOnlyList<string> used)
		{
			if (Catalog.TryGet(goodsId, out BrewIngredientDef _) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, goodsId);
			}
			return Count(goodsId) > CountOf(used, goodsId) ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId);
		}

		// 물약 칸에 닿음. masterwork 면 하나 더 (명품). 재료가 그새 줄었으면 아무것도 안 함
		public HomesteadResult BrewReached(string brewId, IReadOnlyList<string> used, bool masterwork)
		{
			BrewDef brew = Catalog.Get<BrewDef>(brewId);
			HomesteadResult result = CanUse(used);
			if (result.Ok == false)
			{
				return result;
			}
			if (brew.Fallback)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, brewId);
			}
			Dictionary<string, int> inputs = Tally(used);
			foreach (KeyValuePair<string, int> pair in inputs)
			{
				Take(pair.Key, pair.Value);
			}
			int amount = BrewAmount(masterwork);
			Give(brew.Output, amount);
			WriteNote(brewId, inputs, amount);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 어느 칸에도 못 닿음. 흐린 물약 하나, 문턱 (BrewMissReturnFrom) 이상 넣었으면 마지막 재료 하나는 돌려받음 (냄에서 뺌)
		// 문턱보다 적게 넣고 끝내면 다 냄. 빈손으로 흐린 물약을 찍어 내지 못하게
		public HomesteadResult BrewMissed(IReadOnlyList<string> used)
		{
			if (used.Count == 0)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			HomesteadResult result = CanUse(used);
			if (result.Ok == false)
			{
				return result;
			}
			int keep = BrewMissKeeps(used.Count) ? 1 : 0;
			for (int i = 0; i < used.Count - keep; i++)
			{
				Take(used[i], 1);
			}
			Give(FallbackBrew.Output, 1);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult CanRebrew(string brewId)
		{
			if (TryGetNote(brewId, out BrewNote note) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, brewId);
			}
			foreach (KeyValuePair<string, int> pair in note.Inputs)
			{
				if (Count(pair.Key) < pair.Value)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, pair.Key);
				}
			}
			return HomesteadResult.Success;
		}

		// 노트대로 바로. 노트에 적힌 수만큼 나옴
		public HomesteadResult Rebrew(string brewId)
		{
			HomesteadResult result = CanRebrew(brewId);
			if (result.Ok == false)
			{
				return result;
			}
			BrewNote note = State.BrewNotes[brewId];
			foreach (KeyValuePair<string, int> pair in note.Inputs)
			{
				Take(pair.Key, pair.Value);
			}
			Give(Catalog.Get<BrewDef>(brewId).Output, note.Amount);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 더 많이 받는 길, 같으면 재료가 더 적은 길로 바꿈
		private void WriteNote(string brewId, Dictionary<string, int> inputs, int amount)
		{
			if (TryGetNote(brewId, out BrewNote old) && (old.Amount > amount || (old.Amount == amount && Sum(old.Inputs) <= Sum(inputs))))
			{
				return;
			}
			State.BrewNotes[brewId] = new BrewNote { Inputs = inputs, Amount = amount };
		}

		private HomesteadResult CanUse(IReadOnlyList<string> used)
		{
			foreach (KeyValuePair<string, int> pair in Tally(used))
			{
				if (Count(pair.Key) < pair.Value)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, pair.Key);
				}
			}
			return HomesteadResult.Success;
		}

		private static Dictionary<string, int> Tally(IReadOnlyList<string> used)
		{
			Dictionary<string, int> tally = new Dictionary<string, int>();
			foreach (string goodsId in used)
			{
				tally[goodsId] = tally.TryGetValue(goodsId, out int amount) ? amount + 1 : 1;
			}
			return tally;
		}

		private static int CountOf(IReadOnlyList<string> used, string goodsId)
		{
			int count = 0;
			foreach (string each in used)
			{
				if (each == goodsId)
				{
					count++;
				}
			}
			return count;
		}

		private static int Sum(Dictionary<string, int> inputs)
		{
			int sum = 0;
			foreach (int amount in inputs.Values)
			{
				sum += amount;
			}
			return sum;
		}

		// 물약의 효과. 솥 물약이 아닌 물건은 선반에 못 올림
		public bool TryGetPotion(string goodsId, out BrewDef potion)
		{
			potion = Catalog.Brews.Find(brew => brew.Output == goodsId);
			return potion != null;
		}

		public bool HasPotion => State.Potion.Length > 0;

		public HomesteadResult CanSetPotion(string goodsId)
		{
			if (TryGetPotion(goodsId, out BrewDef _) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, goodsId);
			}
			if (State.Potion == goodsId)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone, goodsId);
			}
			return Count(goodsId) < 1 ? HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId) : HomesteadResult.Success;
		}

		// 바꿔 올리면 먼저 올린 것은 창고로 돌아감
		public HomesteadResult SetPotion(string goodsId)
		{
			HomesteadResult result = CanSetPotion(goodsId);
			if (result.Ok == false)
			{
				return result;
			}
			ReturnPotion();
			Take(goodsId, 1);
			State.Potion = goodsId;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult ClearPotion()
		{
			if (HasPotion == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			ReturnPotion();
			NotifyChanged();
			return HomesteadResult.Success;
		}

		private void ReturnPotion()
		{
			if (HasPotion == false)
			{
				return;
			}
			State.Store[State.Potion] = StoredCount(State.Potion) + 1;
			State.Potion = string.Empty;
		}

		// 던전 시작에 부름. 마신 물약 (효과), 빈 선반이면 null
		public BrewDef DrinkPotion()
		{
			if (HasPotion == false)
			{
				return null;
			}
			TryGetPotion(State.Potion, out BrewDef potion);
			State.Potion = string.Empty;
			NotifyChanged();
			return potion;
		}
	}
}
