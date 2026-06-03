using NUnit.Framework;
using UnityEngine;
using WitchMendokusai.DomainSDK.Life;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-168 INC-7 — LifeProfileSO(데이터 주도 성격)가 순수 NeedProfile 로 바르게 변환되는지 잠금.
	/// 격상순서(SO authoring → 순수 모델)의 변환 한 겹.
	/// </summary>
	public sealed class LifeProfileSOTest
	{
		private LifeProfileSO profile;

		[TearDown]
		public void TearDown()
		{
			if (profile != null)
			{
				Object.DestroyImmediate(profile);
				profile = null;
			}
		}

		[Test]
		public void ToNeedProfile_DefaultsToFourNeeds()
		{
			profile = ScriptableObject.CreateInstance<LifeProfileSO>();
			NeedProfile needProfile = profile.ToNeedProfile();

			Assert.That(needProfile.Kinds.Count, Is.EqualTo(4), "네 욕구 모두 변환");
			Assert.That(needProfile.SpecOf(NeedKind.Hunger).DecayPerMinute, Is.EqualTo(0.13f).Within(0.001f), "디폴트 Hunger 감소");
			Assert.That(needProfile.SpecOf(NeedKind.Hunger).LowThreshold, Is.EqualTo(40f).Within(0.001f), "디폴트 임계");
			Assert.That(needProfile.SpecOf(NeedKind.Hunger).Max, Is.EqualTo(100f).Within(0.001f), "디폴트 상한");
		}

		[Test]
		public void DefaultSelfSatisfy_IsPositive()
		{
			profile = ScriptableObject.CreateInstance<LifeProfileSO>();
			Assert.That(profile.SelfSatisfyPerMinute, Is.GreaterThan(0f), "기본 자가회복 > 0(활동이 욕구 채움)");
		}
	}
}
