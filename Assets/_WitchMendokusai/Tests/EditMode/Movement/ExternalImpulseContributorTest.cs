using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TASK-WM-049 후속 — 대시·넉백 채널(<see cref="ExternalImpulseContributor"/>) 회귀 락.
	///
	/// 게임에서 실제로 쓰이는데 시험이 0 이었다. 여기가 어긋나면 「맞았는데 안 밀린다」,
	/// 「대시가 끝났는데 계속 미끄러진다」, 「대시 중에 입력이 먹혀 방향이 꺾인다」가 된다.
	/// 셋 다 조작감 문제로 보고되지 이 클래스로 추적되지 않는 종류다.
	///
	/// ★ 여기서 드러내는 구조 하나: 이 contributor 는 임펄스가 끝나도 <c>Velocity.x/z</c> 를 스스로
	///   0 으로 돌리지 않는다. 뒤에 오는 입력 contributor 가 매 tick 덮어써 주는 것에 기댄다.
	///   그 순서 계약이 깨지면 임펄스가 영영 안 멈춘다 — 마지막 시험이 그 자리를 지킨다.
	/// </summary>
	public sealed class ExternalImpulseContributorTest
	{
		private const float DELTA_TIME = MotorTestHarness.FIXED_DELTA_TIME;
		private const float VELOCITY_TOLERANCE = 0.001f;

		private static ExternalImpulseContributor Pushed(out MotorContext context, float duration)
		{
			context = new MotorContext();
			ExternalImpulseContributor impulse = new();
			impulse.Push(new Vector3(5f, 0f, 0f), duration);
			return impulse;
		}

		/// <summary>미는 동안은 수평 속도를 덮고, 「외부가 몰고 있다」고 표시해야 한다.</summary>
		[Test]
		public void WhilePushing_DrivesHorizontalVelocity_AndRaisesFlag()
		{
			ExternalImpulseContributor impulse = Pushed(out MotorContext context, DELTA_TIME * 5f);

			impulse.Contribute(context, DELTA_TIME);

			Assert.That(impulse.IsActive, Is.True, "밀고 있는데 IsActive 가 false");
			Assert.That(context.IsExternallyDriven, Is.True,
				"IsExternallyDriven 이 안 섰다 — 입력·점프가 이 플래그를 보고 비켜준다");
			Assert.That(context.Velocity.x, Is.EqualTo(5f).Within(VELOCITY_TOLERANCE));
		}

		/// <summary>시간이 다하면 손을 떼고 플래그를 내려야 한다. 안 내리면 입력이 영영 안 돌아온다.</summary>
		[Test]
		public void AfterDurationElapses_ReleasesControl_AndClearsFlag()
		{
			const int PUSH_TICKS = 5;
			ExternalImpulseContributor impulse = Pushed(out MotorContext context, DELTA_TIME * PUSH_TICKS);

			for (int i = 0; i < PUSH_TICKS; i++)
				impulse.Contribute(context, DELTA_TIME);

			Assert.That(impulse.IsActive, Is.False, $"{PUSH_TICKS} tick 뒤에도 살아있다 — 시간 차감이 안 맞는다");

			// 손 뗀 뒤 tick — 더는 속도를 건드리지 않아야 한다.
			context.Velocity.x = 123f;
			impulse.Contribute(context, DELTA_TIME);

			Assert.That(context.IsExternallyDriven, Is.False, "끝났는데 플래그가 남았다 — 입력이 영영 안 돌아온다");
			Assert.That(context.Velocity.x, Is.EqualTo(123f).Within(VELOCITY_TOLERANCE),
				"끝났는데 아직 수평 속도를 덮고 있다");
		}

		/// <summary>취소는 즉시 먹어야 한다 (경직 해제·사망 등에서 부른다).</summary>
		[Test]
		public void Cancel_StopsImmediately()
		{
			ExternalImpulseContributor impulse = Pushed(out MotorContext context, 10f);

			impulse.Cancel();
			context.Velocity.x = 7f;
			impulse.Contribute(context, DELTA_TIME);

			Assert.That(impulse.IsActive, Is.False);
			Assert.That(context.IsExternallyDriven, Is.False);
			Assert.That(context.Velocity.x, Is.EqualTo(7f).Within(VELOCITY_TOLERANCE), "취소했는데 아직 민다");
		}

		/// <summary>단일 슬롯(latest wins) — 클래스 주석이 명시한 설계다. 합성이 필요해지면 여기가 먼저 RED 가 된다.</summary>
		[Test]
		public void SecondPush_ReplacesFirst_LatestWins()
		{
			MotorContext context = new();
			ExternalImpulseContributor impulse = new();

			impulse.Push(new Vector3(5f, 0f, 0f), 10f);
			impulse.Push(new Vector3(-2f, 0f, 0f), 10f);
			impulse.Contribute(context, DELTA_TIME);

			Assert.That(context.Velocity.x, Is.EqualTo(-2f).Within(VELOCITY_TOLERANCE),
				"두 임펄스가 합쳐졌다 — 단일 슬롯 설계가 깨졌다");
		}

		/// <summary>
		/// 수직 성분은 버린다. 넉백에 y 가 실려 들어와도 이 채널로는 사람이 떠오르지 않는다 —
		/// 위로 뜨는 건 점프 채널 몫이고, 섞이면 중력·접지 판정과 싸운다.
		/// </summary>
		[Test]
		public void Push_DropsVerticalComponent()
		{
			MotorContext context = new();
			ExternalImpulseContributor impulse = new();

			impulse.Push(new Vector3(3f, 99f, 0f), 10f);
			impulse.Contribute(context, DELTA_TIME);

			Assert.That(context.Velocity.y, Is.EqualTo(0f).Within(VELOCITY_TOLERANCE),
				"임펄스가 수직 속도를 실었다 — 이 채널은 수평 전용이다");
		}

		/// <summary>
		/// 실제로 캐릭터를 밀고, 끝나면 멈추는가. 순서 계약(임펄스 → 입력)까지 같이 건다:
		/// 미는 동안엔 입력이 비켜주고, 끝나면 입력이 잔여 속도를 걷어가 멈춘다.
		/// </summary>
		[Test]
		public void PushedImpulse_MovesCharacter_ThenStopsWhenInputTakesOver()
		{
			const int PUSH_TICKS = 5;
			const float PUSH_SPEED = 5f;

			using (MotorTestHarness harness = new(new Vector3(0f, 0f, 0f)))
			{
				harness.AddGround(new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));

				ExternalImpulseContributor impulse = new();
				harness.AddContributor(impulse);                                  // 임펄스가 먼저
				harness.AddContributor(new ConstantHorizontalContributor(harness)); // 입력이 나중 (실물 등록 순서)
				harness.AddContributor(new GravityContributor());
				harness.SetHorizontalIntent(Vector3.zero, 0f); // 입력 없음

				harness.Step(); // 접지 확정
				float startX = harness.Position.x;

				impulse.Push(new Vector3(PUSH_SPEED, 0f, 0f), DELTA_TIME * PUSH_TICKS);
				harness.StepMany(PUSH_TICKS);

				float pushedX = harness.Position.x;
				Assert.That(pushedX - startX, Is.EqualTo(PUSH_SPEED * DELTA_TIME * PUSH_TICKS).Within(0.02f),
					"밀린 거리가 speed × duration 과 다르다");

				harness.StepMany(30);

				Assert.That(harness.Position.x, Is.EqualTo(pushedX).Within(0.02f),
					$"임펄스가 끝났는데 계속 미끄러진다 ({pushedX} → {harness.Position.x}) — " +
					"이 contributor 는 잔여 속도를 스스로 안 지운다. 뒤에 오는 입력이 걷어가는 순서 계약이 깨졌다");
				Assert.That(harness.IsGrounded, Is.True, "밀리는 동안 접지를 잃었다");
			}
		}
	}
}
