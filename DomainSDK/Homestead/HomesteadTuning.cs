using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 여러 규칙이 같이 읽는 수치. 자료 파일이 정본, 여기는 자료에 없을 때의 값
	public sealed class HomesteadTuning
	{
		public int MinutesPerDay { get; set; } = 1440;

		// 밭
		public int PlotsPerFarmLevel { get; set; } = 2;
		public int WetMinutes { get; set; } = 720;
		public float WetGrowthRate { get; set; } = 1.5f;
		// 남은 물기가 이보다 적으면 다시 줄 수 있음. 던전 한 판 뒤 남는 물기를 덮는 값
		public int WetRefreshMinutes { get; set; } = 360;
		public int SeasonBonusYield { get; set; } = 1;
		public float TillRelicChance { get; set; } = 0.04f;
		// 재수확 작물이 쌓는 열매 상한. 던전 한 판 (600분) 에 두 번 익는 값
		public int RegrowStockCap { get; set; } = 2;
		// 거둘 때 그 작물 씨앗 하나가 덤일 확률
		public float HarvestSeedChance { get; set; } = 0.05f;
		// 던전에서 돌아올 때 씨앗 하나를 들고 올 확률. 지금 타운홀보다 한 단계 위까지
		public float ExpeditionSeedChance { get; set; } = 0.35f;
		public int ExpeditionSeedTownHallAhead { get; set; } = 1;

		// 호감도
		public int GiftLikedPoints { get; set; } = 30;
		public int GiftFavoritePoints { get; set; } = 60;
		public int GiftPlainPoints { get; set; } = 12;
		public int FacilityPoints { get; set; } = 6;
		public int FavorPoints { get; set; } = 80;

		// 길드
		public List<int> GuildSteps { get; set; } = new List<int>();
		public int RequestSlots { get; set; } = 3;
		public int RequestSlotPerLevels { get; set; } = 3;
		public int RequestSlotMax { get; set; } = 6;
		public int RecruitGuildExp { get; set; } = 25;
		public int AffinityGuildExp { get; set; } = 10;
		public int DonateGuildExp { get; set; } = 3;
		// 처음 놓는 가구 종류마다. 꾸미기가 성장으로 이어지는 고리
		public int FirstPlaceGuildExp { get; set; } = 2;
		// 처음 몇 건은 의뢰 경험치 두 배
		public int RequestBonusCount { get; set; } = 5;

		// 낚시
		public float BiteDelayMin { get; set; } = 2f;
		public float BiteDelayMax { get; set; } = 6f;
		public float BiteWindow { get; set; } = 1.1f;
		public float ReelTension { get; set; } = 0.35f;
		public float SlackTension { get; set; } = 0.62f;
		public float SurgeTension { get; set; } = 1.1f;
		public float ReelProgress { get; set; } = 0.18f;
		public float SweetBonus { get; set; } = 1.8f;
		public float SweetFrom { get; set; } = 0.4f;
		public float SweetTo { get; set; } = 0.78f;
		public float DriftProgress { get; set; } = 0.1f;
		public float StartProgress { get; set; } = 0.3f;
		// 낚싯대 단계마다 몸부림을 덜 타고 더 빨리 감음
		public float RodEase { get; set; } = 0.14f;
		public float RodPull { get; set; } = 0.05f;
		public float CalmSeconds { get; set; } = 2.2f;
		public float SurgeSeconds { get; set; } = 0.9f;
		// 실 감기가 이만큼 길어지면 고기가 바늘을 뱉고 떠남. 끝없는 줄다리기를 막음
		public float DuelGiveUpSeconds { get; set; } = 30f;
		// 아직 모르는 물고기의 무는 몫 배수
		public float UnownedFishBias { get; set; } = 2f;

		// 갱도
		public int LanternSwings { get; set; } = 40;
		// 확정 f04-2: 40 + 등급 x 6. 12 면 등불이 남아 곡괭이가 깊이를 못 가름
		public int LanternPerPickTier { get; set; } = 6;
		public int LiftStep { get; set; } = 5;
		public float GlintSeconds { get; set; } = 0.9f;
		public float GlintWindow { get; set; } = 0.22f;
		// 더하기가 아니라 곱. 더하면 낡은 곡괭이 4배, 흑요 1.5배로 등급마다 빛의 값이 달라짐
		public float GlintMultiplier { get; set; } = 3f;
		public float PassageShare { get; set; } = 0.4f;

		// 유물
		public float UnownedRelicBias { get; set; } = 4f;
		public int RelicPerfectNyang { get; set; } = 40;
		// 이미 건 유물을 또 손질하면 냥도 조금. 끝없는 꼬리를 자름
		public int DuplicateRelicNyang { get; set; } = 30;
		// 받침의 물고기를 더 큰 것으로 바꿀 때 그 값의 몫
		public float LargerFishNyangRate { get; set; } = 0.5f;

		// 집
		public int ThemePieces { get; set; } = 3;
		// 확정 f11-1: 8 이면 아끼는 손이 3단에서 멈춤. 16 이면 4단과 5단이 같은 판에 옴
		public int ThemeBonus { get; set; } = 12;
		public float DuplicateFalloff { get; set; } = 0.5f;
		public List<int> ComfortSteps { get; set; } = new List<int>();
		public float RestDoubleChancePerRank { get; set; } = 0.06f;
		public int MorningHour { get; set; } = 6;

		// 목장
		public int AnimalCarePoints { get; set; } = 8;
		public int AnimalMaxAffinity { get; set; } = 100;
		public int RanchSlotsPerLevel { get; set; } = 2;
		// 정을 나눠 보이는 하트 칸 수
		public int AnimalHearts { get; set; } = 5;

		// 공작소
		public int CraftSlots { get; set; } = 2;
		// 칸 하나의 묶음 상한 (공작소 단계별)
		public List<int> CraftQueue { get; set; } = new List<int>();
	}
}
