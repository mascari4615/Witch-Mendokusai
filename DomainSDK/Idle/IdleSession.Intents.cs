using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    // IdleSession.cs 의 Intents 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 의도를 받아 코어에 넘기는 쪽.
    public sealed partial class IdleSession
    {
        /// <summary>의도를 받는다 — 받아들여졌으면 true. 자원이 모자라거나 상한이면 아무 일도 없다.</summary>
        public bool Send(IdleRaiseUpgradeIntent intent)
        {
            return IdleModel.TryRaise(state, tuning, intent.HeroId, intent.Kind, intent.Amount);
        }

        /// <summary>배속을 다음 자리로. 끝에서 처음으로</summary>
        public void CycleSpeed()
        {
            double[] steps = tuning.SpeedSteps;
            int count = steps == null || steps.Length == 0 ? 1 : steps.Length;
            state.SpeedStep = (state.SpeedStep + 1) % count;
        }

        /// <summary>설정 화면에서 배속 단계를 직접 고른다.</summary>
        public void SetSpeedStep(int step)
        {
            double[] steps = tuning.SpeedSteps;
            int count = steps == null || steps.Length == 0 ? 1 : steps.Length;
            state.SpeedStep = step < 0 ? 0 : step >= count ? count - 1 : step;
        }

        /// <summary>자동 시전 켜고 끄기</summary>
        public void ToggleAutoCast()
        {
            state.AutoCast = state.AutoCast == false;
        }

        /// <summary>가방을 한 묶음 넓힌다 (상점). 골드가 모자라거나 상한이면 아무 일도 없다</summary>
        public bool BuyBagUpgrade()
        {
            return IdleShop.TryBuyBag(state, tuning);
        }

        /// <summary>인형 레벨을 한 칸 올린다 (economy.md 표 3). 골드가 모자라면 아무 일도 없다</summary>
        public bool RaiseHeroLevel(int heroId)
        {
            return IdleHeroes.TryRaiseLevel(state, tuning, heroId);
        }

        /// <summary>
        /// 손으로 한 대. <b>늘 받아들여진다</b> — 모을 것이 필요 없는 유일한 행동이다.
        /// </summary>
        public bool Send(IdleTapIntent intent)
        {
            IdleModel.Tap(state, tuning);
            return true;
        }

        /// <summary>영웅을 한 번 뽑는다. 자원이 모자라면 아무 일도 안 일어난다.</summary>
        public bool TryPull(out IdleHeroPull pull)
        {
            return IdleGacha.TryPull(state, tuning, PickupNow(), out pull);
        }

        /// <summary>영웅을 한 번 뽑는다 (결과가 필요 없을 때).</summary>
        public bool Send(IdlePullHeroIntent intent)
        {
            return IdleGacha.TryPull(state, tuning, PickupNow(), out IdleHeroPull _);
        }

        /// <summary>묶음으로 뽑는다. 결과는 <paramref name="into"/> 에 순서대로</summary>
        public bool TryPullBatch(System.Collections.Generic.List<IdleHeroPull> into)
        {
            return IdleGacha.TryPullBatch(state, tuning, PickupNow(), into);
        }

        public bool Send(IdlePullBatchIntent intent)
        {
            return IdleGacha.TryPullBatch(state, tuning, PickupNow(), new System.Collections.Generic.List<IdleHeroPull>());
        }

        /// <summary>던전 한 판. 받은 것을 돌려준다</summary>
        public bool TryEnterDungeon(IdleDungeonKind kind, out IdleDungeonReward reward)
        {
            return IdleDungeons.TryEnter(state, tuning, kind, out reward);
        }

        /// <summary>남은 입장권을 한 번에 (소탕)</summary>
        public bool TrySweepDungeon(IdleDungeonKind kind, out IdleDungeonReward reward)
        {
            return IdleDungeons.TrySweep(state, tuning, kind, out reward);
        }

        public bool Send(IdleEnterDungeonIntent intent)
        {
            return IdleDungeons.TryEnter(state, tuning, intent.Kind, out IdleDungeonReward _);
        }

        public bool Send(IdleSweepDungeonIntent intent)
        {
            return IdleDungeons.TrySweep(state, tuning, intent.Kind, out IdleDungeonReward _);
        }

        /// <summary>무료 상자를 연다. 받은 뽑기 재화를 돌려준다</summary>
        public bool TryOpenFreeBox(out long stones)
        {
            return IdleFreeBox.TryOpen(state, tuning, Now(), out stones);
        }

        public bool Send(IdleOpenFreeBoxIntent intent)
        {
            return IdleFreeBox.TryOpen(state, tuning, Now(), out long _);
        }

        /// <summary>지금 픽업인 인형. 시계가 정한다</summary>
        private int PickupNow()
        {
            return IdleGacha.PickupHeroOf(tuning, Now());
        }

        /// <summary>
        /// 자리에 영웅을 앉힌다. 그 영웅이 이미 <b>다른 자리</b>에 있으면 둘을 맞바꾼다 —
        /// 같은 얼굴이 두 자리를 먹으면 셋을 고르는 뜻이 사라진다.
        /// </summary>
        public bool Send(IdleSetPartyIntent intent)
        {
            if (intent.Slot < 0 || intent.Slot >= state.Party.Length)
            {
                return false;
            }

            if (intent.HeroId >= 0 && state.IndexOfHero(intent.HeroId) < 0)
            {
                return false;
            }

            // ⚠ 빈 자리는 -1 로 적힌다. 그래서 <b>빼는</b> 요청(-1)에 이 맞바꿈을 그대로 태우면
            //   다른 빈 자리들이 전부 「같은 영웅」으로 잡혀 <b>빼려던 영웅이 두 자리에 복제</b>된다
            //   ([5,-1,-1] 에서 0번을 비우면 [-1,5,5]). 맞바꿈은 <b>진짜 영웅일 때만</b>이다.
            if (intent.HeroId >= 0)
            {
                for (int slot = 0; slot < state.Party.Length; slot++)
                {
                    if (slot != intent.Slot && state.Party[slot] == intent.HeroId)
                    {
                        state.Party[slot] = state.Party[intent.Slot];
                    }
                }
            }

            state.Party[intent.Slot] = intent.HeroId;
            return true;
        }

        /// <summary>생산자를 하나 산다. 자원이 모자라면 아무 일도 안 일어난다.</summary>
        public bool Send(IdleBuyProducerIntent intent)
        {
            return IdleBase.TryBuy(state, tuning, intent.Kind);
        }

        /// <summary>같은 부위·같은 등급 셋을 합친다.</summary>
        public bool Send(IdleMergeIntent intent)
        {
            return IdleGear.TryMerge(state, tuning, intent.Tier, intent.Slot, out IdleItem _);
        }

        public bool Send(IdleSalvageIntent intent)
        {
            return IdleGear.TrySalvage(state, tuning, intent.Tier, intent.Count, out int _, out double _);
        }

        /// <summary>분해 결과를 돌려주는 길. 화면이 「n개 분해, 골드 +g」를 말한다</summary>
        public bool TrySalvage(int tier, int count, out int salvaged, out double gold)
        {
            return IdleGear.TrySalvage(state, tuning, tier, count, out salvaged, out gold);
        }

        /// <summary>분해 미리보기. 몇 개가 되고 골드가 얼마인지</summary>
        public void ViewSalvage(int tier, int count, out int available, out double gold)
        {
            available = IdleGear.CountSalvageable(state, tier);
            int would = count > 0 && count < available ? count : available;
            gold = IdleGear.SalvageGold(tier, tuning) * would;
        }

        public bool Send(IdleLockItemIntent intent)
        {
            return IdleGear.TrySetLocked(state, intent.BagIndex, intent.Locked);
        }

        public bool Send(IdleSortBagIntent intent)
        {
            IdleGear.SortBag(state);
            return true;
        }

        /// <summary>가방의 것을 찬다.</summary>
        public bool Send(IdleEquipIntent intent)
        {
            return IdleGear.TryEquip(state, intent.HeroId, intent.BagIndex);
        }

        /// <summary>
        /// 지나온 자리로 옮긴다. 앞질러 가려 하면 아무 일도 안 일어난다.
        ///
        /// ★ 옮기면 <b>부대가 회복한다</b> — 물러나는 것이 재정비가 아니면 물러날 이유가 없다.
        /// </summary>
        public bool Send(IdleGoToStageIntent intent)
        {
            if (IdleModel.TryGoToStage(state, intent.Stage) == false)
            {
                return false;
            }

            IdleSquad.HealAll(state, tuning);
            return true;
        }

        /// <summary>반복을 끝내고 다음 구역에 다시 도전한다 (V2 방향 6).</summary>
        public bool Send(IdleNextStageIntent intent)
        {
            return IdleSquad.TryAdvanceStage(state, tuning);
        }

        /// <summary>여기 머물지 정한다. 언제든 뒤집을 수 있다 — 되돌릴 수 없는 선택이면 아무도 안 누른다.</summary>
        public bool Send(IdleHoldStageIntent intent)
        {
            state.HoldingStage = intent.Hold;
            return true;
        }

        /// <summary>카드 한 장을 낸다. 코스트가 모자라면 아무 일도 안 일어난다 (V2).</summary>
        public bool Send(IdleCastCardIntent intent)
        {
            return IdleCards.TryCastHand(state, tuning, intent.HandIndex, out IdleCardResult _);
        }

        /// <summary>카드를 내고 <b>무슨 일이 났는지</b>까지 돌려준다 — 감정 카드의 굴림을 화면이 보여주게.</summary>
        public bool TryCastCard(int handIndex, out IdleCardResult result)
        {
            return IdleCards.TryCastHand(state, tuning, handIndex, out result);
        }

        public bool TryCastCardAt(int handIndex, long foeIndex, out IdleCardResult result)
        {
            return IdleCards.TryCastHandAt(state, tuning, handIndex, foeIndex, out result);
        }

        /// <summary>떨어진 것 하나를 감정한다. 그 등급이 없으면 아무 일도 안 일어난다.</summary>
        public bool Send(IdleAppraiseIntent intent)
        {
            return IdlePotentials.TryAppraise(state, tuning, intent.Tier, out PotentialRoll _);
        }

        /// <summary>
        /// 감정하고 <b>무엇이 나왔는지</b>까지 돌려준다 — 화면이 결과를 보여줘야 도박이 도박이 된다.
        /// </summary>
        public bool TryAppraise(int tier, out PotentialRoll roll)
        {
            return IdlePotentials.TryAppraise(state, tuning, tier, out roll);
        }

        /// <summary>판을 환생하고 점수로 바꾼다. 아직 못 환생하면 아무 일도 안 일어난다.</summary>
        public bool Send(IdlePrestigeIntent intent)
        {
            return IdleModel.TryPrestige(state, tuning, out long _);
        }

        /// <summary>시간을 흘린다 — <b>보고 있는 동안만</b> 도는 층(지나가는 것·폭주).</summary>
        public void AdvanceSurge(double seconds)
        {
            IdleSurge.Advance(state, tuning, seconds);
        }

        /// <summary>지나가는 것을 잡는다.</summary>
        public bool TryCatchVisitor(out IdleSurgeKind caught)
        {
            return IdleSurge.TryCatch(state, tuning, out caught);
        }
    }
}

