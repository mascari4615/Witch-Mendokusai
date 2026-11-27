using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 공작소 가공과 목장, 그리고 귀환 소식
	public sealed partial class HomesteadSim
	{
		public const string RANCH = "ranch";

		// ---- 가공

		public int CraftSlots => IsBuilt(WORKSHOP) ? Tuning.CraftSlots + BuildingLevel(WORKSHOP) - 1 : 0;

		public bool IsCraftDone(CraftState craft) => State.Minutes >= craft.ReadyAt;

		public HomesteadResult CanCraft(string recipeId)
		{
			RecipeDef recipe = Catalog.Get<RecipeDef>(recipeId);
			bool isBench = recipe.Building == BENCH;
			if (isBench == false && BuildingLevel(recipe.Building) < recipe.Level)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, recipe.Building);
			}
			if (isBench == false && State.Crafts.Count >= CraftSlots)
			{
				return HomesteadResult.Fail(HomesteadFailure.Busy);
			}
			foreach (KeyValuePair<string, int> pair in recipe.Inputs)
			{
				if (Count(pair.Key) < pair.Value)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, pair.Key);
				}
			}
			return HomesteadResult.Success;
		}

		public HomesteadResult StartCraft(string recipeId)
		{
			HomesteadResult result = CanCraft(recipeId);
			if (result.Ok == false)
			{
				return result;
			}
			RecipeDef recipe = Catalog.Get<RecipeDef>(recipeId);
			foreach (KeyValuePair<string, int> pair in recipe.Inputs)
			{
				Take(pair.Key, pair.Value);
			}
			if (recipe.Building == BENCH)
			{
				Give(recipe.Output, recipe.Amount);
				NotifyChanged();
				return HomesteadResult.Success;
			}
			State.Crafts.Add(new CraftState { Recipe = recipeId, ReadyAt = State.Minutes + recipe.Minutes });
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public int CollectCrafts()
		{
			List<CraftState> done = State.Crafts.FindAll(IsCraftDone);
			foreach (CraftState craft in done)
			{
				RecipeDef recipe = Catalog.Get<RecipeDef>(craft.Recipe);
				Give(recipe.Output, recipe.Amount);
				State.Crafts.Remove(craft);
			}
			if (done.Count > 0)
			{
				NotifyChanged();
			}
			return done.Count;
		}

		// ---- 목장. 안 돌봐도 줄지 않음

		public int RanchSlots => BuildingLevel(RANCH) * Tuning.RanchSlotsPerLevel;

		public HomesteadResult CanBuyAnimal(string animalId)
		{
			AnimalDef animal = Catalog.Get<AnimalDef>(animalId);
			if (BuildingLevel(RANCH) < animal.RanchLevel)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, RANCH);
			}
			return State.Animals.Count >= RanchSlots ? HomesteadResult.Fail(HomesteadFailure.NoRoom) : CanPay(animal.Cost);
		}

		public HomesteadResult BuyAnimal(string animalId)
		{
			HomesteadResult result = CanBuyAnimal(animalId);
			if (result.Ok == false)
			{
				return result;
			}
			Pay(Catalog.Get<AnimalDef>(animalId).Cost);
			State.Animals.Add(new AnimalState { Uid = State.NextAnimalUid++, Id = animalId, ProductSince = State.Minutes });
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public bool IsProductReady(AnimalState animal) => ProductsReady(animal) > 0;

		// 비운 동안 하루치까지 쌓임. 던전에 다녀와도 놓친 몫이 하루를 넘지 않음
		public int ProductCap(AnimalDef def) => Math.Max(1, Tuning.MinutesPerDay / def.IntervalMinutes);

		public int ProductsReady(AnimalState animal)
		{
			AnimalDef def = Catalog.Get<AnimalDef>(animal.Id);
			long made = (State.Minutes - animal.ProductSince) / def.IntervalMinutes;
			return (int)Math.Min(ProductCap(def), made);
		}

		// 쓰다듬기와 거두기를 한 번에. 하루 한 번 정이 오르고, 다 된 것을 모두 거둠.
		// 정이 차면 거둘 때마다 좋은 산물 하나가 덤 (공작소가 받는 보통 산물이 끊기지 않게, 쌓인 몫마다 붙이지 않음)
		public HomesteadResult Care(int uid, out bool collected)
		{
			collected = false;
			AnimalState animal = State.Animals.Find(candidate => candidate.Uid == uid);
			if (animal == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown);
			}
			bool cared = animal.CareDay != Day;
			if (cared)
			{
				animal.CareDay = Day;
				animal.Affinity = Math.Min(Tuning.AnimalMaxAffinity, animal.Affinity + Tuning.AnimalCarePoints);
			}
			int ready = ProductsReady(animal);
			if (ready > 0)
			{
				AnimalDef def = Catalog.Get<AnimalDef>(animal.Id);
				bool fine = def.FineProduct.Length > 0 && animal.Affinity >= def.FineAffinity;
				Give(def.Product, ready);
				if (fine)
				{
					Give(def.FineProduct, 1);
				}
				bool overflowed = (State.Minutes - animal.ProductSince) / def.IntervalMinutes > ready;
				animal.ProductSince = overflowed ? State.Minutes : animal.ProductSince + (long)ready * def.IntervalMinutes;
				collected = true;
			}
			if (cared == false && collected == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.DailyLimit);
			}
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// ---- 귀환 소식. 좋은 일만, 없으면 빈 채

		public HomesteadDigest MakeDigest()
		{
			HomesteadDigest digest = new HomesteadDigest();
			foreach (PlotState plot in State.Plots)
			{
				if (PhaseOf(plot) == PlotPhase.Ripe)
				{
					digest.RipePlots++;
				}
			}
			digest.DoneCrafts = State.Crafts.FindAll(IsCraftDone).Count;
			foreach (JobDef job in Catalog.Jobs)
			{
				digest.JobYields += JobReady(job.Id);
			}
			foreach (AnimalState animal in State.Animals)
			{
				digest.AnimalProducts += ProductsReady(animal);
			}
			foreach (WorkState work in State.Works)
			{
				if (IsWorkDone(work))
				{
					digest.DoneWorks.Add(work.Building);
				}
			}
			foreach (ResidentDef resident in Catalog.Residents)
			{
				if (HasArrived(resident.Id) && ResidentOf(resident.Id).Greeted == false)
				{
					digest.NewResidents.Add(resident.Id);
				}
			}
			foreach (RequestState request in State.Requests)
			{
				if (Count(request.Goods) >= request.Amount)
				{
					digest.DeliverableRequests++;
				}
			}
			digest.DirtyRelics = State.DirtyRelics;
			return digest;
		}

		public void Greet(string residentId)
		{
			ResidentOf(residentId).Greeted = true;
			NotifyChanged();
		}
	}

	public sealed class HomesteadDigest
	{
		public int RipePlots { get; set; }
		public int DoneCrafts { get; set; }
		public int JobYields { get; set; }
		public int AnimalProducts { get; set; }
		public int DeliverableRequests { get; set; }
		public int DirtyRelics { get; set; }
		public List<string> DoneWorks { get; } = new List<string>();
		public List<string> NewResidents { get; } = new List<string>();

		public bool IsEmpty => RipePlots + DoneCrafts + JobYields + AnimalProducts + DeliverableRequests + DirtyRelics + DoneWorks.Count + NewResidents.Count == 0;
	}
}
