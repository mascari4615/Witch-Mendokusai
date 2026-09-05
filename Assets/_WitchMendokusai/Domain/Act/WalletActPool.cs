using WitchMendokusai.DomainSDK.Act;

namespace WitchMendokusai
{
	// 지갑(냥)을 원장의 창고로 쓴다 (TASK-WM-410 — 두 갈래길의 「팔기」 쪽).
	//
	// ★ 왜 자원으로 다루나: 팔기는 <b>한 행동</b>이다 — 작물이 줄고 돈이 느는 것이 전부 되거나 전부 안 돼야 한다.
	//   돈을 원장 밖에서 따로 더하면 「작물만 사라진」 세계가 만들어질 수 있다.
	// ★ 냥의 자원 번호는 아이템 대역(~1억) 위에 둔다 — 같은 원장에서 아이템 ID 와 절대 안 겹치게.
	public sealed class WalletActPool : IActResourcePool
	{
		/// <summary>냥의 자원 번호. 아이템 ID 상한(DataSODefine.ID_MAX) 위 = 충돌 없음.</summary>
		public static readonly ResourceId NYANG = new ResourceId(DataSODefine.ID_MAX + 1);

		public static bool Handles(ResourceId resource) => resource.Equals(NYANG);

		private readonly GameStat gameStat;

		public WalletActPool(GameStat gameStat)
		{
			this.gameStat = gameStat;
		}

		public int AmountOf(ResourceId resource)
		{
			return gameStat == null || Handles(resource) == false ? 0 : gameStat[GameStatType.NYANG];
		}

		public void Add(ResourceId resource, int amount)
		{
			if (gameStat == null || Handles(resource) == false || amount == 0)
			{
				return;
			}

			gameStat[GameStatType.NYANG] += amount;
		}
	}
}
