using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-168 Tomodachi 자율 삶 레이어 INC-4 — <see cref="InterventionModel"/> 4호 개입 적용 회귀 잠금.
    /// 욕구 해소·중재·관계 도약(연애·결혼 게이트)이 자율 모델 위에 올바로 작용하는지. 순수 — PlayMode 무관.
    /// </summary>
    public sealed class InterventionModelTest
    {
        private static NeedProfile MakeNeedProfile()
        {
            Dictionary<NeedKind, NeedSpec> specs = new()
            {
                { NeedKind.Hunger, new NeedSpec(1f, 30f, 100f) },
                { NeedKind.Energy, new NeedSpec(0.5f, 20f, 100f) },
                { NeedKind.Mood, new NeedSpec(1f, 10f, 50f) },
                { NeedKind.Social, new NeedSpec(1f, 30f, 100f) },
            };
            return new NeedProfile(specs);
        }

        private static RelationshipParams MakeRelParams()
        {
            float[] thresholds = { 0f, 10f, 25f, 50f, 80f, 120f, 200f };
            return new RelationshipParams(thresholds, RelationshipStage.Housemate);
        }

        [Test]
        public void ApplyRelief_Feed_RaisesHunger()
        {
            NeedState state = new(new Dictionary<NeedKind, float> { { NeedKind.Hunger, 10f } });
            bool applied = InterventionModel.ApplyRelief(state, MakeNeedProfile(), InterventionKind.Feed, 40f);

            Assert.That(applied, Is.True);
            Assert.That(state.Get(NeedKind.Hunger), Is.EqualTo(50f), "먹이기 = Hunger 충족");
        }

        [Test]
        public void ApplyRelief_NonReliefKind_NoOp()
        {
            NeedState state = new(new Dictionary<NeedKind, float> { { NeedKind.Hunger, 10f } });
            bool applied = InterventionModel.ApplyRelief(state, MakeNeedProfile(), InterventionKind.Bond, 40f);

            Assert.That(applied, Is.False, "Bond 는 욕구 해소형이 아님");
            Assert.That(state.Get(NeedKind.Hunger), Is.EqualTo(10f), "변화 없음");
        }

        [Test]
        public void ReliefForNeed_MapsEachNeed()
        {
            Assert.That(InterventionModel.ReliefForNeed(NeedKind.Hunger), Is.EqualTo(InterventionKind.Feed));
            Assert.That(InterventionModel.ReliefForNeed(NeedKind.Energy), Is.EqualTo(InterventionKind.Rest));
            Assert.That(InterventionModel.ReliefForNeed(NeedKind.Mood), Is.EqualTo(InterventionKind.Cheer));
            Assert.That(InterventionModel.ReliefForNeed(NeedKind.Social), Is.EqualTo(InterventionKind.Socialize));
        }

        [Test]
        public void TryGetSuggestedRelief_PicksForMostUrgent()
        {
            // Social 20/100=0.2 가 가장 낮음 → Socialize 제안.
            NeedState state = new(new Dictionary<NeedKind, float>
            {
                { NeedKind.Hunger, 25f }, { NeedKind.Energy, 100f }, { NeedKind.Mood, 50f }, { NeedKind.Social, 20f },
            });

            bool found = InterventionModel.TryGetSuggestedRelief(state, MakeNeedProfile(), out InterventionKind suggested);
            Assert.That(found, Is.True);
            Assert.That(suggested, Is.EqualTo(InterventionKind.Socialize));
        }

        [Test]
        public void TryGetSuggestedRelief_FalseWhenNoProblem()
        {
            NeedState state = new(new Dictionary<NeedKind, float>
            {
                { NeedKind.Hunger, 100f }, { NeedKind.Energy, 100f }, { NeedKind.Mood, 50f }, { NeedKind.Social, 100f },
            });

            Assert.That(InterventionModel.TryGetSuggestedRelief(state, MakeNeedProfile(), out _), Is.False);
        }

        [Test]
        public void CanBond_TrueAtCeilingWithAffinity()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeRelParams();
            RelationshipModel.AddAffinity(state, parameters, 130f); // Housemate, affinity 130 (≥120)

            Assert.That(InterventionModel.CanBond(state, parameters), Is.True);
        }

        [Test]
        public void CanBond_FalseBelowCeiling()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeRelParams();
            RelationshipModel.AddAffinity(state, parameters, 25f); // Friend (자율 영역)

            Assert.That(InterventionModel.CanBond(state, parameters), Is.False, "자율 영역은 Bond 대상 아님");
        }

        [Test]
        public void CanBond_FalseWhenAffinityInsufficient()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeRelParams();
            RelationshipModel.AddAffinity(state, parameters, 85f); // Housemate, affinity 85 (<120)

            Assert.That(InterventionModel.CanBond(state, parameters), Is.False);
        }

        [Test]
        public void Bond_PromotesRelationshipPastGate()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeRelParams();
            RelationshipModel.AddAffinity(state, parameters, 130f);

            bool bonded = InterventionModel.Bond(state, parameters);
            Assert.That(bonded, Is.True);
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.Partner), "4호가 맺어줌 → 연인");
        }

        [Test]
        public void Mediate_RestoresAffinityWithoutDemotion()
        {
            RelationshipState state = new(1, 2);
            RelationshipParams parameters = MakeRelParams();
            RelationshipModel.AddAffinity(state, parameters, 50f);  // BestFriend
            RelationshipModel.AddAffinity(state, parameters, -45f); // affinity 5, 단계 유지

            InterventionModel.Mediate(state, parameters, 20f); // affinity 25 회복
            Assert.That(state.Affinity, Is.EqualTo(25f), "중재로 친밀도 회복");
            Assert.That(state.Stage, Is.EqualTo(RelationshipStage.BestFriend), "단계 유지");
        }
    }
}
