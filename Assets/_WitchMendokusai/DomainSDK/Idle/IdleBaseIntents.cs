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
        public int BagIndex { get; }

        public IdleEquipIntent(int bagIndex)
        {
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

    /// <summary>「카드 한 장을 낸다」 — 코스트를 치른다 (V2, concept-v2).</summary>
    public readonly struct IdleCastCardIntent : IGameIntent
    {
        public IdleCardKind Kind { get; }

        public IdleCastCardIntent(IdleCardKind kind)
        {
            Kind = kind;
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
