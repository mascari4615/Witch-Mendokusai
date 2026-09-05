using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214).
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 후속 — 빠르게 움직여도 뚫고 지나가지 않는가 (터널링).
	///
	/// 한 tick 에 자기 몸집보다 멀리 움직이면, 시작점과 끝점만 보는 판정은 그 사이의 벽을 통째로
	/// 놓친다. KCC 의 고전적 실패이고 증상이 잔인하다 — 대시로 벽을 통과하거나, 높은 데서 떨어지면
    /// 바닥을 뚫고 월드 밖으로 사라진다.
	///
	/// 지금 구현은 sweep(CapsuleCast/Raycast) 이 *구간 전체*를 훑으므로 안전하다. 하지만 그건
	/// 지금 그런 것이고, sweep 거리 계산(`fallDistance + SKIN_WIDTH * 2` 같은 식)을 누가 손대면
	/// 조용히 깨진다. 컴파일도 되고 평상시 플레이도 멀쩡하다 — 빠를 때만 뚫린다.
	/// 그래서 「빠를 때」를 명시적으로 박는다.
	/// </summary>
	public sealed class MotorTunnelingTest
	{
		private const float GROUND_TOP_Y = 0f;
		private const float WALL_Z = 0f;

		/// <summary>한 tick 에 10m — 캡슐 지름의 10 배. 벽 두께(1m) 도 훌쩍 넘는다.</summary>
		private const float ABSURD_SPEED = 500f;

		[Test]
		public void MovingAbsurdlyFastIntoWall_IsStopped_DoesNotTunnel()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, GROUND_TOP_Y, -5f).ToUnity()))
			{
				harness.AddGround(new Vector3(0f, GROUND_TOP_Y - 0.5f, -5f).ToUnity(), new Vector3(20f, 1f, 20f).ToUnity());
				// z=0 에 두께 1m 벽. 한 tick 이동(10m)보다 훨씬 얇다.
				harness.AddGround(new Vector3(0f, 1.5f, WALL_Z + 0.5f).ToUnity(), new Vector3(20f, 3f, 1f).ToUnity());
				harness.AddContributor(new ConstantHorizontalContributor(harness));
				harness.AddContributor(new GravityContributor());

				harness.SetHorizontalIntent(Vector3.forward.ToUnity(), ABSURD_SPEED);
				harness.Step();

				Assert.That(harness.Position.z, Is.LessThan(WALL_Z),
					$"한 tick 10m 이동이 두께 1m 벽을 뚫고 지나갔다 (z={harness.Position.z}) — " +
					"sweep 이 구간 전체가 아니라 끝점만 보고 있다");
			}
		}

		/// <summary>
		/// 아주 빠른 낙하. 뚫으면 캐릭터가 월드 밖으로 사라지고, 그 뒤엔 아무 판정도 안 남는다.
		/// (높은 곳에서 떨어지는 건 실제로 자주 일어난다 — 절벽·공중 플랫폼·리스폰.)
		/// </summary>
		[Test]
		public void FallingAbsurdlyFast_LandsOnGround_DoesNotFallThrough()
		{
			using (MotorTestHarness harness = new(new Vector3(0f, 30f, 0f).ToUnity()))
			{
				harness.AddGround(new Vector3(0f, GROUND_TOP_Y - 0.5f, 0f).ToUnity(), new Vector3(40f, 1f, 40f).ToUnity());
				harness.AddContributor(new GravityContributor());

				// 중력이 붙기 전에 이미 말도 안 되는 속도를 준다 = 한 tick 에 10m 낙하.
				harness.SetVerticalVelocity(-ABSURD_SPEED);

				float lowest = harness.Position.y;
				for (int step = 0; step < 60; step++)
				{
					harness.Step();
					lowest = Mathf.Min(lowest, harness.Position.y);
				}

				Assert.That(lowest, Is.GreaterThanOrEqualTo(GROUND_TOP_Y - 0.05f),
					$"바닥을 뚫고 내려갔다 (최저 y={lowest}) — 빠른 낙하에서 vertical sweep 이 구간을 놓친다");
				Assert.That(harness.IsGrounded, Is.True, "빠르게 떨어진 뒤 접지 못 했다");
				Assert.That(harness.Position.y, Is.EqualTo(GROUND_TOP_Y).Within(0.05f), "착지 높이가 어긋났다");
			}
		}
	}
}
