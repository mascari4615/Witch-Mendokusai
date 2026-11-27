using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 공작소 가공. 칸 하나에 같은 레시피 여러 묶음을 줄 세움 (대기열)
	// 묶음은 하나씩 차례로 만들어지고, 거둘 때는 끝난 묶음만 창고로. 다 된 것은 칸을 막지 않음
	public sealed partial class HomesteadSim
	{
		public int CraftSlots => IsBuilt(WORKSHOP) ? Tuning.CraftSlots + BuildingLevel(WORKSHOP) - 1 : 0;

		// 칸 하나에 줄 세울 수 있는 묶음 수. 공작소 단계마다 자료 값, 자료가 없으면 1
		public int CraftQueueMax
		{
			get
			{
				if (IsBuilt(WORKSHOP) == false || Tuning.CraftQueue.Count == 0)
				{
					return 1;
				}
				int index = Math.Min(BuildingLevel(WORKSHOP), Tuning.CraftQueue.Count) - 1;
				return Math.Max(1, Tuning.CraftQueue[index]);
			}
		}

		// 지금까지 끝난 묶음 수. 첫 묶음이 ReadyAt 에 끝나고, 그 뒤로 레시피 시간마다 하나씩
		public int DoneBatches(CraftState craft)
		{
			if (State.Minutes < craft.ReadyAt)
			{
				return 0;
			}
			int minutes = Math.Max(1, Catalog.Get<RecipeDef>(craft.Recipe).Minutes);
			long after = (State.Minutes - craft.ReadyAt) / minutes;
			return (int)Math.Min(craft.Batches, 1 + after);
		}

		// 아직 손대지 않은 묶음 수 (만드는 중인 하나와 끝난 것은 빼고). 빼면 재료를 전부 돌려받음
		public int QueuedBatches(CraftState craft) => Math.Max(0, craft.Batches - DoneBatches(craft) - 1);

		public long CraftFinishAt(CraftState craft) => craft.ReadyAt + (long)Catalog.Get<RecipeDef>(craft.Recipe).Minutes * (craft.Batches - 1);

		public bool IsCraftDone(CraftState craft) => DoneBatches(craft) >= craft.Batches;

		public int BusyCrafts => State.Crafts.FindAll(craft => IsCraftDone(craft) == false).Count;

		// 거둘 수 있는 묶음 수 (칸 전체)
		public int ReadyBatches
		{
			get
			{
				int ready = 0;
				foreach (CraftState craft in State.Crafts)
				{
					ready += DoneBatches(craft);
				}
				return ready;
			}
		}

		public HomesteadResult CanCraft(string recipeId) => CanCraft(recipeId, 1);

		public HomesteadResult CanCraft(string recipeId, int batches)
		{
			RecipeDef recipe = Catalog.Get<RecipeDef>(recipeId);
			bool isBench = recipe.Building == BENCH;
			if (isBench == false && BuildingLevel(recipe.Building) < recipe.Level)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, recipe.Building);
			}
			if (isBench == false && BusyCrafts >= CraftSlots)
			{
				return HomesteadResult.Fail(HomesteadFailure.Busy);
			}
			if (batches < 1 || (isBench == false && batches > CraftQueueMax))
			{
				return HomesteadResult.Fail(HomesteadFailure.NoRoom);
			}
			foreach (KeyValuePair<string, int> pair in recipe.Inputs)
			{
				if (Count(pair.Key) < pair.Value * batches)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, pair.Key);
				}
			}
			return HomesteadResult.Success;
		}

		// 재료가 닿는 만큼, 칸 하나의 상한까지
		public int MaxCraftBatches(string recipeId)
		{
			RecipeDef recipe = Catalog.Get<RecipeDef>(recipeId);
			int most = recipe.Building == BENCH ? int.MaxValue : CraftQueueMax;
			foreach (KeyValuePair<string, int> pair in recipe.Inputs)
			{
				most = Math.Min(most, Count(pair.Key) / Math.Max(1, pair.Value));
			}
			return Math.Max(0, most);
		}

		public HomesteadResult StartCraft(string recipeId) => StartCraft(recipeId, 1);

		// 재료는 묶음 수만큼 한 번에 빼고 칸은 하나만 씀
		public HomesteadResult StartCraft(string recipeId, int batches)
		{
			HomesteadResult result = CanCraft(recipeId, batches);
			if (result.Ok == false)
			{
				return result;
			}
			RecipeDef recipe = Catalog.Get<RecipeDef>(recipeId);
			foreach (KeyValuePair<string, int> pair in recipe.Inputs)
			{
				Take(pair.Key, pair.Value * batches);
			}
			if (recipe.Building == BENCH)
			{
				Give(recipe.Output, recipe.Amount * batches);
				NotifyChanged();
				return HomesteadResult.Success;
			}
			TakeDoneCrafts();
			State.Crafts.Add(new CraftState { Recipe = recipeId, ReadyAt = State.Minutes + recipe.Minutes, Batches = batches });
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 거둔 묶음 수
		public int CollectCrafts()
		{
			int done = TakeDoneCrafts();
			if (done > 0)
			{
				NotifyChanged();
			}
			return done;
		}

		// 손대지 않은 묶음을 빼고 재료를 전부 돌려줌. 끝난 묶음은 먼저 창고로. 돌려준 묶음 수
		public int CancelQueuedCraft(int slot)
		{
			if (slot < 0 || slot >= State.Crafts.Count)
			{
				return 0;
			}
			CraftState craft = State.Crafts[slot];
			int queued = QueuedBatches(craft);
			if (queued == 0)
			{
				return 0;
			}
			RecipeDef recipe = Catalog.Get<RecipeDef>(craft.Recipe);
			craft.Batches -= queued;
			foreach (KeyValuePair<string, int> pair in recipe.Inputs)
			{
				Give(pair.Key, pair.Value * queued);
			}
			TakeDoneCrafts();
			NotifyChanged();
			return queued;
		}

		private int TakeDoneCrafts()
		{
			int taken = 0;
			foreach (CraftState craft in State.Crafts.ToArray())
			{
				int done = DoneBatches(craft);
				if (done == 0)
				{
					continue;
				}
				RecipeDef recipe = Catalog.Get<RecipeDef>(craft.Recipe);
				Give(recipe.Output, recipe.Amount * done);
				taken += done;
				craft.Batches -= done;
				craft.ReadyAt += (long)recipe.Minutes * done;
				if (craft.Batches <= 0)
				{
					State.Crafts.Remove(craft);
				}
			}
			return taken;
		}
	}
}
