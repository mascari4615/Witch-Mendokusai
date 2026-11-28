namespace WitchMendokusai.DomainSDK.Homestead
{
	// 창고 물건의 갈래. 박물관 관, 창고 탭, 부탁 조건의 기준
	public enum GoodsKind
	{
		Material = 0,
		Crop = 1,
		Forage = 2,
		Fish = 3,
		Ore = 4,
		Relic = 5,
		Product = 6,
		Dish = 7,
		Seed = 8,
	}

	// 식탁 음식이 다음 던전 한 판에 거는 것. 게임 쪽이 능력치 하나로 옮김
	public enum MealBuff
	{
		Move = 0,
		Regen = 1,
		Exp = 2,
	}

	public enum ToolKind
	{
		Hoe = 0,
		WateringCan = 1,
		Sickle = 2,
		Axe = 3,
		Pickaxe = 4,
		Rod = 5,
	}

	public enum FurnitureLayer
	{
		Floor = 0,
		Rug = 1,
		Wall = 2,
	}

	public enum CostumeSlot
	{
		Hat = 0,
		Outfit = 1,
	}

	// 주민이 오는 길. 하나라도 이뤄지면 도착
	public enum RecruitKind
	{
		Rescue = 0,
		Event = 1,
		Build = 2,
		Favor = 3,
		GuildLevel = 4,
	}

	public enum PlotPhase
	{
		Empty = 0,
		Growing = 1,
		Ripe = 2,
	}

	// 마을 배치물의 층. 길 위에 꾸밈은 겹쳐도 되고 나머지는 서로 못 겹침
	public enum PlaceableKind
	{
		Building = 0,
		Decor = 1,
		Path = 2,
	}

	// 거절 사유. 화면 글은 표현 층 담당
	public enum HomesteadFailure
	{
		None = 0,
		Unknown = 1,
		NotEnoughGoods = 2,
		NotEnoughNyang = 3,
		TownHallTooLow = 4,
		NotBuilt = 5,
		NobodyThere = 6,
		AtMaxLevel = 7,
		AlreadyDone = 8,
		NotReady = 9,
		ToolTooWeak = 10,
		NoRoom = 11,
		Blocked = 12,
		NotOwned = 13,
		DailyLimit = 14,
		Busy = 15,
		Locked = 16,
		LanternOut = 17,
	}
}
