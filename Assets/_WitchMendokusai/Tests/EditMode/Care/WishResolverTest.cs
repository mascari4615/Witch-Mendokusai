using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Care;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-171 「되살림, 그리고 배웅」 Phase 0 — <see cref="WishResolver"/> 회귀 잠금.
    /// 순수 POCO — PlayMode/GUI 무관. (패턴: Life/NeedModelTest — new() 직접 + Assert.That)
    ///
    /// 검증 핵심:
    /// ① 재료/충족도 누적이 결정적이고 클램프된다
    /// ② 완성 판정이 양쪽(재료 AND 충족도) 모두 충족 시에만 true
    /// ③ 결말 분기(Depart/Settle)가 데이터 정의대로 갈린다 (코드 정책 X)
    /// </summary>
    public sealed class WishResolverTest
    {
        private const string ITEM_ROSE = "rose";
        private const string ITEM_LETTER = "letter";
        private const string CHANNEL_COMPANION = "companion";
        private const string CHANNEL_HUNGER = "hunger";

        private static WishSpec MakeMaterialOnlyDepart()
        {
            return new WishSpec(
                id: "yon-mother-rose",
                kind: WishKind.Closure,
                materials: new List<WishMaterialReq> { new(ITEM_ROSE, 3) },
                satisfactionTargets: new Dictionary<string, float>(),
                outcomeOnComplete: WishOutcome.Depart);
        }

        private static WishSpec MakeSatisfactionOnlySettle()
        {
            return new WishSpec(
                id: "ringo-companion",
                kind: WishKind.Companionship,
                materials: new List<WishMaterialReq>(),
                satisfactionTargets: new Dictionary<string, float> { { CHANNEL_COMPANION, 0.8f } },
                outcomeOnComplete: WishOutcome.Settle);
        }

        private static WishSpec MakeMixed()
        {
            return new WishSpec(
                id: "demon-reconcile",
                kind: WishKind.Reconciliation,
                materials: new List<WishMaterialReq>
                {
                    new(ITEM_ROSE, 2),
                    new(ITEM_LETTER, 1),
                },
                satisfactionTargets: new Dictionary<string, float>
                {
                    { CHANNEL_COMPANION, 0.5f },
                    { CHANNEL_HUNGER, 0.7f },
                },
                outcomeOnComplete: WishOutcome.Depart);
        }

        [Test]
        public void Progress_AddMaterial_AccumulatesAndReadsBack()
        {
            WishProgress progress = new();
            progress.AddMaterial(ITEM_ROSE, 1);
            progress.AddMaterial(ITEM_ROSE, 2);

            Assert.That(progress.GetMaterialCount(ITEM_ROSE), Is.EqualTo(3), "같은 itemId 반복 = 합산");
            Assert.That(progress.GetMaterialCount(ITEM_LETTER), Is.EqualTo(0), "미설정 키 = 0");
        }

        [Test]
        public void Progress_SetSatisfaction_ClampsTo0And1()
        {
            WishProgress progress = new();
            progress.SetSatisfaction(CHANNEL_COMPANION, 1.5f);
            Assert.That(progress.GetSatisfaction(CHANNEL_COMPANION), Is.EqualTo(1f), "상한 1 클램프");

            progress.SetSatisfaction(CHANNEL_HUNGER, -0.3f);
            Assert.That(progress.GetSatisfaction(CHANNEL_HUNGER), Is.EqualTo(0f), "하한 0 클램프");
        }

        [Test]
        public void Progress_GetSatisfaction_UnsetChannelReturnsZero()
        {
            WishProgress progress = new();
            Assert.That(progress.GetSatisfaction("unknown"), Is.EqualTo(0f), "미설정 키 = 0");
        }

        [Test]
        public void IsMaterialMet_FalseUntilAllReqsMet()
        {
            WishSpec spec = MakeMaterialOnlyDepart();
            WishProgress progress = new();

            progress.AddMaterial(ITEM_ROSE, 2);
            Assert.That(WishResolver.IsMaterialMet(spec, progress), Is.False, "2/3 = 미충족");

            progress.AddMaterial(ITEM_ROSE, 1);
            Assert.That(WishResolver.IsMaterialMet(spec, progress), Is.True, "3/3 = 충족");
        }

        [Test]
        public void IsMaterialMet_TrueWithSurplus()
        {
            WishSpec spec = MakeMaterialOnlyDepart();
            WishProgress progress = new();
            progress.AddMaterial(ITEM_ROSE, 10);

            Assert.That(WishResolver.IsMaterialMet(spec, progress), Is.True, "여분도 충족");
        }

        [Test]
        public void IsMaterialMet_TrueOnEmptyMaterialList()
        {
            WishSpec spec = MakeSatisfactionOnlySettle();
            WishProgress progress = new();

            Assert.That(WishResolver.IsMaterialMet(spec, progress), Is.True, "빈 재료 = 즉시 충족");
        }

        [Test]
        public void IsSatisfactionMet_FalseUntilTargetReached()
        {
            WishSpec spec = MakeSatisfactionOnlySettle();
            WishProgress progress = new();

            progress.SetSatisfaction(CHANNEL_COMPANION, 0.79f);
            Assert.That(WishResolver.IsSatisfactionMet(spec, progress), Is.False, "0.79 < 0.8 목표");

            progress.SetSatisfaction(CHANNEL_COMPANION, 0.8f);
            Assert.That(WishResolver.IsSatisfactionMet(spec, progress), Is.True, "0.8 = 목표, 도달 인정(>=)");
        }

        [Test]
        public void IsSatisfactionMet_TrueOnEmptyTargets()
        {
            WishSpec spec = MakeMaterialOnlyDepart();
            WishProgress progress = new();

            Assert.That(WishResolver.IsSatisfactionMet(spec, progress), Is.True, "빈 목표 = 즉시 충족");
        }

        [Test]
        public void IsComplete_RequiresBothMaterialAndSatisfaction()
        {
            WishSpec spec = MakeMixed();
            WishProgress progress = new();

            progress.AddMaterial(ITEM_ROSE, 2);
            progress.AddMaterial(ITEM_LETTER, 1);
            Assert.That(WishResolver.IsComplete(spec, progress), Is.False, "재료만 충족 = 미완성");

            progress.SetSatisfaction(CHANNEL_COMPANION, 0.5f);
            Assert.That(WishResolver.IsComplete(spec, progress), Is.False, "한 채널만 충족 = 미완성");

            progress.SetSatisfaction(CHANNEL_HUNGER, 0.7f);
            Assert.That(WishResolver.IsComplete(spec, progress), Is.True, "양쪽 모두 충족 = 완성");
        }

        [Test]
        public void TryResolve_FalseIfIncomplete_OutcomeIsDefault()
        {
            WishSpec spec = MakeMaterialOnlyDepart();
            WishProgress progress = new();
            progress.AddMaterial(ITEM_ROSE, 1);

            bool resolved = WishResolver.TryResolve(spec, progress, out WishOutcome outcome);

            Assert.That(resolved, Is.False, "미완성 = false");
            Assert.That(outcome, Is.EqualTo(default(WishOutcome)), "미완성 = outcome 기본값");
        }

        [Test]
        public void TryResolve_ReturnsDepartWhenSpecSaysDepart()
        {
            WishSpec spec = MakeMaterialOnlyDepart();
            WishProgress progress = new();
            progress.AddMaterial(ITEM_ROSE, 3);

            bool resolved = WishResolver.TryResolve(spec, progress, out WishOutcome outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome, Is.EqualTo(WishOutcome.Depart), "WishSpec 데이터가 정책 — 코드 X");
        }

        [Test]
        public void TryResolve_ReturnsSettleWhenSpecSaysSettle()
        {
            WishSpec spec = MakeSatisfactionOnlySettle();
            WishProgress progress = new();
            progress.SetSatisfaction(CHANNEL_COMPANION, 0.9f);

            bool resolved = WishResolver.TryResolve(spec, progress, out WishOutcome outcome);

            Assert.That(resolved, Is.True);
            Assert.That(outcome, Is.EqualTo(WishOutcome.Settle), "같은 코드, 다른 데이터 = 다른 결말");
        }

        [Test]
        public void TryResolve_IsDeterministic_SameInputsSameOutcome()
        {
            WishSpec spec = MakeMixed();

            WishProgress progressA = new();
            progressA.AddMaterial(ITEM_ROSE, 2);
            progressA.AddMaterial(ITEM_LETTER, 1);
            progressA.SetSatisfaction(CHANNEL_COMPANION, 0.6f);
            progressA.SetSatisfaction(CHANNEL_HUNGER, 0.8f);

            WishProgress progressB = new();
            progressB.AddMaterial(ITEM_ROSE, 2);
            progressB.AddMaterial(ITEM_LETTER, 1);
            progressB.SetSatisfaction(CHANNEL_COMPANION, 0.6f);
            progressB.SetSatisfaction(CHANNEL_HUNGER, 0.8f);

            WishResolver.TryResolve(spec, progressA, out WishOutcome outA);
            WishResolver.TryResolve(spec, progressB, out WishOutcome outB);

            Assert.That(outA, Is.EqualTo(outB), "동일 입력 = 동일 출력(결정성)");
        }
    }
}
