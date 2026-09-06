using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-168 Tomodachi 자율 삶 레이어 INC-1 — <see cref="NeedModel"/> 욕구 시간·회복·시급도 코어 회귀 잠금.
    /// 순수 POCO — PlayMode/GUI 무관. (패턴: Farming/WitchPlantGrowthTest — new() 직접 + Assert.That)
    /// </summary>
    public sealed class NeedModelTest
    {
        // 테스트 프로필: Hunger 빨리 줆(분당 1, 임계 30, 상한 100) / Energy 느림(분당 0.5, 임계 20, 상한 100)
        //              / Mood 상한 다름(분당 1, 임계 10, 상한 50) — 정규화 비교 검증용.
        private static NeedProfile MakeProfile()
        {
            Dictionary<NeedKind, NeedSpec> specs = new()
            {
                { NeedKind.Hunger, new NeedSpec(1f, 30f, 100f) },
                { NeedKind.Energy, new NeedSpec(0.5f, 20f, 100f) },
                { NeedKind.Mood, new NeedSpec(1f, 10f, 50f) },
            };
            return new NeedProfile(specs);
        }

        private static NeedState MakeState(float hunger, float energy, float mood)
        {
            return new NeedState(new Dictionary<NeedKind, float>
            {
                { NeedKind.Hunger, hunger },
                { NeedKind.Energy, energy },
                { NeedKind.Mood, mood },
            });
        }

        [Test]
        public void Step_DecaysEachNeedByItsOwnRate()
        {
            NeedState state = MakeState(100f, 100f, 50f);
            NeedModel.Step(state, MakeProfile(), 10);

            Assert.That(state.Get(NeedKind.Hunger), Is.EqualTo(90f), "분당 1 × 10분 = 10 감소");
            Assert.That(state.Get(NeedKind.Energy), Is.EqualTo(95f), "분당 0.5 × 10분 = 5 감소");
            Assert.That(state.Get(NeedKind.Mood), Is.EqualTo(40f), "분당 1 × 10분 = 10 감소");
        }

        [Test]
        public void Step_ClampsAtFloor_NeverNegative()
        {
            NeedState state = MakeState(5f, 100f, 50f);
            NeedModel.Step(state, MakeProfile(), 100);

            Assert.That(state.Get(NeedKind.Hunger), Is.EqualTo(0f), "굶주려도 음수 안 됨(하한 0)");
        }

        [Test]
        public void Satisfy_RestoresButClampsAtMax()
        {
            NeedState state = MakeState(95f, 100f, 45f);
            NeedProfile profile = MakeProfile();

            NeedModel.Satisfy(state, profile, NeedKind.Hunger, 20f);
            Assert.That(state.Get(NeedKind.Hunger), Is.EqualTo(100f), "상한 100 클램프");

            NeedModel.Satisfy(state, profile, NeedKind.Mood, 20f);
            Assert.That(state.Get(NeedKind.Mood), Is.EqualTo(50f), "Mood 상한 50 클램프");
        }

        [Test]
        public void IsInNeed_TrueOnlyBelowThreshold()
        {
            NeedProfile profile = MakeProfile();

            Assert.That(NeedModel.IsInNeed(MakeState(29f, 100f, 50f), profile, NeedKind.Hunger), Is.True, "29 < 임계 30");
            Assert.That(NeedModel.IsInNeed(MakeState(30f, 100f, 50f), profile, NeedKind.Hunger), Is.False, "30 = 임계, 문제 아님");
        }

        [Test]
        public void TryGetMostUrgent_PicksLowestNormalizedRatio()
        {
            // Hunger 25/100 = 0.25 / Mood 8/50 = 0.16 → 둘 다 임계 미만이지만 Mood 정규화가 더 낮음.
            NeedState state = MakeState(25f, 100f, 8f);
            bool found = NeedModel.TryGetMostUrgent(state, MakeProfile(), out NeedKind urgent);

            Assert.That(found, Is.True);
            Assert.That(urgent, Is.EqualTo(NeedKind.Mood), "절대값(25 vs 8) 아닌 정규화 비율(0.25 vs 0.16)로 판정");
        }

        [Test]
        public void TryGetMostUrgent_TieBreaksByEnumOrder()
        {
            // Hunger 20/100 = 0.2, Mood 10/50 = 0.2 동률 → enum 최저값 Hunger(0) 선택(결정성).
            NeedState state = MakeState(20f, 100f, 10f);
            NeedModel.TryGetMostUrgent(state, MakeProfile(), out NeedKind urgent);

            Assert.That(urgent, Is.EqualTo(NeedKind.Hunger), "동률 = enum 최저값 타이브레이크");
        }

        [Test]
        public void TryGetMostUrgent_FalseWhenNoneInNeed()
        {
            NeedState state = MakeState(100f, 100f, 50f);
            bool found = NeedModel.TryGetMostUrgent(state, MakeProfile(), out _);

            Assert.That(found, Is.False, "문제 욕구 없으면 false");
        }
    }
}
