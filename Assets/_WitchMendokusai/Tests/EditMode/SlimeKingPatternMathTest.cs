using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	public sealed class SlimeKingPatternMathTest
	{
		private const float EPSILON = 0.001f;

		[Test]
		public void Order_CyclesThroughAllPatterns()
		{
			List<SlimeKingPattern> order = new() { SlimeKingPattern.Projectile, SlimeKingPattern.Dash, SlimeKingPattern.Launch, SlimeKingPattern.Pull };
			int index = 0;
			HashSet<SlimeKingPattern> seen = new();
			for (int i = 0; i < order.Count; i++)
			{
				seen.Add(SlimeKingPatternMath.PatternAt(order, index));
				index = SlimeKingPatternMath.NextIndex(index, SlimeKingPatternMath.OrderCount(order));
			}
			Assert.That(seen.Count, Is.EqualTo(order.Count));
			Assert.That(index, Is.Zero);
		}

		[Test]
		public void EmptyOrder_AlternatesProjectileAndDash()
		{
			List<SlimeKingPattern> order = new();
			Assert.That(SlimeKingPatternMath.OrderCount(order), Is.EqualTo(2));
			Assert.That(SlimeKingPatternMath.PatternAt(order, 0), Is.EqualTo(SlimeKingPattern.Projectile));
			Assert.That(SlimeKingPatternMath.PatternAt(order, 1), Is.EqualTo(SlimeKingPattern.Dash));
		}

		[TestCase(3f, 0.8f, 2.4f)]
		[TestCase(5f, 0.5f, 2.5f)]
		[TestCase(3f, 1.5f, 2.97f)]
		public void PullSpeed_StaysBelowWalkSpeed(float walk, float ratio, float expected)
		{
			float speed = SlimeKingPatternMath.PullSpeed(walk, ratio);
			Assert.That(speed, Is.EqualTo(expected).Within(EPSILON));
			Assert.That(speed, Is.LessThan(walk));
		}

		[Test]
		public void PullVelocity_PointsToCenterInsideRadiusOnly()
		{
			Vector3 center = new(0f, 5f, 0f);
			Vector3 inside = SlimeKingPatternMath.PullVelocity(center, new Vector3(4f, 0f, 0f), 9f, 2f);
			Assert.That(inside.x, Is.EqualTo(-2f).Within(EPSILON));
			Assert.That(inside.y, Is.Zero);
			Assert.That(SlimeKingPatternMath.PullVelocity(center, new Vector3(10f, 0f, 0f), 9f, 2f), Is.EqualTo(Vector3.zero));
			Assert.That(SlimeKingPatternMath.PullVelocity(center, new Vector3(0.01f, 0f, 0f), 9f, 2f), Is.EqualTo(Vector3.zero));
		}

		[Test]
		public void LandingPoints_StayInRingAndSpreadAround()
		{
			Vector3 around = new(2f, 0f, -3f);
			const int COUNT = 4;
			List<Vector3> points = new();
			for (int i = 0; i < COUNT; i++)
			{
				Vector3 point = SlimeKingPatternMath.LandingPoint(around, i, COUNT, 30f, 0.5f, i / (float)COUNT, 1.5f, 5f, 0.5f);
				float distance = Vector3.Distance(around, point);
				Assert.That(distance, Is.InRange(1.5f - EPSILON, 5f + EPSILON));
				points.Add(point);
			}
			for (int i = 0; i < COUNT; i++)
			{
				Vector3 from = points[i] - around;
				Vector3 to = points[(i + 1) % COUNT] - around;
				Assert.That(Vector3.Angle(from, to), Is.EqualTo(90f).Within(0.1f));
			}
		}

		[Test]
		public void Arc_StartsEndsOnGroundAndPeaksInMiddle()
		{
			Vector3 from = Vector3.zero;
			Vector3 to = new(4f, 0f, 0f);
			Assert.That(SlimeKingPatternMath.ArcPoint(from, to, 3f, 0f), Is.EqualTo(from));
			Assert.That(SlimeKingPatternMath.ArcPoint(from, to, 3f, 1f), Is.EqualTo(to));
			Assert.That(SlimeKingPatternMath.ArcPoint(from, to, 3f, 0.5f).y, Is.EqualTo(3f).Within(EPSILON));
		}
	}
}
