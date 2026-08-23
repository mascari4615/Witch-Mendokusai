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

        /// <summary>
        /// 카드 코스트 — 시간이 채우고 카드가 쓴다 (V2, concept-v2).
        ///
        /// ★ 환생해도 <b>안 지운다</b> — 코스트는 판의 세기가 아니라 개입의 리듬이라,
        ///   지우면 환생 직후의 「바로 한 장」이 없어진다.
        /// </summary>
        public double Cost { get; set; }

        /// <summary>긴급 보급이 남은 시간(초) — 걸려 있는 동안 기지 수입이 몇 배가 된다.</summary>
        public double SupplySecondsLeft { get; set; }

        /// <summary>자리별 남은 체력 (0 = 쓰러짐). 0번 = 나, 1~3 = 파티 자리 (V2 부대층).</summary>
        public double[] SeatHealth { get; private set; } = new double[IdleSquad.SEAT_COUNT];

        /// <summary>쓰러진 자리의 부활 게이지(초).</summary>
        public double[] SeatReviveSeconds { get; private set; } = new double[IdleSquad.SEAT_COUNT];

        /// <summary>실패해서 <b>반복</b> 중인가 — 클리어해도 안 내려간다. 사람이 「다음 구역」을 눌러야 푼다.</summary>
        public bool Repeating { get; set; }

        /// <summary>마지막으로 <b>깨고 내려간</b> 구역 — 실패하면 여기로 물러난다.</summary>
        public int ClearedStage { get; set; }

        /// <summary>
        /// 자리 체력을 <b>한 번이라도 세웠나</b>.
        ///
        /// ★ 이게 없으면 「아직 안 세운 판」과 「전멸한 판」이 <b>똑같이 체력 0</b> 이라 구별이 안 된다.
        ///   그래서 사진을 찍기만 해도(조회) 판을 세워야 했고, 그건 「묻는 자리는 판을 안 건드린다」를 깬다.
        /// </summary>
        public bool SeatsReady { get; set; }

        /// <summary>
        /// 자리 칸을 갖추고, <b>새로 온 자리는 만렙 체력</b>으로 세운다.
        ///
        /// ★ 옛 저장·새 판은 체력이 0 이라 그대로 두면 <b>시작하자마자 전멸</b>이다.
        /// ★ 쓰러진 자리(부활 게이지가 돌고 있다)와 <b>새로 앉은 자리</b>를 갈라 본다 —
        ///   안 가르면 부활 대기 중인 영웅이 매 프레임 공짜로 일어난다.
        /// </summary>
        public void EnsureSeatRoom(IdleTuning tuning)
        {
            if (SeatHealth.Length < IdleSquad.SEAT_COUNT)
            {
                SeatHealth = new double[IdleSquad.SEAT_COUNT];
                SeatReviveSeconds = new double[IdleSquad.SEAT_COUNT];
            }

            bool first = SeatsReady == false;
            SeatsReady = true;

            for (int seat = 0; seat < IdleSquad.SEAT_COUNT; seat++)
            {
                if (IdleSquad.SeatTaken(this, seat) == false)
                {
                    SeatHealth[seat] = 0d;
                    SeatReviveSeconds[seat] = 0d;
                    continue;
                }

                // 처음 세우는 판, 또는 <b>새로 앉힌 자리</b>(체력도 게이지도 0)는 만렙으로.
                if (first || (SeatHealth[seat] <= 0d && SeatReviveSeconds[seat] <= 0d))
                {
                    SeatHealth[seat] = IdleSquad.MaxHealthOf(this, tuning, seat);
                }
            }
        }

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
                Cost = Cost,
                SupplySecondsLeft = SupplySecondsLeft,
                SeatHealth = (double[])SeatHealth.Clone(),
                SeatReviveSeconds = (double[])SeatReviveSeconds.Clone(),
                Repeating = Repeating,
                ClearedStage = ClearedStage,
                SeatsReady = SeatsReady,
            };
        }

        /// <summary>저장 꼴에서 되살린다.</summary>
        /// <summary>
        /// 저장에서 온 <b>수</b>를 걸러낸다 — NaN·무한·음수는 0 으로.
        ///
        /// ★ 이게 없으면 가장 고약한 고장이 난다: <b>안 터지는데 판이 죽는다</b>.
        ///   자원이 한 번 NaN 이 되면 모든 견줌이 거짓이 되어 아무것도 살 수 없고,
        ///   화면은 「-」만 띄우며 멀쩡히 돈다. 사람은 왜인지 영영 모른다.
        ///   저장은 바깥에서 온 글자다 — 문 앞에서 본다.
        /// </summary>
        /// <summary>셀 수 있는 것은 음수가 될 수 없다 — 저장에서 온 값이면 특히.</summary>
        private static int NotBelowZero(int value)
        {
            return value < 0 ? 0 : value;
        }

        /// <summary>자리 배열을 제 길이로 받아 낸다 — 없거나 짧으면 새로, 값은 <see cref="Sane"/>.</summary>
        private static double[] SizedSane(double[] saved)
        {
            double[] made = new double[IdleSquad.SEAT_COUNT];

            if (saved == null)
            {
                return made;
            }

            for (int seat = 0; seat < made.Length && seat < saved.Length; seat++)
            {
                made[seat] = Sane(saved[seat]);
            }

            return made;
        }

        private static double Sane(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                return 0d;
            }

            return value;
        }

        public void Load(IdleSaveData saveData)
        {
            Resource = Sane(saveData.Resource);
            Kills = saveData.Kills > 0L ? saveData.Kills : 0L;
            // 옛 저장에는 「넣은 피해」가 있었다. 지금은 「때린 횟수」로 센다 — 옛 값은 버린다
            // (대상 하나만큼의 진행이라 잃어도 체감이 없다).
            HitsOnTarget = saveData.HitsOnTarget;
            AttackProgress = Sane(saveData.AttackProgress);
            // ★ 옛 저장에는 단계 칸이 없어 0 이 들어온다 — 그대로 두면 0단계가 되어 판이 어긋난다.
            //   저장 형식이 늘어날 때마다 「없던 시절의 값」을 여기서 메운다.
            Stage = saveData.Stage > 0 ? saveData.Stage : 1;
            KillsInStage = NotBelowZero(saveData.KillsInStage);
            BestStage = saveData.BestStage > 0 ? saveData.BestStage : Stage;
            HoldingStage = saveData.HoldingStage;
            PrestigePoints = saveData.PrestigePoints;
            Ascensions = saveData.Ascensions;
            // 옛 저장에는 기지·가방이 없어 null 로 온다 — 빈 것으로 받는다.
            Owned = saveData.Owned != null ? (long[])saveData.Owned.Clone() : new long[0];

            // 음수로 가진 생산자는 <b>수입을 깎는다</b> — 자원이 줄어드는 판이 되고, 사람은
            //   「고장」이라고만 느낀다. 개수는 음수가 될 수 없는 값이다.
            for (int kind = 0; kind < Owned.Length; kind++)
            {
                if (Owned[kind] < 0L)
                {
                    Owned[kind] = 0L;
                }
            }
            // ⚠ 장비의 <b>부위 번호</b>도 저장에서 그대로 온다. 범위를 벗어난 값이 섞이면
            //   차는 순간 Worn[그 번호] 가 배열 밖을 짚어 터지고, 화면도 이름표를 짚다 터진다.
            //   영웅 번호와 같은 자리의 같은 병이라 같은 곳에서 거른다 — <b>문 앞</b>.
            Bag = new System.Collections.Generic.List<IdleItem>();

            if (saveData.BagItems != null)
            {
                for (int index = 0; index < saveData.BagItems.Length; index++)
                {
                    if (IdleGear.IsRealSlot(saveData.BagItems[index]))
                    {
                        Bag.Add(saveData.BagItems[index]);
                    }
                }
            }

            Worn = new IdleItem[IdleGear.SLOT_COUNT];

            if (saveData.WornItems != null && saveData.WornItems.Length == IdleGear.SLOT_COUNT)
            {
                for (int slot = 0; slot < Worn.Length; slot++)
                {
                    IdleItem one = saveData.WornItems[slot];

                    // 차고 있던 것은 <b>그 자리의 부위</b>여야 한다 — 아니면 빈 자리로 받는다.
                    Worn[slot] = IdleGear.IsRealSlot(one) && (int)one.Slot == slot ? one : default;
                }
            }
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
            BestPotentialValue = Sane(saveData.BestPotentialValue);
            BestPotentialGrade = saveData.BestPotentialGrade;
            // 옛 저장에는 등급 칸이 없어 null 로 온다 — 빈 칸으로 받는다.
            DroppedByTier = saveData.DroppedByTier ?? new long[0];
            DropProgressByTier = saveData.DropProgressByTier != null
                ? (double[])saveData.DropProgressByTier.Clone()
                : new double[0];

            for (int slot = 0; slot < DropProgressByTier.Length; slot++)
            {
                DropProgressByTier[slot] = Sane(DropProgressByTier[slot]);
            }
            if (DropProgressByTier.Length < DroppedByTier.Length)
            {
                double[] grown = new double[DroppedByTier.Length];
                for (int i = 0; i < DropProgressByTier.Length; i++)
                {
                    grown[i] = DropProgressByTier[i];
                }
                DropProgressByTier = grown;
            }
            // 레벨·처치 수도 음수가 될 수 없는 값이다. 음수 레벨은 값을 거꾸로 만들고
            // 값을 거꾸로 만들면 <b>올릴수록 약해지는</b> 판이 된다 — 안 터지고 조용히 틀린다.
            Damage.Level = NotBelowZero(saveData.DamageLevel);
            AttackSpeed.Level = NotBelowZero(saveData.AttackSpeedLevel);
            LastSeenUnixSeconds = saveData.LastSeenUnixSeconds;
            // 코스트·보급도 저장에서 온 수다 — NaN·음수는 0. 넘친 코스트는 다음 스텝이 상한으로 누른다.
            Cost = Sane(saveData.Cost);
            SupplySecondsLeft = Sane(saveData.SupplySecondsLeft);
            // 옛 저장에는 자리 칸이 없어 null 로 온다 — 빈 칸으로 받고, EnsureSeatRoom 이 세운다.
            SeatHealth = SizedSane(saveData.SeatHealth);
            SeatReviveSeconds = SizedSane(saveData.SeatReviveSeconds);
            Repeating = saveData.Repeating;
            ClearedStage = NotBelowZero(saveData.ClearedStage);
            SeatsReady = saveData.SeatsReady;
        }
    }
}
