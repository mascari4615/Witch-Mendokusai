using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-168 Tomodachi 자율 삶 레이어 INC-2 — <see cref="ActivitySelector"/> 활동 선택 코어 회귀 잠금.
    /// 욕구가 활동을 끌어당기고, 급한 욕구 없으면 시간대 기본. 순수 — PlayMode/GUI 무관.
    /// </summary>
    public sealed class ActivitySelectorTest
    {
        private static NeedProfile MakeProfile()
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

        private static NeedState MakeState(float hunger, float energy, float mood, float social)
        {
            return new NeedState(new Dictionary<NeedKind, float>
            {
                { NeedKind.Hunger, hunger },
                { NeedKind.Energy, energy },
                { NeedKind.Mood, mood },
                { NeedKind.Social, social },
            });
        }

        // 충족도 다 채운 상태 (어느 욕구도 임계 미만 아님).
        private static NeedState Satisfied()
        {
            return MakeState(100f, 100f, 50f, 100f);
        }

        [Test]
        public void Select_UrgentHunger_PicksEat()
        {
            NeedState state = MakeState(10f, 100f, 50f, 100f);
            ActivityKind activity = ActivitySelector.Select(state, MakeProfile(), TimeOfDay.Afternoon);

            Assert.That(activity, Is.EqualTo(ActivityKind.Eat), "배고프면 먹기");
        }

        [Test]
        public void Select_UrgentNeed_BeatsTimeOfDay()
        {
            // 밤이라도 급한 배고픔이 시간대 기본(Sleep)을 이긴다.
            NeedState state = MakeState(5f, 100f, 50f, 100f);
            ActivityKind activity = ActivitySelector.Select(state, MakeProfile(), TimeOfDay.Night);

            Assert.That(activity, Is.EqualTo(ActivityKind.Eat), "욕구가 시간대보다 우선");
        }

        [Test]
        public void Select_MultipleUrgent_FollowsMostUrgent()
        {
            // Hunger 25/100=0.25, Social 20/100=0.2 → 정규화 최저 Social → Socialize.
            NeedState state = MakeState(25f, 100f, 50f, 20f);
            ActivityKind activity = ActivitySelector.Select(state, MakeProfile(), TimeOfDay.Morning);

            Assert.That(activity, Is.EqualTo(ActivityKind.Socialize), "가장 시급한 결핍(정규화 최저)을 따른다");
        }

        [Test]
        public void Select_NoUrgent_Night_Sleeps()
        {
            ActivityKind activity = ActivitySelector.Select(Satisfied(), MakeProfile(), TimeOfDay.Night);
            Assert.That(activity, Is.EqualTo(ActivityKind.Sleep), "급한 욕구 없는 밤 = 자기(예방 회복)");
        }

        [Test]
        public void Select_NoUrgent_Day_Idles()
        {
            ActivityKind activity = ActivitySelector.Select(Satisfied(), MakeProfile(), TimeOfDay.Afternoon);
            Assert.That(activity, Is.EqualTo(ActivityKind.Idle), "급한 욕구 없는 낮 = 배회");
        }

        [Test]
        public void ActivityForNeed_MapsEachNeed()
        {
            Assert.That(ActivitySelector.ActivityForNeed(NeedKind.Hunger), Is.EqualTo(ActivityKind.Eat));
            Assert.That(ActivitySelector.ActivityForNeed(NeedKind.Energy), Is.EqualTo(ActivityKind.Sleep));
            Assert.That(ActivitySelector.ActivityForNeed(NeedKind.Mood), Is.EqualTo(ActivityKind.Hobby));
            Assert.That(ActivitySelector.ActivityForNeed(NeedKind.Social), Is.EqualTo(ActivityKind.Socialize));
        }
    }
}
