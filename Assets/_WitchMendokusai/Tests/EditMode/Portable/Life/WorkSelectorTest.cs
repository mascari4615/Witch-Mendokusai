using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-183 INC-W1 — <see cref="WorkSelector"/> 자율/4호-개입 노동 선택 + <see cref="WorkAssignment"/> 만료 회귀 잠금 (순수).
    /// 자율-우선: 지시 없으면 DefaultWork, 지시 유효하면 그것, 만료되면 자율 복귀.
    /// </summary>
    public sealed class WorkSelectorTest
    {
        private static WorkProfile Profile(WorkKind defaultWork)
        {
            return new WorkProfile(defaultWork, new Dictionary<WorkKind, float>());
        }

        [Test]
        public void SelectWork_NoAssignment_PicksDefaultWork()
        {
            Assert.That(WorkSelector.SelectWork(Profile(WorkKind.Mine), null), Is.EqualTo(WorkKind.Mine), "지시 없음 = 자율 기본 일");
        }

        [Test]
        public void SelectWork_ActiveAssignment_OverridesDefault()
        {
            WorkAssignment assignment = new WorkAssignment(WorkKind.Cook, 30);
            Assert.That(WorkSelector.SelectWork(Profile(WorkKind.Mine), assignment), Is.EqualTo(WorkKind.Cook), "유효 지시 = 자율 무시하고 지시 일");
        }

        [Test]
        public void SelectWork_ExpiredAssignment_RevertsToDefault()
        {
            WorkAssignment expired = new WorkAssignment(WorkKind.Cook, 0);
            Assert.That(expired.IsActive, Is.False, "남은 시간 0 = 만료");
            Assert.That(WorkSelector.SelectWork(Profile(WorkKind.Mine), expired), Is.EqualTo(WorkKind.Mine), "만료 지시 = 자율 복귀");
        }

        [Test]
        public void Assignment_Tick_CountsDownAndExpires()
        {
            WorkAssignment assignment = new WorkAssignment(WorkKind.Cook, 10);
            assignment = assignment.Tick(6);
            Assert.That(assignment.RemainingMinutes, Is.EqualTo(4));
            Assert.That(assignment.IsActive, Is.True);

            assignment = assignment.Tick(4);
            Assert.That(assignment.IsActive, Is.False, "남은 시간 소진 = 만료");
        }
    }
}
