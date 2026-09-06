using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 지금 판의 <b>읽기 전용 사진</b> — 코어가 표현에게 건네는 것 (TASK-WM-406).
    ///
    /// ★ 상태 자체를 안 넘긴다 — 넘기면 표현이 코어를 고칠 수 있게 되고,
    ///   그 순간 「코어만으로 게임이 돈다」가 거짓이 된다. 표현을 갈아끼울 때마다 판정이 달라진다.
    /// ★ 화면에 필요한 <b>계산된 값</b>까지 담는다 — 표현마다 같은 계산을 다시 하면 어긋난다.
    ///   3D 창과 글자 창이 다른 숫자를 보이면 그건 버그가 아니라 설계 실패다.
    /// </summary>
    public readonly struct IdleSnapshot : IGameSnapshot
    {
        /// <summary>모은 자원.</summary>
        public double Resource { get; }

        /// <summary>초당 들어오는 자원.</summary>
        public double IncomePerSecond { get; }

        /// <summary>지금까지 처치 수.</summary>
        public long Kills { get; }

        /// <summary>지금 대상의 남은 체력 비율(0~1) — 진행 막대에 쓴다.</summary>
        public double TargetHealthRatio { get; }

        /// <summary>지금 내려와 있는 단계.</summary>
        public int Stage { get; }

        /// <summary>이번 단계에서 처치한 수.</summary>
        public int KillsInStage { get; }

        /// <summary>이번 단계에 필요한 처치 수 — 「몇 남았나」는 표현이 뺄셈하지 말고 이걸 쓴다.</summary>
        public int KillsPerStage { get; }

        /// <summary>여태 모은 리셋 점수.</summary>
        public long PrestigePoints { get; }

        /// <summary>지금 환생하면 받는 점수 — 0 이면 아직 아직 환생 못 한다.</summary>
        public long PrestigeAward { get; }

        /// <summary>환생이 값어치를 갖기 시작하는 깊이 — 이미 값어치가 있으면 0.</summary>
        public int PrestigeNextStage { get; }

        /// <summary>리셋 점수가 지금 주고 있는 배수.</summary>
        public double PrestigeMultiplier { get; }

        /// <summary>등급별로 여태 떨어진 개수 (0번째 = 1등급).</summary>
        public long[] DroppedByTier { get; }

        /// <summary>지금 단계에서 나올 수 있는 가장 높은 등급 — 「더 내려가야 하는 이유」를 그대로 보여주는 값.</summary>
        public int MaxTierNow { get; }

        /// <summary>
        /// 이번 판의 천장 — 아무리 내려가도 여기까지다.
        /// <see cref="MaxTierNow"/> 가 여기 닿았으면 <b>더 내려가도 등급은 안 열린다</b> = 환생할 때다.
        /// </summary>
        public int TierCeiling { get; }

        /// <summary>여태 뽑은 가장 좋은 잠재 값(비율).</summary>
        public double BestPotentialValue { get; }

        /// <summary>그 잠재의 등급.</summary>
        public PotentialGrade BestPotentialGrade { get; }

        /// <summary>지금 자리를 비워도 되는 시간(초) — 환생할수록 는다.</summary>
        public double MaxOfflineSeconds { get; }

        /// <summary>여기 머무는 중인가 — 사람이 고른 것.</summary>
        public bool HoldingStage { get; }

        /// <summary>여태 가장 깊이 간 단계 — 물러났다가 여기로 돌아올 수 있다.</summary>
        public int BestStage { get; }

        /// <summary>가장 잘 벌리는 자리 — 막혔을 때 물러날 곳.</summary>
        public int BestFarmingStage { get; }

        /// <summary>기지 — 생산자 종류별로 화면이 그릴 것.</summary>
        public IdleProducerView[] Producers { get; }

        /// <summary>가방에 든 장비 (읽기만).</summary>
        public IdleItem[] Bag { get; }

        /// <summary>부위마다 차고 있는 것.</summary>
        public IdleItem[] Worn { get; }

        /// <summary>가방 칸 수 — 「몇/몇」을 화면이 뺄셈으로 지어내지 않게.</summary>
        public int BagCapacity { get; }

        /// <summary>
        /// 몇 개를 모아야 한 단계 위로 합치나.
        ///
        /// ★ 화면·안내가 이걸 <b>물어봐야</b> 한다. 전에는 안내 쪽에 3 이 박혀 있어서,
        ///   인스펙터에서 이 손잡이를 4 로 바꾸면 안내만 조용히 거짓말을 했다.
        /// </summary>
        public int MergeCount { get; }

        /// <summary>가진 영웅들 (도감).</summary>
        public IdleHeroView[] Heroes { get; }

        /// <summary>내보낸 셋 — 영웅 id, 빈 자리는 -1.</summary>
        public int[] Party { get; }

        /// <summary>한 번 뽑는 값 (자원) — 뽑을수록 오른다.</summary>
        public double PullCost { get; }

        /// <summary>한 번 뽑는 데 드는 환생석 — 자원과 둘 다 낸다.</summary>
        public long PullStoneCost { get; }

        /// <summary>쓸 수 있는 환생석 (배수와 별개).</summary>
        public long Stones { get; }

        /// <summary>지금 지나가는 것이 떠 있나 (남은 시간, 0 이면 없다).</summary>
        public double VisitorSecondsLeft { get; }

        /// <summary>지금 걸린 폭주와 남은 시간.</summary>
        public IdleSurgeKind SurgeKind { get; }

        public double SurgeSecondsLeft { get; }

        /// <summary>
        /// 지금 걸린 폭주가 <b>몇 배</b>인가 (없으면 1).
        ///
        /// ★ 화면이 「폭주!」만 말하면 사람은 <b>얼마나</b> 좋아졌는지 모른다 — 봉우리를
        ///   느끼라고 넣은 장치인데 느낄 재료를 안 준 셈이다. 배수는 튜닝이 정하므로
        ///   화면이 못 짓는다. 그래서 판정 쪽이 실어 보낸다.
        /// </summary>
        public double SurgeMultiplier { get; }

        /// <summary>지금 뽑을 수 있나.</summary>
        public bool CanPull { get; }

        /// <summary>천장까지 남은 횟수 — 「언젠가는 온다」를 화면이 셀 수 있게.</summary>
        public int PullsToPity { get; }

        /// <summary>묶음 뽑기 수 (사용자 2026-09-05: 10회)</summary>
        public int PullBatchCount { get; }

        /// <summary>묶음 뽑기 값 (자원). 1회의 묶음 수 배</summary>
        public double PullBatchCost { get; }

        /// <summary>묶음 뽑기에 드는 환생석</summary>
        public long PullBatchStoneCost { get; }

        public bool CanPullBatch { get; }

        /// <summary>묶음이 보장하는 최저 등급 (묶음 안에 하나)</summary>
        public IdleHeroGrade PullBatchFloorGrade { get; }

        /// <summary>지금 픽업인 인형. 없으면 -1</summary>
        public int PickupHeroId { get; }

        /// <summary>픽업 인형이 같은 등급 안에서 뽑히는 무게 (다른 인형은 1)</summary>
        public double PickupWeight { get; }

        /// <summary>픽업이 바뀌기까지 남은 초</summary>
        public double PickupSecondsLeft { get; }

        /// <summary>무료 상자를 오늘 열 수 있나</summary>
        public bool FreeBoxReady { get; }

        /// <summary>다음 무료 상자까지 남은 초. 열 수 있으면 0</summary>
        public double FreeBoxSecondsLeft { get; }

        /// <summary>무료 상자가 주는 뽑기 재화</summary>
        public long FreeBoxStones { get; }

        /// <summary>던전 하나에 하루 몇 판인가 (입장권 상한)</summary>
        public long TicketsPerDay { get; }

        /// <summary>던전 보상 장비의 등급 (지금 갈 수 있는 최고 등급)</summary>
        public int DungeonGearTier { get; }

        /// <summary>재화 던전 한 판이 주는 골드</summary>
        public double DungeonGold { get; }

        /// <summary>보스 던전 한 판이 주는 환생 조각</summary>
        public long DungeonBossShards { get; }

        /// <summary>보스 던전 한 판이 주는 장비 수</summary>
        public long DungeonBossGear { get; }

        /// <summary>장비 던전 한 판이 주는 장비 수</summary>
        public long DungeonGearCount { get; }

        /// <summary>
        /// 등급이 나올 확률 — 위에서부터 레전드·에픽·레어(나머지는 일반).
        ///
        /// ★ 화면이 손잡이를 직접 읽지 않게 사진에 싣는다. 그리고 <b>사람에게 보여주기 위한</b>
        ///   값이다 — 확률을 감추면 「관대한 판」이라는 약속을 사용자가 확인할 길이 없다.
        /// </summary>
        public double LegendChance { get; }

        public double EpicChance { get; }

        public double RareChance { get; }

        /// <summary>도감 점수(모은 종류 + 올린 ★)와 그것이 주는 전체 배수.</summary>
        public int DiscoveryScore { get; }

        public double DiscoveryMultiplier { get; }

        /// <summary>공격력 축.</summary>
        public IdleUpgradeView Damage { get; }

        /// <summary>공격속도 축.</summary>
        public IdleUpgradeView AttackSpeed { get; }

        /// <summary>
        /// 지금 초당 몇 번 치나 — 화면이 <b>때리는 장단</b>을 이 숫자에 맞춘다.
        ///
        /// ★ 사용자 지적 (2026-08-16): 「공격 하는지 안 하는지 알 수가 없다」.
        ///   화면이 스스로 장단을 지어내면 코어의 실제 속도와 어긋나고, 그러면
        ///   공격속도를 올려도 <b>빨라진 게 안 보인다</b> — 올린 이유가 사라진다.
        /// </summary>
        public double AttacksPerSecond { get; }

        /// <summary>카드 코스트 — 지금 찬 양.</summary>
        public double Cost { get; }

        /// <summary>코스트 상한 — 「몇/몇」을 화면이 지어내지 않게.</summary>
        public double CostMax { get; }

        /// <summary>긴급 보급이 남은 시간(초) — 0 이면 안 걸려 있다.</summary>
        public double SupplySecondsLeft { get; }

        /// <summary>손패 — 카드마다 값과 「지금 낼 수 있나」.</summary>
        public IdleCardView[] Cards { get; }

        /// <summary>가방을 한 묶음 더 넓히는 값 (상점). 상한이면 0</summary>
        public double BagUpgradeCost { get; }

        /// <summary>지금 살 수 있나. 판정은 코어가 한다</summary>
        public bool CanBuyBag { get; }

        /// <summary>지금 걸린 배속 (gap-2026-08-23 P1-6). 화면이 이 값을 그대로 적는다</summary>
        public double Speed { get; }

        /// <summary>자동 시전이 켜졌나</summary>
        public bool AutoCast { get; }

        /// <summary>던전마다 남은 입장권 (economy.md 4). 화면이 날짜 계산을 다시 하지 않게</summary>
        public long[] Tickets { get; }

        /// <summary>입장권이 다시 찰 때까지 남은 초</summary>
        public double TicketRefillSeconds { get; }

        /// <summary>줄 선 카드 — 다음에 손패로 올라올 순서 (gap-2026-08-23 P1 순환 손패)</summary>
        public IdleCardKind[] Queued { get; }

        /// <summary>자리 넷 — 체력·부활·누가 앉았나 (V2 부대층).</summary>
        public IdleSeatView[] Seats { get; }

        /// <summary>실패해서 반복 중인가 — 화면이 「다음 구역」을 내밀 자리.</summary>
        public bool Repeating { get; }

        /// <summary>마지막으로 깨고 내려간 구역.</summary>
        public int ClearedStage { get; }

        /// <summary>지금 구역 적들이 초당 넣는 피해 — 「얼마나 위험한가」를 화면이 지어내지 않게.</summary>
        public double EnemyDamagePerSecond { get; }

        /// <summary>지금 적에게 넣은 타격 수. 화면이 피해 숫자를 코어 박자에 맞추는 근거 (죽으면 0 부터)</summary>
        public long HitsOnTarget { get; }

        /// <summary>
        /// 판이 여태 앞으로 민 거리. 화면이 세상을 이어 붙이는 데 씀
        ///
        /// ★ 자리 좌표는 웨이브마다 0 기준으로 다시 깎임. 그 몫이 여기 쌓임
        /// </summary>
        public double OriginX { get; }

        /// <summary>인형 자리별 전장 위치 (combat.md 5). 무대의 유일한 근거</summary>
        public IdleFighterView[] Fighters { get; }

        /// <summary>전장에 서 있는 적</summary>
        public IdleFoeView[] Foes { get; }

        /// <summary>이번 진행에서 난 타격. 피해 숫자와 볼트의 근거</summary>
        public IdleHit[] Hits { get; }

        public IdleSnapshot(double resource, double incomePerSecond, long kills, double targetHealthRatio,
            int stage, int killsInStage, int killsPerStage,
            long prestigePoints, long prestigeAward, int prestigeNextStage, double prestigeMultiplier,
            long[] droppedByTier, int maxTierNow, int tierCeiling,
            IdleProducerView[] producers, IdleItem[] bag, IdleItem[] worn, int bagCapacity, int mergeCount,
            double bestPotentialValue, PotentialGrade bestPotentialGrade, double maxOfflineSeconds, bool holdingStage, int bestStage, int bestFarmingStage,
            IdleHeroView[] heroes, int[] party, double pullCost, long pullStoneCost, long stones,
            double visitorSecondsLeft, IdleSurgeKind surgeKind, double surgeSecondsLeft, double surgeMultiplier,
            bool canPull, int pullsToPity, double legendChance, double epicChance, double rareChance,
            int discoveryScore, double discoveryMultiplier,
            IdleUpgradeView damage, IdleUpgradeView attackSpeed, double attacksPerSecond,
            double cost, double costMax, double supplySecondsLeft, IdleCardView[] cards,
            IdleSeatView[] seats, bool repeating, int clearedStage, double enemyDamagePerSecond,
            long hitsOnTarget, double originX, IdleFighterView[] fighters, IdleFoeView[] foes, IdleHit[] hits,
            IdleCardKind[] queued, long[] tickets, double ticketRefillSeconds,
            double speed, bool autoCast, double bagUpgradeCost, bool canBuyBag,
            int pullBatchCount, double pullBatchCost, long pullBatchStoneCost, bool canPullBatch,
            IdleHeroGrade pullBatchFloorGrade, int pickupHeroId, double pickupWeight, double pickupSecondsLeft,
            bool freeBoxReady, double freeBoxSecondsLeft, long freeBoxStones,
            long ticketsPerDay, int dungeonGearTier, double dungeonGold, long dungeonBossShards,
            long dungeonBossGear, long dungeonGearCount)
        {
            TicketsPerDay = ticketsPerDay;
            DungeonGearTier = dungeonGearTier;
            DungeonGold = dungeonGold;
            DungeonBossShards = dungeonBossShards;
            DungeonBossGear = dungeonBossGear;
            DungeonGearCount = dungeonGearCount;
            PullBatchCount = pullBatchCount;
            PullBatchCost = pullBatchCost;
            PullBatchStoneCost = pullBatchStoneCost;
            CanPullBatch = canPullBatch;
            PullBatchFloorGrade = pullBatchFloorGrade;
            PickupHeroId = pickupHeroId;
            PickupWeight = pickupWeight;
            PickupSecondsLeft = pickupSecondsLeft;
            FreeBoxReady = freeBoxReady;
            FreeBoxSecondsLeft = freeBoxSecondsLeft;
            FreeBoxStones = freeBoxStones;
            Speed = speed;
            AutoCast = autoCast;
            BagUpgradeCost = bagUpgradeCost;
            CanBuyBag = canBuyBag;
            Queued = queued;
            Tickets = tickets;
            TicketRefillSeconds = ticketRefillSeconds;
            OriginX = originX;
            Fighters = fighters;
            Foes = foes;
            Hits = hits;
            Cost = cost;
            CostMax = costMax;
            SupplySecondsLeft = supplySecondsLeft;
            Cards = cards;
            Seats = seats;
            Repeating = repeating;
            ClearedStage = clearedStage;
            EnemyDamagePerSecond = enemyDamagePerSecond;
            HitsOnTarget = hitsOnTarget;
            Resource = resource;
            IncomePerSecond = incomePerSecond;
            Kills = kills;
            TargetHealthRatio = targetHealthRatio;
            Stage = stage;
            KillsInStage = killsInStage;
            KillsPerStage = killsPerStage;
            PrestigePoints = prestigePoints;
            PrestigeAward = prestigeAward;
            PrestigeNextStage = prestigeNextStage;
            PrestigeMultiplier = prestigeMultiplier;
            DroppedByTier = droppedByTier;
            MaxTierNow = maxTierNow;
            TierCeiling = tierCeiling;
            Producers = producers;
            Bag = bag;
            Worn = worn;
            BagCapacity = bagCapacity;
            MergeCount = mergeCount;
            BestPotentialValue = bestPotentialValue;
            BestPotentialGrade = bestPotentialGrade;
            MaxOfflineSeconds = maxOfflineSeconds;
            HoldingStage = holdingStage;
            BestStage = bestStage;
            BestFarmingStage = bestFarmingStage;
            Heroes = heroes;
            Party = party;
            PullCost = pullCost;
            PullStoneCost = pullStoneCost;
            Stones = stones;
            VisitorSecondsLeft = visitorSecondsLeft;
            SurgeKind = surgeKind;
            SurgeSecondsLeft = surgeSecondsLeft;
            SurgeMultiplier = surgeMultiplier;
            CanPull = canPull;
            PullsToPity = pullsToPity;
            LegendChance = legendChance;
            EpicChance = epicChance;
            RareChance = rareChance;
            DiscoveryScore = discoveryScore;
            DiscoveryMultiplier = discoveryMultiplier;
            Damage = damage;
            AttackSpeed = attackSpeed;
            AttacksPerSecond = attacksPerSecond;
        }

        /// <summary>축 하나를 골라 본다 — 표현이 반복문으로 그릴 때 쓴다.</summary>
        public IdleUpgradeView ViewOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Damage ? Damage : AttackSpeed;
        }
    }
}

