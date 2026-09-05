using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.DomainSDK.Act
{
    /// <summary>
    /// 행동 한 건을 원장에 걸어 본 결과 (TASK-WM-408). 순수 값 타입 (DomainSDK).
    /// 거절이면 세계는 <b>손도 안 댄 채</b> 그대로다(원장은 전부 적용하거나 전부 안 한다).
    /// </summary>
    public readonly struct ActOutcome
    {
        private ActOutcome(bool applied, bool dayChanged, ActRejection rejection, NeedKind rejectedNeed, ResourceId rejectedResource)
        {
            Applied = applied;
            DayChanged = dayChanged;
            Rejection = rejection;
            RejectedNeed = rejectedNeed;
            RejectedResource = rejectedResource;
        }

        /// <summary>적용됐나.</summary>
        public readonly bool Applied;

        /// <summary>이 행동으로 하루가 넘어갔나 — 자정에 걸리는 일들(정산·성장)의 입력.</summary>
        public readonly bool DayChanged;

        public readonly ActRejection Rejection;

        /// <summary>모자랐던 욕구 (<see cref="Rejection"/> 가 Need 일 때만 의미).</summary>
        public readonly NeedKind RejectedNeed;

        /// <summary>모자랐던 자원 (<see cref="Rejection"/> 가 Resource 일 때만 의미).</summary>
        public readonly ResourceId RejectedResource;

        public static ActOutcome Success(bool dayChanged) => new ActOutcome(true, dayChanged, ActRejection.None, default, default);

        public static ActOutcome NeedShort(NeedKind kind) => new ActOutcome(false, false, ActRejection.Need, kind, default);

        public static ActOutcome ResourceShort(ResourceId resource) => new ActOutcome(false, false, ActRejection.Resource, default, resource);
    }
}
