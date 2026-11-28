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
		public int SeasonBonusYield { get; set; } = 1;
		public float TillRelicChance { get; set; } = 0.04f;

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

		// 갱도
		public int LanternSwings { get; set; } = 40;
		public int LanternPerPickTier { get; set; } = 12;
		public int LiftStep { get; set; } = 5;
		public float GlintSeconds { get; set; } = 0.9f;
		public float GlintWindow { get; set; } = 0.22f;
		public int GlintPower { get; set; } = 3;
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
		public int ThemeBonus { get; set; } = 8;
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

		// 집 부엌. 바늘이 0 과 1 사이를 오가고 알맞은 칸에서 누르면 잘 구움
		public float CookSweetFrom { get; set; } = 0.62f;
		public float CookSweetTo { get; set; } = 0.8f;
		// 바늘이 한 번 오가는 데 걸리는 초
		public float CookSwingSeconds { get; set; } = 1.4f;
		// 누르지 않고 이만큼 지나면 그냥 다 구워짐 (실패 없음)
		public float CookMaxSeconds { get; set; } = 6f;
		public int CookWellDoneBonus { get; set; } = 1;
		// 솥 지도에서 물약 칸 가운데에 깨끗이 닿으면 (명품) 더 받는 수
		public int BrewMasterworkBonus { get; set; } = 1;
		// 솥이 어느 칸에도 못 닿았을 때 이만큼 이상 넣었으면 마지막 재료 하나를 돌려받음
		public int BrewMissReturnFrom { get; set; } = 2;
	}
}
