using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 던전 가방과 냥을 생활 규칙의 주머니로
	public sealed class HomesteadPurse : IHomesteadPurse
	{
		private readonly Inventory inventory;
		private readonly GameStat gameStat;

		public HomesteadPurse(Inventory inventory, GameStat gameStat)
		{
			this.inventory = inventory;
			this.gameStat = gameStat;
		}

		public int Nyang
		{
			get => (int)gameStat[GameStatType.NYANG];
			set => gameStat[GameStatType.NYANG] = value;
		}

		public int CountItem(int itemId) => inventory.CountByID(itemId);

		public int ConsumeItem(int itemId, int amount) => inventory.Consume(itemId, amount);
	}
}
