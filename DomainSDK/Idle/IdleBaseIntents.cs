using WitchMendokusai.DomainSDK.Contracts;

namespace WitchMendokusai.DomainSDK.Idle
{
    /// <summary>「생산자를 하나 산다」 — 기지 층에서 사람이 하는 유일한 행동 (TASK-WM-406).</summary>
    public readonly struct IdleBuyProducerIntent : IGameIntent
    {
        public int Kind { get; }

        public IdleBuyProducerIntent(int kind)
        {
            Kind = kind;
        }
    }

    /// <summary>「이 등급을 n개 분해한다」. 0 이하면 전부 (사용자 2026-09-05)</summary>
    public readonly struct IdleSalvageIntent : IGameIntent
    {
        public int Tier { get; }
        public int Count { get; }

        public IdleSalvageIntent(int tier, int count)
        {
            Tier = tier;
            Count = count;
        }
    }

    /// <summary>「가방 칸 하나를 잠근다(푼다)」. 잠근 것은 합치기와 분해에서 제외</summary>
    public readonly struct IdleLockItemIntent : IGameIntent
    {
        public int BagIndex { get; }
        public bool Locked { get; }

        public IdleLockItemIntent(int bagIndex, bool locked)
        {
            BagIndex = bagIndex;
            Locked = locked;
        }
    }

    /// <summary>「가방을 정렬한다」. 등급 내림차순, 같으면 부위 순</summary>
    public readonly struct IdleSortBagIntent : IGameIntent
    {
    }

    /// <summary>「이 부위·이 등급을 합친다」 — 같은 것 셋이 한 단계 위가 된다.</summary>
    public readonly struct IdleMergeIntent : IGameIntent
    {
        public int Tier { get; }
        public IdleItemSlot Slot { get; }

        public IdleMergeIntent(int tier, IdleItemSlot slot)
        {
            Tier = tier;
            Slot = slot;
        }
    }

    /// <summary>「가방의 이것을 찬다」 — 차고 있던 것은 가방으로 돌아온다.</summary>
    public readonly struct IdleEquipIntent : IGameIntent
    {
        /// <summary>끼울 인형 번호 (사용자 2026-08-31: 장비는 인형별)</summary>
        public int HeroId { get; }

        public int BagIndex { get; }

        public IdleEquipIntent(int heroId, int bagIndex)
        {
            HeroId = heroId;
            BagIndex = bagIndex;
        }
    }

    /// <summary>
    /// 「손으로 한 대 때린다」 — 사람이 판을 눌렀다 (TASK-WM-406).
    ///
    /// ★ 이 게임에서 사람이 <b>아무 때나</b> 할 수 있는 유일한 행동이다.
    ///   나머지(사기·합치기·감정·환생)는 모을 것이 있어야 누를 수 있다.
    /// </summary>
    public readonly struct IdleTapIntent : IGameIntent
    {
    }

    /// <summary>「영웅을 한 번 뽑는다」 — 환생석을 치른다 (TASK-WM-406).</summary>
    public readonly struct IdlePullHeroIntent : IGameIntent
    {
    }

    /// <summary>
    /// 「다음 구역으로 간다」 — 실패해서 반복 중일 때 사람이 다시 도전한다 (V2 방향 6).
    /// </summary>
    public readonly struct IdleNextStageIntent : IGameIntent
    {
    }

    /// <summary>「카드 한 장을 낸다」 — 코스트를 치른다 (V2, concept-v2).</summary>
    public readonly struct IdleCastCardIntent : IGameIntent
    {
        public int HandIndex { get; }

        public IdleCastCardIntent(int handIndex)
        {
            HandIndex = handIndex;
        }
    }

    /// <summary>「이 자리에 이 영웅을 앉힌다」 — 같은 영웅이 다른 자리에 있으면 자리를 맞바꾼다.</summary>
    public readonly struct IdleSetPartyIntent : IGameIntent
    {
        public int Slot { get; }
        public int HeroId { get; }

        public IdleSetPartyIntent(int slot, int heroId)
        {
            Slot = slot;
            HeroId = heroId;
        }
    }
}
