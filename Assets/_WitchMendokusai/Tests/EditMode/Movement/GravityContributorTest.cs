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
	/// TASK-WM-049 후속 — 중력 채널(<see cref="GravityContributor"/>) 계약 락.
	///
	/// 25 줄짜리라 눈으로 보면 다 보이지만, **오늘(2026-08-05) 잡은 「떠서 걷는」 결함의 메커니즘이
	/// 바로 여기였다**: Grounded 인 동안 중력을 0 으로 눌러버리므로, 접지 판정이 실제보다 관대하면
	/// 캐릭터가 공중에 뜬 채로 아무 힘도 안 받는다. 그때 아무도 캐릭터를 내려놓지 않는다.
	///
	/// 즉 이 클래스의 계약은 「접지 판정이 정확하다」는 전제 위에서만 옳다. 그 전제는
	/// <see cref="Motor"/> 의 ResolveGround 가 지키고(TASK-WM-029-B), 여기서는 *이쪽 절반*을 잠근다.
	/// 특히 세 번째 시험(위로 솟는 중엔 손대지 않는다)이 중요하다 — 그게 깨지면 점프가 즉사한다.
	/// </summary>
	public sealed class GravityContributorTest
	{
		private const float DELTA_TIME = MotorTestHarness.FIXED_DELTA_TIME;
		private const float TOLERANCE = 0.0001f;

		private static MotorContext Context(MotorGroundState groundState, float verticalVelocity)
		{
			MotorContext context = new();
			context.GroundState = groundState;
			context.Velocity = new Vector3(0f, verticalVelocity, 0f).ToUnity();
			return context;
		}

		/// <summary>공중에서는 tick 마다 g·dt 가 쌓인다. 이게 낙하의 전부다.</summary>
		[Test]
		public void Airborne_AccumulatesGravityEveryTick()
		{
			GravityContributor gravity = new();
			MotorContext context = Context(MotorGroundState.Airborne, 0f);

			const int TICKS = 5;
			for (int i = 0; i < TICKS; i++)
				gravity.Contribute(context, DELTA_TIME);

			float expected = Physics.gravity.y * DELTA_TIME * TICKS;
			Assert.That(context.Velocity.y, Is.EqualTo(expected).Within(TOLERANCE),
				"공중에서 중력이 tick 당 g·dt 로 안 쌓인다");
		}

		/// <summary>
		/// 접지 상태에서 아래로 향하던 속도는 0 으로 눌린다. 안 누르면 매 tick 지면을 향해 쌓여
		/// 언젠가 바닥을 뚫는다.
		///
		/// ★ 단, 이 누름이 「관대한 접지 판정」과 만나면 공중 정지가 된다 — 2026-08-05 결함의 뿌리.
		///   그래서 접지 판정 쪽(ResolveGround)이 발끝에 실제로 닿았을 때만 Grounded 를 준다.
		/// </summary>
		[Test]
		public void Grounded_ClampsDownwardVelocityToZero()
		{
			GravityContributor gravity = new();
			MotorContext context = Context(MotorGroundState.Grounded, -12f);

			gravity.Contribute(context, DELTA_TIME);

			Assert.That(context.Velocity.y, Is.EqualTo(0f).Within(TOLERANCE),
				"접지인데 아래로 향한 속도가 남았다 — 다음 tick 에 바닥을 파고든다");
		}

		/// <summary>
		/// 접지 상태라도 *위로* 솟는 중이면 손대지 않는다. 점프한 그 tick 은 아직 Grounded 로
		/// 보이는데, 여기서 vy 를 건드리면 점프가 발밑에서 즉사한다.
		/// </summary>
		[Test]
		public void Grounded_ButRising_LeavesUpwardVelocityAlone()
		{
			GravityContributor gravity = new();
			MotorContext context = Context(MotorGroundState.Grounded, 6f);

			gravity.Contribute(context, DELTA_TIME);

			Assert.That(context.Velocity.y, Is.EqualTo(6f).Within(TOLERANCE),
				"접지 tick 에 상승 속도를 깎았다 — 점프가 발밑에서 죽는다");
		}

		/// <summary>접지 + 정지 상태에서는 아무 일도 일어나지 않는다. 서 있는 동안 가라앉지 않는다.</summary>
		[Test]
		public void Grounded_AtRest_DoesNotAccumulate()
		{
			GravityContributor gravity = new();
			MotorContext context = Context(MotorGroundState.Grounded, 0f);

			for (int i = 0; i < 10; i++)
				gravity.Contribute(context, DELTA_TIME);

			Assert.That(context.Velocity.y, Is.EqualTo(0f).Within(TOLERANCE),
				"서 있는데 중력이 쌓인다 — 접지 판정이 매 tick 이걸 지워주는 것에 기대게 된다");
		}
	}
}
