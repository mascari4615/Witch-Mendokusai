using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-166 Phase 2 INC-7 — <see cref="CommutePathFollower"/> 경로 추종(왕복) 회귀 잠금.
	///
	/// 셀 경로 위 progress 전진/양끝 ping-pong/세그먼트 보간. 순수(좌표 산술만). new() + Assert.That.
	///
	/// 실행: unity -runTests -batchmode -testPlatform EditMode -assemblyNames WM.Tests.EditMode
	/// </summary>
	public sealed class CommutePathFollowerTest
	{
		private static List<Vector3Int> Line(int length)
		{
			List<Vector3Int> path = new();
			for (int i = 0; i < length; i++)
			{
				path.Add(new Vector3Int(i, 0, 0));
			}

			return path;
		}

		[Test]
		public void Advance_MovesForward()
		{
			CommutePathFollower follower = new(Line(4));

			follower.Advance(1.5f);

			Assert.That(follower.Progress, Is.EqualTo(1.5f).Within(0.0001f));
			Assert.That(follower.Direction, Is.EqualTo(1));
		}

		[Test]
		public void Advance_PingPongsAtEnd()
		{
			CommutePathFollower follower = new(Line(4)); // max progress = 3

			follower.Advance(10f); // 끝 초과

			Assert.That(follower.Progress, Is.EqualTo(3f).Within(0.0001f), "끝에서 clamp");
			Assert.That(follower.Direction, Is.EqualTo(-1), "방향 반전(집으로)");
		}

		[Test]
		public void Advance_PingPongsBackToStart()
		{
			CommutePathFollower follower = new(Line(4));
			follower.Advance(10f); // 끝 도달, direction = -1
			follower.Advance(10f); // 시작으로 되돌아 clamp

			Assert.That(follower.Progress, Is.EqualTo(0f).Within(0.0001f), "시작에서 clamp");
			Assert.That(follower.Direction, Is.EqualTo(1), "방향 다시 정방향");
		}

		[Test]
		public void CurrentSegment_Interpolates()
		{
			CommutePathFollower follower = new(Line(4));
			follower.Advance(1.5f);

			follower.CurrentSegment(out Vector3Int fromCell, out Vector3Int toCell, out float t);

			Assert.That(fromCell, Is.EqualTo(new Vector3Int(1, 0, 0)), "lower 셀");
			Assert.That(toCell, Is.EqualTo(new Vector3Int(2, 0, 0)), "다음 셀");
			Assert.That(t, Is.EqualTo(0.5f).Within(0.0001f), "셀 사이 보간 t");
		}

		[Test]
		public void SingleCellPath_Stays()
		{
			CommutePathFollower follower = new(Line(1));

			follower.Advance(5f);

			Assert.That(follower.Progress, Is.EqualTo(0f), "단일 셀 = 정지");
			follower.CurrentSegment(out Vector3Int fromCell, out Vector3Int toCell, out float t);
			Assert.That(fromCell, Is.EqualTo(new Vector3Int(0, 0, 0)));
			Assert.That(toCell, Is.EqualTo(new Vector3Int(0, 0, 0)));
		}
	}
}
