using WitchMendokusai.DomainSDK.Upgrade;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleState.cs 의 Save 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 저장과 불러오기.
    public sealed partial class IdleState
    {
        private void LoadCardDeck(int[] deck)
        {
            CardDeck = deck != null ? (int[])deck.Clone() : new int[0];
            IdleCards.EnsureDeck(this);
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
                CardDeck = (int[])CardDeck.Clone(),
                SeatHealth = (double[])SeatHealth.Clone(),
                SeatReviveSeconds = (double[])SeatReviveSeconds.Clone(),
                Repeating = Repeating,
                ClearedStage = ClearedStage,
                SeatsReady = SeatsReady,
                PrestigeShards = PrestigeShards,
                Tickets = (long[])Tickets.Clone(),
                TicketDay = TicketDay,
                FreeBoxDay = FreeBoxDay,
                SpeedStep = SpeedStep,
                AutoCast = AutoCast,
                BagUpgrades = BagUpgrades,

                MeasuredStage = MeasuredStage,
                MeasuredKillsPerSecond = MeasuredKillsPerSecond,
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

            // 옛 저장은 자리 넷 (0 은 나). 2026-08-30 나 삭제. 한 칸 당겨 받음
            int skip = saved.Length == IdleSquad.SEAT_COUNT + 1 ? 1 : 0;

            for (int seat = 0; seat < made.Length && seat + skip < saved.Length; seat++)
            {
                made[seat] = Sane(saved[seat + skip]);
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

            Worn = new IdleItem[IdleHeroes.Count * IdleGear.SLOT_COUNT];

            if (saveData.WornItems != null)
            {
                // 옛 저장은 4칸(판 공용). 그 시절 장비는 시작 인형 것으로 (2026-08-31 인형별 장비)
                int startAt = saveData.WornItems.Length == IdleGear.SLOT_COUNT
                    ? IdleGear.WornAt(IdleHeroes.STARTER_ID, 0)
                    : 0;

                for (int at = 0; at < saveData.WornItems.Length && startAt + at < Worn.Length; at++)
                {
                    IdleItem one = saveData.WornItems[at];
                    int slot = (startAt + at) % IdleGear.SLOT_COUNT;

                    // 차고 있던 것은 그 자리의 부위여야 함. 아니면 빈 자리로
                    Worn[startAt + at] = IdleGear.IsRealSlot(one) && (int)one.Slot == slot ? one : default;
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
                        IdleHeroOwned owned = saveData.Heroes[index];
                        owned.Level = NotBelowZero(owned.Level);
                        owned.DamageLevel = NotBelowZero(owned.DamageLevel);
                        owned.AttackSpeedLevel = NotBelowZero(owned.AttackSpeedLevel);
                        owned.MaxHealthLevel = NotBelowZero(owned.MaxHealthLevel);
                        owned.DefenseLevel = NotBelowZero(owned.DefenseLevel);
                        owned.CriticalChanceLevel = NotBelowZero(owned.CriticalChanceLevel);
                        owned.CriticalDamageLevel = NotBelowZero(owned.CriticalDamageLevel);
                        owned.RecoveryLevel = NotBelowZero(owned.RecoveryLevel);
                        Heroes.Add(owned);
                    }
                }
            }

            Party = IdleHeroes.EmptyParty();

            if (saveData.Party != null)
            {
                // ★ 옛 저장은 세 칸이다(편성이 여섯 칸이 되기 전, 2026-08-30). 앞부터 이어받으면
                //   그 셋이 그대로 메인 칸 (사람이 앉혀 둔 순서 보존).
                //   더 긴 저장(바깥에서 온 글자): 넘치는 칸 버림.
                int carry = saveData.Party.Length < Party.Length ? saveData.Party.Length : Party.Length;

                for (int slot = 0; slot < carry; slot++)
                {
                    int id = saveData.Party[slot];

                    // 자리에 앉은 얼굴도 <b>가진 얼굴</b>이어야 한다 — 버린 영웅이 서 있으면 안 된다.
                    Party[slot] = IdleHeroes.Knows(id) && IndexOfHero(id) >= 0 ? id : -1;
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
            Damage.Level = 0;
            AttackSpeed.Level = 0;
            LastSeenUnixSeconds = saveData.LastSeenUnixSeconds;
            // 코스트·보급도 저장에서 온 수다 — NaN·음수는 0. 넘친 코스트는 다음 스텝이 상한으로 누른다.
            Cost = Sane(saveData.Cost);
            SupplySecondsLeft = Sane(saveData.SupplySecondsLeft);
            LoadCardDeck(saveData.CardDeck);
            IdleCards.EnsureDeck(this);
            // 옛 저장에는 자리 칸이 없어 null 로 온다 — 빈 칸으로 받고, EnsureSeatRoom 이 세운다.
            SeatHealth = SizedSane(saveData.SeatHealth);
            SeatReviveSeconds = SizedSane(saveData.SeatReviveSeconds);
            Repeating = saveData.Repeating;
            ClearedStage = NotBelowZero(saveData.ClearedStage);
            SeatsReady = saveData.SeatsReady;
            PrestigeShards = saveData.PrestigeShards > 0L ? saveData.PrestigeShards : 0L;
            Tickets = saveData.Tickets;
            TicketDay = saveData.TicketDay;
            FreeBoxDay = saveData.FreeBoxDay;
            SpeedStep = NotBelowZero(saveData.SpeedStep);
            AutoCast = saveData.AutoCast;
            BagUpgrades = NotBelowZero(saveData.BagUpgrades);
            EnsureTicketRoom();

            for (int index = 0; index < Tickets.Length; index++)
            {
                if (Tickets[index] < 0L)
                {
                    Tickets[index] = 0L;
                }
            }

            MeasuredStage = NotBelowZero(saveData.MeasuredStage);
            MeasuredKillsPerSecond = Sane(saveData.MeasuredKillsPerSecond);

            // 자리 0 시절 저장은 인형 0명 가능. 시작 인형 지급
            IdleHeroes.EnsureStarter(this);

            // 옛 판의 공용 공격력과 공격속도는 시작 인형에게 한 번 이관
            int starter = IndexOfHero(IdleHeroes.STARTER_ID);
            if (starter >= 0 && (saveData.DamageLevel > 0 || saveData.AttackSpeedLevel > 0))
            {
                IdleHeroOwned owned = Heroes[starter];
                owned.DamageLevel += NotBelowZero(saveData.DamageLevel);
                owned.AttackSpeedLevel += NotBelowZero(saveData.AttackSpeedLevel);
                Heroes[starter] = owned;
            }
        }
    }
}

