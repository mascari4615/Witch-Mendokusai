using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>
    /// 지금 판의 상태 — 모은 자원, 축별 레벨, 그리고 아직 안 끝난 타격의 잔여.
    ///
    /// ★ double 을 쓴다 — 방치형은 곧 float 정밀도를 넘긴다(1e7 부터 1 단위가 사라진다).
    /// ★ 잔여 피해까지 상태다 — 이걸 안 들고 있으면 짧은 스텝을 여러 번 밟을 때 피해가 매번 버려져
    ///   같은 시간을 재도 스텝 크기에 따라 결과가 달라진다. 저장·오프라인 보상이 그 위에 서므로 치명적이다.
    /// </summary>
    public sealed class IdleState : ISavable<IdleSaveData>
    {
        /// <summary>모은 자원.</summary>
        public double Resource { get; set; }

        /// <summary>지금까지 처치한 대상 수 — 진행감·통계용.</summary>
        public long Kills { get; set; }

        /// <summary>지금 대상에게 이미 넣어 둔 피해.</summary>
        public double DamageDealtToTarget { get; set; }
        /// <summary>지금 내려와 있는 단계 (1부터).</summary>
        public int Stage { get; set; } = 1;

        /// <summary>이번 단계에서 처치한 수 — 이게 <see cref="IdleTuning.KillsPerStage"/> 에 닿으면 내려간다.</summary>
        public int KillsInStage { get; set; }

        /// <summary>
        /// 여태 닿아 본 가장 깊은 단계.
        ///
        /// ★ 지금은 안 쓴다 — <b>나중에 쓸 자리를 지금 저장에 만들어 둔다.</b> 울티마 스쿼드에서
        ///   「스테이지마다 나올 수 있는 장비 단계의 상한」이 이 값에 걸린다. 저장 형식은 나중에
        ///   바꾸기가 가장 비싼 물건이라, 확실히 올 칸은 미리 판다.
        /// </summary>
        public int BestStage { get; set; } = 1;

        /// <summary>여태 모은 리셋 점수 — 리셋해도 안 사라진다. 이게 「다시 시작」을 보상으로 만드는 것.</summary>
        public long PrestigePoints { get; set; }

        /// <summary>몇 번 리셋했나.</summary>
        public int Ascensions { get; set; }

        /// <summary>등급별로 여태 떨어진 개수 (0번째 = 1등급).</summary>
        public long[] DroppedByTier { get; private set; } = new long[0];

        /// <summary>주사위의 지금 상태 — 저장에 실린다. 안 실으면 껐다 켜서 다시 굴리기가 공짜가 된다.</summary>
        public long RandomState { get; set; } = 0x2545F4914F6CDD1DL;

        /// <summary>여태 뽑은 가장 좋은 잠재 값(비율).</summary>
        public double BestPotentialValue { get; set; }

        /// <summary>그 잠재의 등급 (<see cref="PotentialGrade"/>).</summary>
        public int BestPotentialGrade { get; set; }

        /// <summary>등급별 잔여분 — 아직 하나가 안 된 몫. 이걸 들고 가야 쪼개 밟아도 총합이 같다.</summary>
        public double[] DropProgressByTier { get; private set; } = new double[0];

        /// <summary>
        /// 등급 칸을 넉넉히 잡아 둔다. 손잡이(<see cref="IdleTuning.MaxTier"/>)가 커질 수 있어
        /// <b>늘리기만</b> 한다 — 줄이면 이미 떨어진 것이 사라진다.
        /// </summary>
        public void EnsureTierRoom(int tierCount)
        {
            if (tierCount < 1)
            {
                tierCount = 1;
            }

            if (DroppedByTier.Length >= tierCount)
            {
                return;
            }

            long[] grownCounts = new long[tierCount];
            double[] grownProgress = new double[tierCount];

            for (int i = 0; i < DroppedByTier.Length; i++)
            {
                grownCounts[i] = DroppedByTier[i];
                grownProgress[i] = DropProgressByTier[i];
            }

            DroppedByTier = grownCounts;
            DropProgressByTier = grownProgress;
        }

        /// <summary>마지막으로 본 시각 (Unix 초, UTC). 오프라인 보상의 재료.</summary>
        public long LastSeenUnixSeconds { get; set; }

        /// <summary>공격력 레벨.</summary>
        public UpgradeLevel Damage { get; } = new UpgradeLevel();

        /// <summary>공격속도 레벨.</summary>
        public UpgradeLevel AttackSpeed { get; } = new UpgradeLevel();

        /// <summary>한 축의 레벨 상태를 고른다.</summary>
        public UpgradeLevel LevelOf(IdleUpgradeKind kind)
        {
            return kind == IdleUpgradeKind.Damage ? Damage : AttackSpeed;
        }

        /// <summary>저장 꼴로 담는다 — 잔여 피해와 마지막 시각까지 빠짐없이.</summary>
        public IdleSaveData Save()
        {
            return new IdleSaveData
            {
                Resource = Resource,
                Kills = Kills,
                DamageDealtToTarget = DamageDealtToTarget,
                Stage = Stage,
                KillsInStage = KillsInStage,
                BestStage = BestStage,
                PrestigePoints = PrestigePoints,
                Ascensions = Ascensions,
                DroppedByTier = (long[])DroppedByTier.Clone(),
                DropProgressByTier = (double[])DropProgressByTier.Clone(),
                RandomState = RandomState,
                BestPotentialValue = BestPotentialValue,
                BestPotentialGrade = BestPotentialGrade,
                DamageLevel = Damage.Level,
                AttackSpeedLevel = AttackSpeed.Level,
                LastSeenUnixSeconds = LastSeenUnixSeconds,
            };
        }

        /// <summary>저장 꼴에서 되살린다.</summary>
        public void Load(IdleSaveData saveData)
        {
            Resource = saveData.Resource;
            Kills = saveData.Kills;
            DamageDealtToTarget = saveData.DamageDealtToTarget;
            // ★ 옛 저장에는 단계 칸이 없어 0 이 들어온다 — 그대로 두면 0단계가 되어 판이 어긋난다.
            //   저장 형식이 늘어날 때마다 「없던 시절의 값」을 여기서 메운다.
            Stage = saveData.Stage > 0 ? saveData.Stage : 1;
            KillsInStage = saveData.KillsInStage;
            BestStage = saveData.BestStage > 0 ? saveData.BestStage : Stage;
            PrestigePoints = saveData.PrestigePoints;
            Ascensions = saveData.Ascensions;
            // 주사위 상태가 0 인 저장(= 옛 저장)은 굴러가지 않는다 — 기본 씨앗을 준다.
            RandomState = saveData.RandomState != 0L ? saveData.RandomState : 0x2545F4914F6CDD1DL;
            BestPotentialValue = saveData.BestPotentialValue;
            BestPotentialGrade = saveData.BestPotentialGrade;
            // 옛 저장에는 등급 칸이 없어 null 로 온다 — 빈 칸으로 받는다.
            DroppedByTier = saveData.DroppedByTier ?? new long[0];
            DropProgressByTier = saveData.DropProgressByTier ?? new double[0];
            if (DropProgressByTier.Length < DroppedByTier.Length)
            {
                double[] grown = new double[DroppedByTier.Length];
                for (int i = 0; i < DropProgressByTier.Length; i++)
                {
                    grown[i] = DropProgressByTier[i];
                }
                DropProgressByTier = grown;
            }
            Damage.Level = saveData.DamageLevel;
            AttackSpeed.Level = saveData.AttackSpeedLevel;
            LastSeenUnixSeconds = saveData.LastSeenUnixSeconds;
        }
    }
}
