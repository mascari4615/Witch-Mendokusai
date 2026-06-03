namespace WitchMendokusai.DomainSDK.Life
{
    /// <summary>
    /// 한 주민의 노동 런타임 상태 — 지금 하는 일(<see cref="CurrentWork"/>) + 4호 지시(<see cref="ActiveAssignment"/>, 만료 카운트다운). 순수 POCO(DomainSDK).
    /// <see cref="NeedState"/> 형제 — LifeAgent(INC-W4)가 보유, 매 틱 <see cref="Advance"/> 로 갱신. 욕구는 여기 없음(노동만).
    /// SaveData 격상은 후속(INC-W6) — 지금은 런타임 only.
    /// </summary>
    public sealed class WorkState
    {
        public WorkState(WorkKind initialWork)
        {
            CurrentWork = initialWork;
        }

        /// <summary>지금 하는 일.</summary>
        public WorkKind CurrentWork { get; private set; }

        /// <summary>4호 지시(있으면) — 없으면 자율(<see cref="WorkProfile.DefaultWork"/>).</summary>
        public WorkAssignment? ActiveAssignment { get; private set; }

        /// <summary>유효한 4호 지시가 걸려 있는가.</summary>
        public bool HasActiveAssignment => ActiveAssignment.HasValue && ActiveAssignment.Value.IsActive;

        /// <summary>4호 지시 박기 — 만료 전까지 이 일을 우선(다음 <see cref="Advance"/> 부터 반영).</summary>
        public void Assign(WorkKind kind, int minutes) => ActiveAssignment = new WorkAssignment(kind, minutes);

        /// <summary>지시 해제 — 즉시 자율로 복귀.</summary>
        public void ClearAssignment() => ActiveAssignment = null;

        /// <summary>
        /// 시간 경과 한 스텝 — ① 지금 유효한 지시(또는 자율)로 이 구간의 일을 결정 ② 지시 남은 시간 차감(만료되면 해제).
        /// 결정→차감 순서라 지시가 그 구간 동안은 유지된다(off-by-one 방지). <see cref="CurrentWork"/> 가 바뀌면 true(이벤트·색 갱신용).
        /// 욕구 우선(배고프면 일 멈춤)은 호출자(LifeAgent)가 이 Advance 호출 여부로 게이트.
        /// </summary>
        public bool Advance(WorkProfile profile, int minutes)
        {
            WorkKind next = WorkSelector.SelectWork(profile, ActiveAssignment);
            bool changed = next != CurrentWork;
            CurrentWork = next;

            if (ActiveAssignment.HasValue)
            {
                WorkAssignment ticked = ActiveAssignment.Value.Tick(minutes);
                ActiveAssignment = ticked.IsActive ? ticked : (WorkAssignment?)null;
            }

            return changed;
        }
    }
}
