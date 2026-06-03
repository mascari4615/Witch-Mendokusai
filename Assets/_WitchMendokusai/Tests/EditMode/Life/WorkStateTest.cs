using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-183 INC-W2 — <see cref="WorkState"/> 노동 런타임 상태(지시 카운트다운→만료 자율복귀) +
    /// <see cref="InterventionModel.ApplyWorkOverride"/>(4호 일 지정) 회귀 잠금 (순수, Play 무관).
    /// </summary>
    public sealed class WorkStateTest
    {
        private static WorkProfile Profile(WorkKind defaultWork)
        {
            return new WorkProfile(defaultWork, new Dictionary<WorkKind, float>());
        }

        [Test]
        public void Advance_NoAssignment_PicksDefaultWork()
        {
            WorkState state = new WorkState(WorkKind.Idle);
            bool changed = state.Advance(Profile(WorkKind.Mine), 5);

            Assert.That(state.CurrentWork, Is.EqualTo(WorkKind.Mine), "지시 없음 = 자율 기본 일");
            Assert.That(changed, Is.True, "Idle → Mine 으로 바뀜");
            Assert.That(state.HasActiveAssignment, Is.False);
        }

        [Test]
        public void Advance_ActiveAssignment_OverridesDefault()
        {
            WorkState state = new WorkState(WorkKind.Mine);
            state.Assign(WorkKind.Cook, 30);
            state.Advance(Profile(WorkKind.Mine), 6);

            Assert.That(state.CurrentWork, Is.EqualTo(WorkKind.Cook), "유효 지시 = 자율 무시하고 지시 일");
            Assert.That(state.HasActiveAssignment, Is.True);
        }

        [Test]
        public void Advance_AssignmentHonoredForDuration_ThenRevertsToDefault()
        {
            // Cook 10분 지시. 결정→차감 순서라 지시 구간 동안은 Cook 유지, 소진 후 자율(Mine) 복귀.
            WorkState state = new WorkState(WorkKind.Mine);
            state.Assign(WorkKind.Cook, 10);

            Assert.That(state.Advance(Profile(WorkKind.Mine), 6), Is.True, "Mine → Cook");
            Assert.That(state.CurrentWork, Is.EqualTo(WorkKind.Cook));
            Assert.That(state.HasActiveAssignment, Is.True, "아직 4분 남음");

            state.Advance(Profile(WorkKind.Mine), 6); // 이 구간도 Cook(결정 먼저), 그 후 만료
            Assert.That(state.CurrentWork, Is.EqualTo(WorkKind.Cook), "만료되는 구간도 지시 일은 유지");
            Assert.That(state.HasActiveAssignment, Is.False, "소진 = 지시 해제");

            Assert.That(state.Advance(Profile(WorkKind.Mine), 6), Is.True, "다음 구간 = 자율 복귀");
            Assert.That(state.CurrentWork, Is.EqualTo(WorkKind.Mine));
        }

        [Test]
        public void ClearAssignment_RevertsImmediately()
        {
            WorkState state = new WorkState(WorkKind.Mine);
            state.Assign(WorkKind.Cook, 100);
            state.ClearAssignment();

            Assert.That(state.HasActiveAssignment, Is.False);
            state.Advance(Profile(WorkKind.Forage), 5);
            Assert.That(state.CurrentWork, Is.EqualTo(WorkKind.Forage), "해제 후 = 자율 기본 일");
        }

        [Test]
        public void Advance_ReturnsChanged_OnlyWhenWorkChanges()
        {
            WorkState state = new WorkState(WorkKind.Mine);
            Assert.That(state.Advance(Profile(WorkKind.Mine), 5), Is.False, "Mine 유지 = 변화 없음");
        }

        [Test]
        public void ApplyWorkOverride_SetsAssignment()
        {
            WorkState state = new WorkState(WorkKind.Mine);
            bool applied = InterventionModel.ApplyWorkOverride(state, WorkKind.Cook, 30);

            Assert.That(applied, Is.True);
            Assert.That(state.HasActiveAssignment, Is.True);
            Assert.That(state.ActiveAssignment.Value.RequestedWork, Is.EqualTo(WorkKind.Cook));
            Assert.That(state.ActiveAssignment.Value.RemainingMinutes, Is.EqualTo(30));
        }

        [Test]
        public void ApplyWorkOverride_RejectsIdleAndNonPositiveMinutes()
        {
            WorkState state = new WorkState(WorkKind.Mine);

            Assert.That(InterventionModel.ApplyWorkOverride(state, WorkKind.Idle, 30), Is.False, "Idle(무위) 지정 거절");
            Assert.That(InterventionModel.ApplyWorkOverride(state, WorkKind.Cook, 0), Is.False, "0분 거절");
            Assert.That(InterventionModel.ApplyWorkOverride(state, WorkKind.Cook, -5), Is.False, "음수 거절");
            Assert.That(state.HasActiveAssignment, Is.False, "전부 거절됐으니 지시 없음");
        }
    }
}
