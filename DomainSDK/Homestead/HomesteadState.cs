using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 세이브에 남는 전부. 값만 있고 규칙은 HomesteadSim
	public sealed class HomesteadState
	{
		// 이 마을이 겪은 게임 분의 합. 벽시계가 세이브에 안 남아 따로 쌓음
		public long Minutes { get; set; }

		public Dictionary<string, int> Store { get; set; } = new Dictionary<string, int>();
		// 처음 얻은 순서대로. 도감과 새 물건 표시
		public List<string> Known { get; set; } = new List<string>();

		public int TownHall { get; set; } = 1;
		public Dictionary<string, int> Buildings { get; set; } = new Dictionary<string, int>();
		public List<WorkState> Works { get; set; } = new List<WorkState>();
		public int GuildExp { get; set; }
		public Dictionary<string, ResidentState> Residents { get; set; } = new Dictionary<string, ResidentState>();
		// 마을 밖에서 알린 사건과 횟수. 영입 조건이 읽음
		public Dictionary<string, int> Events { get; set; } = new Dictionary<string, int>();

		public Dictionary<ToolKind, int> Tools { get; set; } = new Dictionary<ToolKind, int>();
		public List<PlotState> Plots { get; set; } = new List<PlotState>();
		// 채집 자리 -> 다시 나는 분
		public Dictionary<string, long> GatherReady { get; set; } = new Dictionary<string, long>();

		public Dictionary<string, FishRecord> FishRecords { get; set; } = new Dictionary<string, FishRecord>();
		public int MineDeepest { get; set; }

		public int DirtyRelics { get; set; }
		public RelicRestoration Restoring { get; set; }
		public List<string> Donated { get; set; } = new List<string>();
		// 관 id -> 받은 이정표 수
		public Dictionary<string, int> WingClaims { get; set; } = new Dictionary<string, int>();
		// 물건 id -> 받침에 놓인 물고기 크기. 더 큰 것을 가져오면 바뀜
		public Dictionary<string, float> ExhibitSizes { get; set; } = new Dictionary<string, float>();

		public int HouseTier { get; set; } = 1;
		public Dictionary<string, int> OwnedFurniture { get; set; } = new Dictionary<string, int>();
		public List<PlacedFurniture> Placed { get; set; } = new List<PlacedFurniture>();
		public int NextPieceUid { get; set; } = 1;
		public long RestedUntil { get; set; }
		public long RestedOn { get; set; } = -1;
		// 자고 일어나 더 넘어간 날. 생활 분만으로는 밤에 자도 날이 안 바뀔 수 있음
		public int ExtraDays { get; set; }
		public int RestedRank { get; set; }

		// 마을에 놓인 것. 좌표는 차지한 네모의 작은 쪽 칸
		public List<PlacedPiece> Village { get; set; } = new List<PlacedPiece>();
		public int NextVillageUid { get; set; } = 1;

		public List<string> OwnedCostumes { get; set; } = new List<string>();
		public Dictionary<CostumeSlot, string> Worn { get; set; } = new Dictionary<CostumeSlot, string>();

		public List<AnimalState> Animals { get; set; } = new List<AnimalState>();
		public int NextAnimalUid { get; set; } = 1;

		public List<CraftState> Crafts { get; set; } = new List<CraftState>();
		// 일 id -> 맡은 인형
		public Dictionary<string, JobState> Jobs { get; set; } = new Dictionary<string, JobState>();

		public List<RequestState> Requests { get; set; } = new List<RequestState>();
		public int NextRequestUid { get; set; } = 1;
		public long RequestDay { get; set; } = -1;
		public int RequestsDone { get; set; }

		// 이미 알린 일. 귀환 소식과 첫 안내가 되풀이되지 않게
		public List<string> Seen { get; set; } = new List<string>();
	}

	// 벌인 공사. 시간이 차거나 던전에 다녀오면 끝, 가림막은 직접 걷음
	public sealed class WorkState
	{
		public string Building { get; set; } = string.Empty;
		public int Level { get; set; }
		public long ReadyAt { get; set; }
	}

	public sealed class ResidentState
	{
		public bool Arrived { get; set; }
		public bool Greeted { get; set; }
		public int Affinity { get; set; }
		public int ClaimedSteps { get; set; }
		public long GiftDay { get; set; } = -1;
		public long FacilityDay { get; set; } = -1;
	}

	public sealed class PlotState
	{
		public string Crop { get; set; } = string.Empty;
		public bool Tilled { get; set; }
		public float Growth { get; set; }
		public long WetUntil { get; set; }
		public int Harvests { get; set; }
		// 재수확 작물이 익은 채 쌓아 둔 열매 수. 옛 세이브에는 없어 0 으로 읽힘
		public int Stock { get; set; }
	}

	public sealed class FishRecord
	{
		public int Count { get; set; }
		public float BestSize { get; set; }
	}

	public sealed class PlacedFurniture
	{
		public int Uid { get; set; }
		public string Id { get; set; } = string.Empty;
		public float X { get; set; }
		public float Z { get; set; }
		public float Yaw { get; set; }
	}

	public sealed class PlacedPiece
	{
		public int Uid { get; set; }
		public string Id { get; set; } = string.Empty;
		public int X { get; set; }
		public int Z { get; set; }
		// 시계 방향 90도 몇 번
		public int Turn { get; set; }
	}

	public sealed class AnimalState
	{
		public int Uid { get; set; }
		public string Id { get; set; } = string.Empty;
		public int Affinity { get; set; }
		public long CareDay { get; set; } = -1;
		public long ProductSince { get; set; }
	}

	public sealed class CraftState
	{
		public string Recipe { get; set; } = string.Empty;
		// 남은 첫 묶음의 끝 시각. 다음 묶음은 레시피 시간 간격
		public long ReadyAt { get; set; }
		// 남은 묶음 수. 대기열 전의 세이브에는 없어서 1
		public int Batches { get; set; } = 1;
	}

	public sealed class JobState
	{
		public int DollId { get; set; } = -1;
		public long Since { get; set; }
	}

	public sealed class RequestState
	{
		public int Uid { get; set; }
		public string Template { get; set; } = string.Empty;
		public string Goods { get; set; } = string.Empty;
		public int Amount { get; set; }
		public int Nyang { get; set; }
		public int Exp { get; set; }
		public string Resident { get; set; } = string.Empty;
	}

	// 손질 중인 유물. 닦은 자리 수와 남은 상태
	public sealed class RelicRestoration
	{
		public string Relic { get; set; } = string.Empty;
		public int Cleaned { get; set; }
		public int Mistakes { get; set; }
	}
}
