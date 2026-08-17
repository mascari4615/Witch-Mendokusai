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
        /// <summary>이번 대상을 이미 때린 횟수 — 스텝 불변의 근거다.</summary>
        public long HitsOnTarget { get; set; }

        /// <summary>아직 한 번을 못 채운 공격 — 이걸 들고 가야 쪼개 밟아도 결과가 같다.</summary>
        public double AttackProgress { get; set; }
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

        /// <summary>
        /// 여기 <b>머문다</b> — 단계를 다 밀어도 안 내려간다.
        ///
        /// ★ 이 게임의 <b>첫 번째 진짜 선택</b>이다(TASK 의 「반복 결정 셋」 중 ① 어디서 사냥할까).
        ///   수치상 갈등은 이미 있었다 — 얕으면 빨리 잡아 <b>많이</b> 떨구고,
        ///   깊으면 느리지만 <b>좋은 것</b>이 떨어진다(등급 상한이 깊이에 걸려 있으니까).
        ///   여태는 그 갈등을 코어가 혼자 결정했다. 이제 사람이 정한다.
        /// </summary>
        public bool HoldingStage { get; set; }

        /// <summary>여태 모은 리셋 점수 — 리셋해도 안 사라진다. 이게 「다시 시작」을 보상으로 만드는 것.</summary>
        public long PrestigePoints { get; set; }

        /// <summary>몇 번 리셋했나.</summary>
        public int Ascensions { get; set; }

        /// <summary>등급별로 여태 떨어진 개수 (0번째 = 1등급).</summary>
        public long[] DroppedByTier { get; private set; } = new long[0];

        /// <summary>
        /// 생산자 종류별 보유 수 — 기지가 내는 자원의 근거.
        ///
        /// ★ <b>첫 하나는 쥐여 준다</b>. 자원이 0 이면 아무것도 못 사고, 아무것도 못 사면
        ///   자원이 안 늘어 <b>게임이 시작되지 않는다</b>. 쿠키 클리커가 첫 클릭을 주는 자리와 같다.
        /// </summary>
        public long[] Owned { get; private set; } = new long[] { 1L };

        /// <summary>가방 — 모험이 가져온 장비.</summary>
        public System.Collections.Generic.List<IdleItem> Bag { get; private set; }
            = new System.Collections.Generic.List<IdleItem>();

        /// <summary>부위마다 차고 있는 것 (빈 자리는 등급 0).</summary>
        public IdleItem[] Worn { get; private set; } = new IdleItem[IdleGear.SLOT_COUNT];

        /// <summary>떨어진 순번 — 부위를 돌려 주는 데 쓴다(무작위 X, 결정적).</summary>
        public long DropSequence { get; set; }

        /// <summary>뽑아서 가진 영웅들 (TASK-WM-406).</summary>
        public System.Collections.Generic.List<IdleHeroOwned> Heroes { get; private set; }
            = new System.Collections.Generic.List<IdleHeroOwned>();

        /// <summary>
        /// 내보낸 셋 — 각 자리에 영웅 <see cref="IdleHeroKind.Id"/>, 빈 자리는 -1.
        ///
        /// ★ <b>보유</b>와 <b>출전</b>을 나눈 자리다. 안 나눴으면 전원 참전이 늘 정답이라
        ///   「누구를 내보낼까」가 결정이 아니게 된다.
        /// </summary>
        public int[] Party { get; private set; } = new int[] { -1, -1, -1 };

        /// <summary>천장까지 남은 셈 — 마지막 최고등급 이후 몇 번 뽑았나.</summary>
        public int PullsSincePity { get; set; }

        /// <summary>지나가는 것이 떠 있는 남은 시간(초). 0 이면 지금은 없다.</summary>
        public double VisitorSecondsLeft { get; set; }

        /// <summary>마지막으로 뜬 뒤 흐른 시간(초) — 기다린 만큼 잘 뜬다.</summary>
        public double SinceVisitorSeconds { get; set; }

        /// <summary>지금 걸린 폭주 (<see cref="IdleSurgeKind"/>).</summary>
        public int SurgeKind { get; set; }

        /// <summary>그 폭주가 남은 시간(초).</summary>
        public double SurgeSecondsLeft { get; set; }

        /// <summary>
        /// 쓸 수 있는 <b>환생석</b> — 뽑기에 낸다 (TASK-WM-406).
        ///
        /// ★ <b>배수와 갈라 둔다.</b> 배수는 <see cref="PrestigePoints"/>(여태 가장 깊이 간 자리)가
        ///   정하고, 이건 <b>쓰면 준다</b>. 하나로 겸했더니 뽑을수록 손해였다 —
        ///   실측 이레: 안 뽑음 1619단계 vs 다 뽑음 104단계.
        ///   한 재화가 <b>지수 성장</b>과 <b>일회성 소비</b>를 겸하면 소비 쪽은 늘 진다.
        ///   쿠키 클리커가 명성을 <b>누적</b> 쿠키로 매기는 것과 같은 수법이다.
        /// </summary>
        public long Stones { get; set; }

        /// <summary>
        /// 여태 뽑은 총 횟수 — <b>값이 여기를 따라 오른다</b>.
        ///
        /// ★ 환생해도 안 돌아간다. 뽑은 얼굴은 남으니 값도 남아야 앞뒤가 맞는다.
        /// </summary>
        public long PullsDone { get; set; }

        /// <summary>가진 영웅이 목록의 몇 번째인가. 없으면 -1.</summary>
        public int IndexOfHero(int id)
        {
            for (int index = 0; index < Heroes.Count; index++)
            {
                if (Heroes[index].Id == id)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>생산자 칸을 넉넉히 잡아 둔다 — 늘리기만 한다.</summary>
        public void EnsureProducerRoom(int count)
        {
            if (Owned.Length >= count)
            {
                return;
            }

            long[] grown = new long[count];
            for (int i = 0; i < Owned.Length; i++)
            {
                grown[i] = Owned[i];
            }

            Owned = grown;
        }

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
                HitsOnTarget = HitsOnTarget,
                AttackProgress = AttackProgress,
                Stage = Stage,
                KillsInStage = KillsInStage,
                BestStage = BestStage,
                HoldingStage = HoldingStage,
                PrestigePoints = PrestigePoints,
                Ascensions = Ascensions,
                Owned = (long[])Owned.Clone(),
                BagItems = Bag.ToArray(),
                WornItems = (IdleItem[])Worn.Clone(),
                DropSequence = DropSequence,
                Heroes = Heroes.ToArray(),
                Party = (int[])Party.Clone(),
                PullsSincePity = PullsSincePity,
                PullsDone = PullsDone,
                Stones = Stones,
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
            // 옛 저장에는 「넣은 피해」가 있었다. 지금은 「때린 횟수」로 센다 — 옛 값은 버린다
            // (대상 하나만큼의 진행이라 잃어도 체감이 없다).
            HitsOnTarget = saveData.HitsOnTarget;
            AttackProgress = saveData.AttackProgress;
            // ★ 옛 저장에는 단계 칸이 없어 0 이 들어온다 — 그대로 두면 0단계가 되어 판이 어긋난다.
            //   저장 형식이 늘어날 때마다 「없던 시절의 값」을 여기서 메운다.
            Stage = saveData.Stage > 0 ? saveData.Stage : 1;
            KillsInStage = saveData.KillsInStage;
            BestStage = saveData.BestStage > 0 ? saveData.BestStage : Stage;
            HoldingStage = saveData.HoldingStage;
            PrestigePoints = saveData.PrestigePoints;
            Ascensions = saveData.Ascensions;
            // 옛 저장에는 기지·가방이 없어 null 로 온다 — 빈 것으로 받는다.
            Owned = saveData.Owned ?? new long[0];
            Bag = saveData.BagItems != null
                ? new System.Collections.Generic.List<IdleItem>(saveData.BagItems)
                : new System.Collections.Generic.List<IdleItem>();
            Worn = saveData.WornItems != null && saveData.WornItems.Length == IdleGear.SLOT_COUNT
                ? (IdleItem[])saveData.WornItems.Clone()
                : new IdleItem[IdleGear.SLOT_COUNT];
            DropSequence = saveData.DropSequence;
            // 옛 저장에는 영웅이 없다 — 빈 도감·빈 파티로 받는다(터지지 않는다).
            //
            // ⚠ <b>모르는 번호는 버린다</b>. 저장은 <b>바깥에서 온 글자</b>다 — 사람이 고칠 수도
            //   있고, 명단이 바뀌면 옛 저장에 없는 얼굴이 남는다. 그대로 받으면
            //   IdleHeroes.KindOf 가 배열 밖을 짚어 <b>매 프레임</b> 터진다(화면이 통째로 죽는다).
            //   경계에서 거르는 것은 증상 덮기가 아니라 바깥 입력을 다루는 자리의 일이다.
            Heroes = new System.Collections.Generic.List<IdleHeroOwned>();

            if (saveData.Heroes != null)
            {
                for (int index = 0; index < saveData.Heroes.Length; index++)
                {
                    if (IdleHeroes.Knows(saveData.Heroes[index].Id))
                    {
                        Heroes.Add(saveData.Heroes[index]);
                    }
                }
            }

            Party = new int[] { -1, -1, -1 };

            if (saveData.Party != null && saveData.Party.Length == Party.Length)
            {
                for (int seat = 0; seat < Party.Length; seat++)
                {
                    int id = saveData.Party[seat];

                    // 자리에 앉은 얼굴도 <b>가진 얼굴</b>이어야 한다 — 버린 영웅이 서 있으면 안 된다.
                    Party[seat] = IdleHeroes.Knows(id) && IndexOfHero(id) >= 0 ? id : -1;
                }
            }
            PullsSincePity = saveData.PullsSincePity;
            PullsDone = saveData.PullsDone;
            Stones = saveData.Stones;
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
