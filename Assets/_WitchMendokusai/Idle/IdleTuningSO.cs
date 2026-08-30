using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai
{
    /// <summary>
    /// 방치 판의 숫자를 <b>인스펙터에서</b> 만지는 자리 (TASK-WM-406).
    ///
    /// ★ 수치 노출 룰의 방치형 쪽 형태 — 코어(DomainSDK)는 Unity 를 모르므로 SO 가 될 수 없다.
    ///   그래서 이 SO 가 값을 들고 있다가 <see cref="ToTuning"/> 으로 코어에 넘긴다.
    ///   숫자를 고치려고 코드를 여는 일이 없어야 한다 — 밸런싱은 코드 작업이 아니다.
    ///
    /// ★ 비용·성장 배수가 이 게임의 난이도 손잡이다:
    ///   비용 배수 &gt; 값 배수 여야 뒤로 갈수록 한 번 사는 게 사건이 된다(성취감).
    ///   반대면 금세 숫자가 폭발해 무의미해지고, 차이가 너무 크면 하염없이 기다린다(지루).
    /// </summary>
    [CreateAssetMenu(fileName = "IdleTuning", menuName = "WM/Idle/Tuning")]
    public sealed class IdleTuningSO : ScriptableObject
    {
        [Header("대상 — 단계마다 커진다")]
        [Tooltip("1단계 대상의 체력.")]
        [SerializeField] private double targetHealth = 10d;

        [Tooltip("단계마다 체력에 곱해지는 배수. 보상 배수보다 커야 «벽»이 생긴다.")]
        [SerializeField] private double targetHealthRatio = 1.55d;

        [Tooltip("1단계 처치 하나당 들어오는 자원.")]
        [SerializeField] private double rewardPerKill = 1d;

        [Tooltip("단계마다 보상에 곱해지는 배수.")]
        [SerializeField] private double rewardRatio = 1.35d;

        [Tooltip("한 단계에서 몇을 처치해야 다음으로 내려가나.")]
        [SerializeField] private int killsPerStage = 10;

        [Header("기본 능력치 (레벨 0)")]
        [SerializeField] private double baseDamage = 1d;
        [SerializeField] private double baseAttackSpeed = 1d;

        [Header("공격력 곡선")]
        [Tooltip("0→1 비용.")]
        [SerializeField] private double damageBaseCost = 10d;

        [Tooltip("레벨마다 비용에 곱해지는 배수.")]
        [SerializeField] private double damageCostRatio = 1.22d;

        [Tooltip("레벨 하나가 주는 공격력의 첫 값.")]
        [SerializeField] private double damageBaseValue = 1d;

        [Tooltip("레벨마다 효과에 곱해지는 배수. 1 이면 매 레벨 같은 양이 더해진다.")]
        [SerializeField] private double damageValueRatio = 1.15d;

        [Header("공격속도 곡선")]
        [SerializeField] private double speedBaseCost = 25d;
        [SerializeField] private double speedCostRatio = 1.28d;
        [SerializeField] private double speedBaseValue = 0.5d;
        [SerializeField] private double speedValueRatio = 1.12d;

        [Header("손 때리기")]
        [Tooltip("한 번 두드리면 <공격 몇 초치>가 즉시 들어가나.")]
        [SerializeField] private double tapSecondsOfAttack = 0.2d;

        [Header("지나가는 것 · 폭주")]
        [Tooltip("이 시간 전에는 안 뜬다.")]
        [SerializeField] private double visitorEarliestSeconds = 90d;

        [Tooltip("이쯤이면 거의 확실히 뜬다 (5제곱 램프의 끝).")]
        [SerializeField] private double visitorLatestSeconds = 300d;

        [Tooltip("떠 있는 시간 — 짧을수록 잡는 것이 사건이 된다.")]
        [SerializeField] private double visitorStaySeconds = 13d;

        [Tooltip("잡았을 때 폭주가 지속되는 시간.")]
        [SerializeField] private double surgeSeconds = 30d;

        [Tooltip("폭주 — 판 전체(공격속도·기지 생산)에 걸리는 배수.")]
        [SerializeField] private double frenzyMultiplier = 7d;

        [Tooltip("잡았을 때 손폭주가 걸릴 확률.")]
        [SerializeField] private double handFrenzyChance = 0.2d;

        [Tooltip("손폭주 — 손으로 때리는 값에만 걸리는 배수.")]
        [SerializeField] private double handFrenzyMultiplier = 50d;

        [Header("뽑기")]
        [Tooltip("첫 뽑기의 자원 값.")]
        [SerializeField] private double pullCostBase = 250d;

        [Tooltip("뽑을수록 값에 곱해지는 배수.")]
        [SerializeField] private double pullCostRatio = 1.15d;

        [Tooltip("한 번 뽑는 데 드는 환생석 (자원과 «둘 다» 낸다).")]
        [SerializeField] private long pullStoneCost = 1L;

        [Tooltip("레전드 확률. 화면에 그대로 적히므로 여기와 표시가 갈리면 안 된다.")]
        [SerializeField] private double legendChance = 0.02d;

        [Tooltip("에픽 확률.")]
        [SerializeField] private double epicChance = 0.10d;

        [Tooltip("레어 확률. 나머지가 일반.")]
        [SerializeField] private double rareChance = 0.28d;

        [Tooltip("천장 — 이 횟수 안에 레전드를 보장한다.")]
        [SerializeField] private int pityPulls = 60;

        [Header("영웅 — 중복·보유·도감")]
        [Tooltip("★ 상한. 넘겨도 중복은 안 버린다.")]
        [SerializeField] private int maxStars = 5;

        [Tooltip("다음 ★ 까지 필요한 중복의 기본 개수 (★ 마다 늘어난다).")]
        [SerializeField] private int copiesPerStar = 2;

        [Tooltip("★ 하나가 더하는 몫.")]
        [SerializeField] private double heroStarStep = 0.10d;

        [Tooltip("«가지고만 있어도» 붙는 몫 (등급 무게에 곱해진다).")]
        [SerializeField] private double heroOwnedShareByGrade = 0.03d;

        [Tooltip("메인 칸에 «내보내야» 붙는 몫. 보유보다 커야 누구를 낼지가 결정이 된다.")]
        [SerializeField] private double heroPartyShareByGrade = 0.12d;

        [Tooltip("보조 칸(전장 불참)에 넣었을 때 붙는 몫. 메인보다 작고 보유보다 커야 한다.")]
        [SerializeField] private double heroSupportShareByGrade = 0.06d;

        [Tooltip("도감 점수 몇마다 한 계단 오르나.")]
        [SerializeField] private int codexStepScore = 5;

        [Tooltip("한 계단이 주는 판 전체 배수 (뿌리 둘에서 «한 번»만 걸린다).")]
        [SerializeField] private double codexStepBonus = 0.15d;

        [Header("기지")]
        [Tooltip("생산자 종류 수.")]
        [SerializeField] private int producerCount = 8;

        [Tooltip("하나 살 때마다 값에 곱해지는 배수 (생산자 클리커 계열와 같은 자리).")]
        [SerializeField] private double producerCostRatio = 1.15d;

        [Tooltip("«몰아 사기/올리기» 한 번에 처리하는 최대 개수 — 크면 한 번 누르는 데 판이 멎는다.")]
        [SerializeField] private int bulkBuyMost = 50;

        [Header("장비 — 가방·합치기·감정")]
        [Tooltip("가방 칸 수. 차면 새 장비가 안 들어온다(감정용 개수는 계속 쌓인다).")]
        [SerializeField] private int bagCapacity = 40;

        [Tooltip("합치는 데 필요한 같은 부위·같은 등급 개수.")]
        [SerializeField] private int mergeCount = 3;

        [Tooltip("장비 등급 하나가 그 부위에 더하는 몫.")]
        [SerializeField] private double gearTierBonus = 0.15d;

        [Tooltip("1등급 감정 값.")]
        [SerializeField] private double appraiseBaseCost = 50d;

        [Tooltip("등급마다 감정 값에 곱해지는 배수.")]
        [SerializeField] private double appraiseCostRatio = 4d;

        [Tooltip("합치기 값 = 그 등급 감정 값 x 이 값.")]
        [SerializeField] private double mergeCostFactor = 0.5d;

        [Tooltip("잠재 등급의 최고값 = 최저값 x 이 값.")]
        [SerializeField] private double potentialSpread = 2d;

        [Header("떨구기 — 깊이가 등급의 관문")]
        [Tooltip("첫 판의 등급 천장.")]
        [SerializeField] private int baseMaxTier = 6;

        [Tooltip("환생 한 번마다 천장이 몇 오르나.")]
        [SerializeField] private int tiersPerAscension = 2;

        [Tooltip("몇 단계마다 등급이 하나씩 열리나.")]
        [SerializeField] private int stagesPerTier = 5;

        [Tooltip("처치 하나가 떨구는 기대 개수.")]
        [SerializeField] private double dropsPerKill = 0.25d;

        [Tooltip("한 등급 위로 갈 때 나올 몫에 곱해지는 비율 (합은 늘 1).")]
        [SerializeField] private double tierRarity = 0.4d;

        [Header("환생")]
        [Tooltip("여기부터 환생 점수가 붙는다.")]
        [SerializeField] private int prestigeMinStage = 10;

        [Tooltip("«가장 깊이 간» 단계 하나당 점수.")]
        [SerializeField] private double prestigePointsPerStage = 1d;

        [Tooltip("점수 하나가 곱하는 배수 — 이 게임에서 가장 센 손잡이다.")]
        [SerializeField] private double prestigeMultiplierPerPoint = 1.55d;

        [Header("기지 곡선 — 경제의 뼈대")]
        [Tooltip("첫 생산자의 값.")]
        [SerializeField] private double producerCost = 15d;

        [Tooltip("다음 종류로 갈 때 값에 곱해지는 배수 (종류 사이의 간격).")]
        [SerializeField] private double producerCostStep = 10d;

        [Tooltip("첫 생산자가 내는 초당 자원.")]
        [SerializeField] private double producerOutput = 0.5d;

        [Tooltip("다음 종류로 갈 때 산출에 곱해지는 배수. 값 배수보다 «작아야» 뒤 종류가 사치가 된다.")]
        [SerializeField] private double producerOutputStep = 8d;

        [Header("잠재 — 등급별 범위")]
        [Tooltip("가장 낮은 등급(레어)의 최저값.")]
        [SerializeField] private double potentialFloor = 0.02d;

        [Tooltip("등급이 하나 오를 때 최저값에 곱해지는 배수.")]
        [SerializeField] private double potentialStep = 2.2d;

        [Header("카드 · 코스트 (V2)")]
        [Tooltip("코스트 게이지 상한 (자동전투+카드 개입 계열 = 10칸).")]
        [SerializeField] private double costMax = 10d;

        [Tooltip("초당 차는 코스트 — 자동전투+카드 개입 계열 실측 기준 0.4 (1칸 ≈ 2.4초).")]
        [SerializeField] private double costPerSecond = 0.4d;

        [Tooltip("일제 사격의 코스트.")]
        [SerializeField] private double volleyCost = 3d;

        [Tooltip("일제 사격이 즉시 몰아치는 «자동 공격 몇 초치»인가.")]
        [SerializeField] private double volleySecondsOfAttack = 20d;

        [Tooltip("긴급 보급의 코스트.")]
        [SerializeField] private double supplyCost = 2d;

        [Tooltip("긴급 보급이 걸려 있는 시간 (초).")]
        [SerializeField] private double supplySeconds = 30d;

        [Tooltip("걸려 있는 동안 기지 수입에 곱하는 배수.")]
        [SerializeField] private double supplyMultiplier = 3d;

        [Tooltip("비밀 감정의 코스트 — 자원 대신 코스트로 한 번 굴린다.")]
        [SerializeField] private double appraiseCardCost = 5d;

        [Header("부대 — 맞고 쓰러지고 일어난다 (V2)")]
        [Tooltip("한 자리의 기본 체력 (장비·환생·영웅 등급이 곱해진다).")]
        [SerializeField] private double seatBaseHealth = 400d;

        [Tooltip("1구역 적이 초당 넣는 피해.")]
        [SerializeField] private double enemyDamage = 0.35d;

        [Tooltip("구역마다 적 피해에 곱해지는 배수 — 체력 곡선보다 완만해야 성장이 산다.")]
        [SerializeField] private double enemyDamageRatio = 1.18d;

        [Tooltip("쓰러진 자리가 다시 일어나는 시간 (초). 하나라도 서 있어야 돈다.")]
        [SerializeField] private double reviveSeconds = 12d;

        [Tooltip("하나 잡을 때마다 최대 체력의 몇 할을 회복하나 — 벽의 위치를 정한다.")]
        [SerializeField] private double healPerKillShare = 0.08d;

        [Tooltip("영웅 등급 한 계단이 체력에 더하는 몫.")]
        [SerializeField] private double heroGradeHealthStep = 0.35d;

        [Header("자리 비움")]
        [Tooltip("기본 오프라인 상한 (초).")]
        [SerializeField] private double baseMaxOfflineSeconds = 8d * 3600d;

        [Tooltip("환생 한 번이 늘려 주는 상한 (초).")]
        [SerializeField] private double offlineSecondsPerAscension = 2d * 3600d;

        [Tooltip("아무리 늘려도 여기까지 (초).")]
        [SerializeField] private double maxOfflineCapSeconds = 24d * 3600d;

        /// <summary>코어가 쓰는 형태로 넘긴다.</summary>
        public IdleTuning ToTuning()
        {
            return new IdleTuning
            {
                TargetHealthByStage = new GeometricScale(targetHealth, targetHealthRatio),
                RewardByStage = new GeometricScale(rewardPerKill, rewardRatio),
                KillsPerStage = killsPerStage,
                BaseDamage = baseDamage,
                BaseAttackSpeed = baseAttackSpeed,
                DamageCurve = new GeometricUpgradeCurve
                {
                    BaseCost = damageBaseCost,
                    CostRatio = damageCostRatio,
                    BaseValue = damageBaseValue,
                    ValueRatio = damageValueRatio,
                },
                AttackSpeedCurve = new GeometricUpgradeCurve
                {
                    BaseCost = speedBaseCost,
                    CostRatio = speedCostRatio,
                    BaseValue = speedBaseValue,
                    ValueRatio = speedValueRatio,
                },
                TapSecondsOfAttack = tapSecondsOfAttack,
                VisitorEarliestSeconds = visitorEarliestSeconds,
                VisitorLatestSeconds = visitorLatestSeconds,
                VisitorStaySeconds = visitorStaySeconds,
                SurgeSeconds = surgeSeconds,
                FrenzyMultiplier = frenzyMultiplier,
                HandFrenzyChance = handFrenzyChance,
                HandFrenzyMultiplier = handFrenzyMultiplier,
                PullCostBase = pullCostBase,
                PullCostRatio = pullCostRatio,
                PullStoneCost = pullStoneCost,
                LegendChance = legendChance,
                EpicChance = epicChance,
                RareChance = rareChance,
                PityPulls = pityPulls,
                MaxStars = maxStars,
                CopiesPerStar = copiesPerStar,
                HeroStarStep = heroStarStep,
                HeroOwnedShareByGrade = heroOwnedShareByGrade,
                HeroPartyShareByGrade = heroPartyShareByGrade,
                HeroSupportShareByGrade = heroSupportShareByGrade,
                CodexStepScore = codexStepScore,
                CodexStepBonus = codexStepBonus,
                ProducerCount = producerCount,
                ProducerCostRatio = producerCostRatio,
                BulkBuyMost = bulkBuyMost,
                BagCapacity = bagCapacity,
                MergeCount = mergeCount,
                GearTierBonus = gearTierBonus,
                AppraiseBaseCost = appraiseBaseCost,
                AppraiseCostRatio = appraiseCostRatio,
                MergeCostFactor = mergeCostFactor,
                PotentialSpread = potentialSpread,
                BaseMaxTier = baseMaxTier,
                TiersPerAscension = tiersPerAscension,
                StagesPerTier = stagesPerTier,
                DropsPerKill = dropsPerKill,
                TierRarity = tierRarity,
                PrestigeMinStage = prestigeMinStage,
                PrestigePointsPerStage = prestigePointsPerStage,
                PrestigeMultiplierPerPoint = prestigeMultiplierPerPoint,
                BaseMaxOfflineSeconds = baseMaxOfflineSeconds,
                OfflineSecondsPerAscension = offlineSecondsPerAscension,
                MaxOfflineCapSeconds = maxOfflineCapSeconds,
                CostMax = costMax,
                CostPerSecond = costPerSecond,
                VolleyCost = volleyCost,
                VolleySecondsOfAttack = volleySecondsOfAttack,
                SupplyCost = supplyCost,
                SupplySeconds = supplySeconds,
                SupplyMultiplier = supplyMultiplier,
                AppraiseCardCost = appraiseCardCost,
                SeatBaseHealth = seatBaseHealth,
                EnemyDamageByStage = new GeometricScale(enemyDamage, enemyDamageRatio),
                ReviveSeconds = reviveSeconds,
                HealPerKillShare = healPerKillShare,
                HeroGradeHealthStep = heroGradeHealthStep,
                ProducerCostByKind = new GeometricScale(producerCost, producerCostStep),
                ProducerOutputByKind = new GeometricScale(producerOutput, producerOutputStep),
                PotentialByGrade = new GeometricScale(potentialFloor, potentialStep),
            };
        }
    }
}
