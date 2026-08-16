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
}
