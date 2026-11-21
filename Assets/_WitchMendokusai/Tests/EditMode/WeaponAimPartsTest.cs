using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 조준 부품 순수 계산. 마지막 이동 방향 따라돌기, 밀집 지점, 부채꼴 각도
	public class WeaponAimPartsTest
	{
		[Test]
		public void MoveAim_keeps_last_direction_when_input_stops()
		{
			MoveAimTracker tracker = new();
			tracker.Tick(Vector3.right, 15f, 0.016f);
			tracker.Tick(Vector3.zero, 15f, 0.016f);
			tracker.Tick(Vector3.zero, 15f, 1f);
			Assert.That(Vector3.Distance(tracker.Current, Vector3.right), Is.LessThan(0.001f));
		}

		[Test]
		public void MoveAim_first_direction_snaps()
		{
			MoveAimTracker tracker = new();
			tracker.Tick(new Vector3(0f, 0f, -1f), 15f, 0.016f);
			Assert.That(Vector3.Distance(tracker.Current, Vector3.back), Is.LessThan(0.001f));
		}

		[Test]
		public void MoveAim_turns_gradually_and_converges()
		{
			MoveAimTracker tracker = new();
			tracker.Tick(Vector3.right, 15f, 0.016f);
			tracker.Tick(Vector3.forward, 15f, 0.016f);
			float afterOneFrame = Vector3.Angle(tracker.Current, Vector3.forward);
			Assert.That(afterOneFrame, Is.GreaterThan(60f).And.LessThan(90f), "한 프레임에 다 돌지 않음");
			// 0.15초면 약 89% 도달
			tracker.Tick(Vector3.forward, 15f, 0.15f);
			Assert.That(Vector3.Angle(tracker.Current, Vector3.forward), Is.LessThan(10f));
			for (int i = 0; i < 60; i++)
				tracker.Tick(Vector3.forward, 15f, 0.016f);
			Assert.That(Vector3.Angle(tracker.Current, Vector3.forward), Is.LessThan(0.01f));
		}

		[Test]
		public void MoveAim_reverse_direction_turns_without_flipping_to_zero()
		{
			MoveAimTracker tracker = new();
			tracker.Tick(Vector3.right, 15f, 0.016f);
			tracker.Tick(Vector3.left, 15f, 0.05f);
			Assert.That(tracker.Current.magnitude, Is.EqualTo(1f).Within(0.001f));
		}

		[Test]
		public void MoveAim_zero_rate_follows_instantly()
		{
			MoveAimTracker tracker = new();
			tracker.Tick(Vector3.right, 0f, 0.016f);
			tracker.Tick(Vector3.forward, 0f, 0.016f);
			Assert.That(Vector3.Distance(tracker.Current, Vector3.forward), Is.LessThan(0.001f));
		}

		[Test]
		public void Density_picks_largest_group_not_nearest()
		{
			List<Vector3> positions = new()
			{
				new Vector3(1f, 0f, 0f),
				new Vector3(8f, 0f, 8f), new Vector3(8.5f, 0f, 8f), new Vector3(8f, 0f, 8.5f), new Vector3(9f, 0f, 8f),
			};
			bool found = DensityTargeting.TryFindDensest(positions, Vector3.zero, 14f, 2f, 1, out Vector3 point, out int count);
			Assert.That(found, Is.True);
			Assert.That(count, Is.EqualTo(4));
			Assert.That(Vector3.Distance(point, new Vector3(8.375f, 0f, 8.125f)), Is.LessThan(0.01f));
		}

		[Test]
		public void Density_group_split_across_a_grid_line_still_counts_whole()
		{
			// 0 근처 격자 경계를 가로지르는 무리
			List<Vector3> positions = new() { new Vector3(-0.2f, 0f, 5f), new Vector3(0.2f, 0f, 5f), new Vector3(0f, 0f, 5.3f) };
			Assert.That(DensityTargeting.TryFindDensest(positions, Vector3.zero, 14f, 1f, 1, out _, out int count), Is.True);
			Assert.That(count, Is.EqualTo(3));
		}

		[Test]
		public void Density_tie_prefers_nearer_to_origin()
		{
			List<Vector3> positions = new() { new Vector3(10f, 0f, 0f), new Vector3(3f, 0f, 0f) };
			DensityTargeting.TryFindDensest(positions, Vector3.zero, 14f, 1f, 1, out Vector3 point, out _);
			Assert.That(point.x, Is.EqualTo(3f).Within(0.001f));
		}

		[Test]
		public void Density_ignores_out_of_range_and_respects_min_count()
		{
			List<Vector3> positions = new() { new Vector3(30f, 0f, 0f), new Vector3(30f, 0f, 0.5f) };
			Assert.That(DensityTargeting.TryFindDensest(positions, Vector3.zero, 14f, 2f, 1, out _, out _), Is.False);
			positions.Add(new Vector3(2f, 0f, 0f));
			Assert.That(DensityTargeting.TryFindDensest(positions, Vector3.zero, 14f, 2f, 2, out _, out _), Is.False, "무리 최소 2마리 미달");
			Assert.That(DensityTargeting.TryFindDensest(positions, Vector3.zero, 14f, 2f, 1, out _, out int count), Is.True);
			Assert.That(count, Is.EqualTo(1));
		}

		[Test]
		public void Density_empty_fails()
		{
			Assert.That(DensityTargeting.TryFindDensest(new List<Vector3>(), Vector3.zero, 14f, 2f, 1, out _, out _), Is.False);
		}

		[Test]
		public void FanAngle_is_symmetric_and_capped()
		{
			Assert.That(FanDashMovement.FanAngle(0, 1, 30f, 180f), Is.EqualTo(0f));
			Assert.That(FanDashMovement.FanAngle(0, 3, 30f, 180f), Is.EqualTo(-30f).Within(0.001f));
			Assert.That(FanDashMovement.FanAngle(2, 3, 30f, 180f), Is.EqualTo(30f).Within(0.001f));
			// 칼 10자루는 30도씩이면 270도, 상한 180도로 줄임
			float first = FanDashMovement.FanAngle(0, 10, 30f, 180f);
			float last = FanDashMovement.FanAngle(9, 10, 30f, 180f);
			Assert.That(last - first, Is.EqualTo(180f).Within(0.001f));
		}
	}
}
