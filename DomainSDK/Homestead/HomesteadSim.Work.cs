using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 목장과 귀환 소식. 공작소 가공은 HomesteadSim.Craft.cs
	public sealed partial class HomesteadSim
	{
		public const string RANCH = "ranch";

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

		// 정을 하트 칸으로. 끝 정이면 칸이 다 참
		public int AnimalHearts(AnimalState animal) => animal.Affinity * Tuning.AnimalHearts / Tuning.AnimalMaxAffinity;

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
			digest.DoneCrafts = ReadyBatches;
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

		// 마지막으로 보인 소식. 저장하지 않음 (켤 때마다 한 번은 보임)
		private HomesteadDigest shownDigest;

		// 지난번 보인 뒤 늘거나 새로 생긴 것이 있을 때만 지금 소식 전부, 아니면 빈 채
		// 마을과 실내를 오갈 때마다 같은 소식이 다시 뜨지 않게
		public HomesteadDigest TakeNewDigest()
		{
			HomesteadDigest digest = MakeDigest();
			bool hasNews = digest.HasNewsSince(shownDigest);
			shownDigest = digest;
			return hasNews ? digest : new HomesteadDigest();
		}

		// 거두기, 납품, 인사처럼 소식거리가 줄면 기준도 같이 내림
		// 안 내리면 거둔 뒤 다시 찬 칸이 지난번과 같은 수라 새 소식으로 안 잡힘
		private void LowerShownDigest()
		{
			if (shownDigest != null)
			{
				shownDigest = HomesteadDigest.Lower(shownDigest, MakeDigest());
			}
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

		public bool HasNewsSince(HomesteadDigest before)
		{
			if (before == null)
			{
				return IsEmpty == false;
			}
			return RipePlots > before.RipePlots
				|| DoneCrafts > before.DoneCrafts
				|| JobYields > before.JobYields
				|| AnimalProducts > before.AnimalProducts
				|| DeliverableRequests > before.DeliverableRequests
				|| DirtyRelics > before.DirtyRelics
				|| DoneWorks.Exists(building => before.DoneWorks.Contains(building) == false)
				|| NewResidents.Exists(resident => before.NewResidents.Contains(resident) == false);
		}

		// 칸마다 둘 중 작은 쪽, 목록은 둘 다에 있는 것만
		public static HomesteadDigest Lower(HomesteadDigest a, HomesteadDigest b)
		{
			HomesteadDigest lower = new HomesteadDigest
			{
				RipePlots = Math.Min(a.RipePlots, b.RipePlots),
				DoneCrafts = Math.Min(a.DoneCrafts, b.DoneCrafts),
				JobYields = Math.Min(a.JobYields, b.JobYields),
				AnimalProducts = Math.Min(a.AnimalProducts, b.AnimalProducts),
				DeliverableRequests = Math.Min(a.DeliverableRequests, b.DeliverableRequests),
				DirtyRelics = Math.Min(a.DirtyRelics, b.DirtyRelics),
			};
			lower.DoneWorks.AddRange(a.DoneWorks.FindAll(b.DoneWorks.Contains));
			lower.NewResidents.AddRange(a.NewResidents.FindAll(b.NewResidents.Contains));
			return lower;
		}

		public bool IsEmpty => RipePlots + DoneCrafts + JobYields + AnimalProducts + DeliverableRequests + DirtyRelics + DoneWorks.Count + NewResidents.Count == 0;
	}
}
