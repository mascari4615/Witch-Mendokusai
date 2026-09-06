using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-168 INC-5d — ActivitySelector 의 활동↔욕구 역매핑 + 이력현상(commitment) 잠금.
	/// 한 활동을 그 욕구가 충분히 찰 때까지 유지(strobe 방지)하는 핵심 동작.
	/// </summary>
	public sealed class ActivitySelectorCommitmentTest
	{
		private const float CONTENT = 85f;

		private static NeedProfile Profile()
		{
			Dictionary<NeedKind, NeedSpec> specs = new()
			{
				{ NeedKind.Hunger, new NeedSpec(1f, 50f, 100f) },
				{ NeedKind.Energy, new NeedSpec(1f, 50f, 100f) },
				{ NeedKind.Mood, new NeedSpec(1f, 50f, 100f) },
				{ NeedKind.Social, new NeedSpec(1f, 50f, 100f) },
			};
			return new NeedProfile(specs);
		}

		[Test]
		public void NeedForActivity_IsInverseOfActivityForNeed()
		{
			Assert.That(ActivitySelector.NeedForActivity(ActivityKind.Eat), Is.EqualTo(NeedKind.Hunger));
			Assert.That(ActivitySelector.NeedForActivity(ActivityKind.Sleep), Is.EqualTo(NeedKind.Energy));
			Assert.That(ActivitySelector.NeedForActivity(ActivityKind.Hobby), Is.EqualTo(NeedKind.Mood));
			Assert.That(ActivitySelector.NeedForActivity(ActivityKind.Socialize), Is.EqualTo(NeedKind.Social));
			Assert.That(ActivitySelector.NeedForActivity(ActivityKind.Idle), Is.Null, "Idle 은 채울 욕구 없음");
		}

		[Test]
		public void Commitment_KeepsCurrentActivity_UntilContent()
		{
			// 현재 Eat. Hunger 60(임계 50 위라 안 급함)이지만 content 85 미만 → 더 급한 Energy 가 있어도 Eat 유지.
			NeedState state = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 60f }, { NeedKind.Energy, 20f }, { NeedKind.Mood, 90f }, { NeedKind.Social, 90f },
			});
			ActivityKind result = ActivitySelector.SelectWithCommitment(state, Profile(), TimeOfDay.Afternoon, ActivityKind.Eat, CONTENT);
			Assert.That(result, Is.EqualTo(ActivityKind.Eat), "욕구가 content 미만이면 그 활동을 계속(깜빡임 방지)");
		}

		[Test]
		public void Commitment_Reselects_WhenContent()
		{
			// 현재 Eat. Hunger 90(content 85 이상) → 재평가 → 가장 급한 Energy 20 → Sleep.
			NeedState state = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 90f }, { NeedKind.Energy, 20f }, { NeedKind.Mood, 90f }, { NeedKind.Social, 90f },
			});
			ActivityKind result = ActivitySelector.SelectWithCommitment(state, Profile(), TimeOfDay.Afternoon, ActivityKind.Eat, CONTENT);
			Assert.That(result, Is.EqualTo(ActivityKind.Sleep), "욕구가 충분히 차면 다음 급한 욕구로 넘어감");
		}

		[Test]
		public void Commitment_FromIdle_PicksUrgent()
		{
			// Idle 은 채울 욕구 없음 → 항상 재평가 → 급한 욕구가 있으면 그것.
			NeedState state = new(new Dictionary<NeedKind, float>
			{
				{ NeedKind.Hunger, 20f }, { NeedKind.Energy, 90f }, { NeedKind.Mood, 90f }, { NeedKind.Social, 90f },
			});
			ActivityKind result = ActivitySelector.SelectWithCommitment(state, Profile(), TimeOfDay.Afternoon, ActivityKind.Idle, CONTENT);
			Assert.That(result, Is.EqualTo(ActivityKind.Eat));
		}
	}
}
