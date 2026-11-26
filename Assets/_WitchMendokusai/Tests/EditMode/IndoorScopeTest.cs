using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 실내 신호 (IndoorScope) 와 실내 빛 (IndoorLight). 실내면 날씨 입자를 멈추고 빛은 시계를 약하게 따름
	public class IndoorScopeTest
	{
		private const float NIGHT = 0.95f;

		[SetUp]
		public void SetUp() => IndoorScope.Clear();

		[TearDown]
		public void TearDown() => IndoorScope.Clear();

		[Test]
		public void Indoor_until_every_owner_leaves()
		{
			object museum = new();
			object mine = new();
			int changes = 0;
			void Count() => changes++;
			IndoorScope.Changed += Count;
			try
			{
				IndoorScope.Enter(museum);
				IndoorScope.Enter(mine);
				IndoorScope.Leave(museum);
				Assert.That(IndoorScope.IsIndoor, Is.True, "갱도가 아직 실내");
				IndoorScope.Leave(mine);
				Assert.That(IndoorScope.IsIndoor, Is.False);
				Assert.That(changes, Is.EqualTo(2), "들어설 때와 마지막으로 나갈 때만 알림");
			}
			finally
			{
				IndoorScope.Changed -= Count;
			}
		}

		[Test]
		public void Leaving_twice_is_harmless()
		{
			object room = new();
			IndoorScope.Enter(room);
			IndoorScope.Leave(room);
			IndoorScope.Leave(room);
			Assert.That(IndoorScope.IsIndoor, Is.False);
		}

		private static Gradient DayNight()
		{
			Gradient gradient = new();
			gradient.SetKeys(
				new[] { new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 0f), new GradientColorKey(Color.white, 0.5f), new GradientColorKey(new Color(0.05f, 0.05f, 0.1f), 1f) },
				new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
			return gradient;
		}

		private static float Luminance(Color color) => color.r * .2126f + color.g * .7152f + color.b * .0722f;

		[Test]
		public void Indoor_night_is_dimmer_than_noon_but_brighter_than_outside()
		{
			Gradient ambient = DayNight();
			IndoorLight indoor = IndoorLight.Default;
			float outsideNight = Luminance(ambient.Evaluate(NIGHT));
			float indoorNight = Luminance(indoor.Ambient(ambient, NIGHT));
			float indoorNoon = Luminance(indoor.Ambient(ambient, indoor.NoonTime));
			Assert.That(indoorNight, Is.GreaterThan(outsideNight));
			Assert.That(indoorNight, Is.LessThan(indoorNoon), "밤에는 조금 어두움");
			Assert.That(indoorNight, Is.GreaterThanOrEqualTo(indoor.MinLuminance - 0.001f));
		}

		[Test]
		public void Clock_weight_one_follows_outside()
		{
			Gradient ambient = DayNight();
			IndoorLight indoor = IndoorLight.Default;
			indoor.ClockWeight = 1f;
			indoor.MinLuminance = 0f;
			Assert.That(Luminance(indoor.Ambient(ambient, NIGHT)), Is.EqualTo(Luminance(ambient.Evaluate(NIGHT))).Within(0.001f));
		}
	}
}
