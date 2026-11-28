namespace WitchMendokusai.DomainSDK.Homestead
{
	// 사역마. 욘의 집에 사는 작은 동료 (자료 familiar.json)
	// - 알이 집에 오면 깨움 (Hatch). 알 길은 던전 다녀옴, 보스 처치, 길드 레벨
	// - 깨운 것 중 하나를 데려감 (TakeAlong). 던전 판마다 줍기 반경을 더함 (FamiliarForRun). 먹어 없어지는 식탁 음식과 달리 남음
	// - 쓰다듬기는 하루 한 번 반가워할 뿐. 정도 산물도 없음 (목장, 주민 호감도와 겹치지 않게)
	// - 벌 없음: 배고픔, 떠남, 기한 없음
	public sealed partial class HomesteadSim
	{
		public const string EXPEDITION_EVENT = "expedition";

		public bool IsHatched(string familiarId) => State.Familiars.Exists(familiar => familiar.Id == familiarId);

		// 깨우기 전, 알 길 하나라도 채웠으면 알이 집에 있음
		public bool IsEggHere(FamiliarDef familiar)
		{
			if (IsHatched(familiar.Id))
			{
				return false;
			}
			bool byEvent = familiar.Event.Length > 0 && EventCount(familiar.Event) >= familiar.EventCount;
			bool byGuild = familiar.GuildLevel > 1 && GuildLevel >= familiar.GuildLevel;
			return byEvent || byGuild;
		}

		// 집에 보이는 알. 한 번에 하나, 자료 순서대로. 없으면 null
		public FamiliarDef NextEgg()
		{
			foreach (FamiliarDef familiar in Catalog.Familiars)
			{
				if (IsEggHere(familiar))
				{
					return familiar;
				}
			}
			return null;
		}

		// 처음 깨운 사역마는 바로 데려갈 하나가 됨
		public HomesteadResult Hatch(string familiarId)
		{
			FamiliarDef familiar = Catalog.Get<FamiliarDef>(familiarId);
			if (IsHatched(familiarId))
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone, familiarId);
			}
			if (IsEggHere(familiar) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady, familiarId);
			}
			State.Familiars.Add(new FamiliarState { Id = familiarId });
			if (State.FamiliarWith.Length == 0)
			{
				State.FamiliarWith = familiarId;
			}
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public bool IsTakenAlong(string familiarId) => State.FamiliarWith == familiarId;

		public HomesteadResult TakeAlong(string familiarId)
		{
			if (IsHatched(familiarId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotOwned, familiarId);
			}
			if (IsTakenAlong(familiarId))
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone, familiarId);
			}
			State.FamiliarWith = familiarId;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public bool CanPetFamiliar(string familiarId)
		{
			FamiliarState familiar = State.Familiars.Find(each => each.Id == familiarId);
			return familiar != null && familiar.PetDay != Day;
		}

		public HomesteadResult PetFamiliar(string familiarId)
		{
			FamiliarState familiar = State.Familiars.Find(each => each.Id == familiarId);
			if (familiar == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotOwned, familiarId);
			}
			if (familiar.PetDay == Day)
			{
				return HomesteadResult.Fail(HomesteadFailure.DailyLimit, familiarId);
			}
			familiar.PetDay = Day;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 던전 시작에 부름. 데려간 사역마, 없으면 null. 식탁과 달리 비우지 않음
		public FamiliarDef FamiliarForRun()
		{
			if (State.FamiliarWith.Length == 0)
			{
				return null;
			}
			return Catalog.TryGet(State.FamiliarWith, out FamiliarDef familiar) ? familiar : null;
		}
	}
}
