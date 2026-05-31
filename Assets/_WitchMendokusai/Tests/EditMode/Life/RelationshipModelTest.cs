using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-168 Tomodachi 자율 삶 레이어 INC-3 — <see cref="RelationshipModel"/> 관계 진전·개입 게이트 회귀 잠금.
    /// 핵심 invariant: 친밀도 자율 상승은 동거(Housemate)까지, 연애·결혼은 4호 개입(TryIntervene)으로만.
    /// 순수 — PlayMode/GUI 무관.
    /// </summary>
    public sealed class RelationshipModelTest
    {
        // 단계 진입 친밀도: Stranger0 / Acq10 / Friend25 / Best50 / Housemate80 / Partner120 / Married200.
        // 자율 상한 = Housemate. (연애·결혼은 개입 전용)
        private static RelationshipParams MakeParams()
        {
            float[] thresholds = { 0f, 10f, 25f, 50f, 80f, 120f, 200f };
            return new RelationshipParams(thresholds, RelationshipStage.Housemate);
        }

        [Test]
        public void AddAffinity_AdvancesStepwiseThroughAutoStages()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeParams();

            RelationshipModel.AddAffinity(state, parameters, 10f);
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.Acquaintance), "10 = 아는 사이");

            RelationshipModel.AddAffinity(state, parameters, 40f); // 누적 50
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.BestFriend), "50 = 베프");
        }

        [Test]
        public void AddAffinity_NeverReachesPartner_NoMatterHowHigh()
        {
            // 핵심 invariant — 친밀도 만땅(999)이어도 자율로는 Housemate 에서 멈춘다.
            RelationshipState state = new(1, 2);
            RelationshipModel.AddAffinity(state, MakeParams(), 999f);

            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.Housemate),
                "연애·결혼은 자연 발동 X — 친밀도 아무리 높아도 동거에서 멈춤");
        }

        [Test]
        public void TryIntervene_PromotesPastCeiling_WhenAffinityMet()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeParams();
            RelationshipModel.AddAffinity(state, parameters, 130f); // Housemate 에 멈춤, affinity 130

            bool promoted = RelationshipModel.TryIntervene(state, parameters);
            Assert.That(promoted, Is.True);
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.Partner), "4호 개입 + 친밀도(130≥120) → 연인");
        }

        [Test]
        public void TryIntervene_FailsBelowCeiling_AutoStagesAreNotInterventionTargets()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeParams();
            RelationshipModel.AddAffinity(state, parameters, 25f); // Friend

            bool promoted = RelationshipModel.TryIntervene(state, parameters);
            Assert.That(promoted, Is.False, "자율 영역(Friend→Best)은 개입 대상 아님 — AddAffinity 가 처리");
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.Friend));
        }

        [Test]
        public void TryIntervene_FailsWhenAffinityInsufficient()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeParams();
            RelationshipModel.AddAffinity(state, parameters, 85f); // Housemate, affinity 85 (<120)

            bool promoted = RelationshipModel.TryIntervene(state, parameters);
            Assert.That(promoted, Is.False, "동거여도 친밀도 부족(85<120)이면 개입해도 연인 안 됨");
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.Housemate));
        }

        [Test]
        public void TryIntervene_MarriedIsTop()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeParams();
            RelationshipModel.AddAffinity(state, parameters, 250f); // Housemate(자율 상한), affinity 250

            Assert.That(RelationshipModel.TryIntervene(state, parameters), Is.True, "→ Partner");
            Assert.That(RelationshipModel.TryIntervene(state, parameters), Is.True, "→ Married");
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.Married));
            Assert.That(RelationshipModel.TryIntervene(state, parameters), Is.False, "Married 가 최고 단계");
        }

        [Test]
        public void RequiresIntervention_TrueOnlyAtCeiling()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeParams();

            RelationshipModel.AddAffinity(state, parameters, 25f); // Friend
            Assert.That(RelationshipModel.RequiresIntervention(state, parameters), Is.False, "자율 영역");

            RelationshipModel.AddAffinity(state, parameters, 100f); // Housemate
            Assert.That(RelationshipModel.RequiresIntervention(state, parameters), Is.True, "다음(Partner)=개입 필요");
        }

        [Test]
        public void AddAffinity_NegativeDoesNotDemoteStage()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeParams();
            RelationshipModel.AddAffinity(state, parameters, 50f); // BestFriend

            RelationshipModel.AddAffinity(state, parameters, -45f); // 누적 5, 하지만 단계 유지
            Assert.That(state.Affinity, Is.EqualTo(5f), "친밀도는 줄어듦");
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.BestFriend), "단계는 후퇴 안 함");
        }
    }
}
