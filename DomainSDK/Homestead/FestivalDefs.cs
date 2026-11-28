using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 내가 여는 마을 축제. 날짜 없음, 준비가 차면 아무 날이나 엶
	public sealed class FestivalDef : HomesteadDef
	{
		public int TownHall { get; set; } = 1;
		// 와 있어야 할 주민 수
		public int Residents { get; set; }
		// 마을에 놓여 있어야 할 꾸밈 수. 세기만 하고 쓰지 않음
		public int Decor { get; set; }
		// 한 번 연 뒤 다시 열 수 있기까지 날
		public int RepeatDays { get; set; } = 7;
		public List<FestivalSlotDef> Slots { get; set; } = new List<FestivalSlotDef>();
		// 축제 날 주민 한 명에게 건네는 축하 인사의 호감
		public int CheerPoints { get; set; }
		// 선택 칸 (음식) 을 채워 열면 온 주민 모두에게 더하는 호감
		public int FeastPoints { get; set; }
		public RewardDef FirstReward { get; set; } = new RewardDef();
		public RewardDef Reward { get; set; } = new RewardDef();
	}

	// 준비물 칸 하나. 물건 하나가 아니라 갈래로 받음 (어느 작물이든)
	public sealed class FestivalSlotDef
	{
		public string Name { get; set; } = string.Empty;
		public GoodsKind Kind { get; set; }
		public int Amount { get; set; }
		// 비어 있어도 열림. 채우면 FeastPoints
		public bool Optional { get; set; }
	}

	public sealed class FestivalState
	{
		// 칸 번호 -> 바친 수. 열 때 비움
		public Dictionary<int, int> Given { get; set; } = new Dictionary<int, int>();
		public long HeldDay { get; set; } = -1;
		public int Times { get; set; }
		// 이번 축제 날 인사를 받은 주민
		public List<string> Cheered { get; set; } = new List<string>();
	}

	// 여는 조건 한 줄. 화면이 그대로 "지금 / 필요" 로 보임
	public readonly struct FestivalNeed
	{
		public readonly FestivalNeedKind Kind;
		public readonly int Now;
		public readonly int Need;

		public FestivalNeed(FestivalNeedKind kind, int now, int need)
		{
			Kind = kind;
			Now = now;
			Need = need;
		}

		public bool IsMet => Now >= Need;
	}

	public enum FestivalNeedKind
	{
		TownHall = 0,
		Residents = 1,
		Decor = 2,
	}
}
