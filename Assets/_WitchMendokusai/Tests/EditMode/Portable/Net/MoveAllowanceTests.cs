using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 걸음은 <b>시간에</b> 묶인다 (TASK-WM-222) — 빨리 보낸다고 빨리 가지지 않는다.
	/// </summary>
	public class MoveAllowanceTests
	{
		[Test]
		public void 한_번에_한_걸음은_그대로_간다()
		{
			MoveAllowance judge = new MoveAllowance();

			Vector3 granted = judge.Allow(1, 0L, new Vector3(0.15f, 0f, 0f));

			Assert.AreEqual(0.15f, granted.magnitude, 0.001f, "정상 창의 한 걸음이 잘리면 안 된다");
		}

		[Test]
		public void 쉬지_않고_퍼부어도_걸음_속도를_못_넘는다()
		{
			MoveAllowance judge = new MoveAllowance();
			float went = 0f;

			// 같은 순간(0ms)에 1000번 — 시계가 안 흘렀으니 지갑도 안 채워진다.
			for (int i = 0; i < 1000; i++)
				went += judge.Allow(1, 0L, new Vector3(1.5f, 0f, 0f)).magnitude;

			Assert.LessOrEqual(went, MoveAllowance.BURST_DISTANCE + 0.001f,
				$"시계가 안 흘렀는데 {went}m 나 갔다 — 순간이동이다");
		}

		[Test]
		public void 일_초_동안_퍼부어도_허락된_속도까지만_간다()
		{
			MoveAllowance judge = new MoveAllowance();
			float went = 0f;

			// 1초를 1ms 씩 쪼개 매번 1.5m 를 조른다 = 초당 1500m 를 시도한다.
			for (long ms = 0; ms <= 1000; ms++)
				went += judge.Allow(1, ms, new Vector3(1.5f, 0f, 0f)).magnitude;

			float ceiling = MoveAllowance.BURST_DISTANCE + MoveAllowance.ALLOWED_SPEED;
			Assert.LessOrEqual(went, ceiling + 0.01f, $"1초에 {went}m — 걸어서 갈 수 있는 거리가 아니다");
			Assert.Greater(went, MoveAllowance.WALK_SPEED * 0.9f, "정상 속도까지는 갈 수 있어야 한다");
		}

		[Test]
		public void 밀렸다_몰려온_걸음은_받아_준다()
		{
			MoveAllowance judge = new MoveAllowance();
			judge.Allow(1, 0L, new Vector3(0.15f, 0f, 0f));

			// 회선이 400ms 밀렸다 — 그동안 걸은 만큼(1.2m)이 한꺼번에 도착한다.
			Vector3 granted = judge.Allow(1, 400L, new Vector3(1.2f, 0f, 0f));

			Assert.AreEqual(1.2f, granted.magnitude, 0.01f, "회선이 나쁜 사람이 느려지면 안 된다");
		}

		[Test]
		public void 방향은_그대로_두고_길이만_줄인다()
		{
			MoveAllowance judge = new MoveAllowance();
			judge.Allow(1, 0L, new Vector3(MoveAllowance.BURST_DISTANCE, 0f, 0f));

			Vector3 granted = judge.Allow(1, 100L, new Vector3(3f, 0f, 4f));

			Assert.Greater(granted.magnitude, 0f, "조금은 가야 한다");
			Assert.AreEqual(3f / 4f, granted.x / granted.z, 0.001f, "잘렸다고 방향이 틀어지면 안 된다");
		}

		[Test]
		public void 시계가_뒤로_가도_지갑이_안_채워진다()
		{
			MoveAllowance judge = new MoveAllowance();
			judge.Allow(1, 10000L, new Vector3(MoveAllowance.BURST_DISTANCE, 0f, 0f));

			float went = 0f;
			for (int i = 0; i < 100; i++)
				went += judge.Allow(1, 0L, new Vector3(1.5f, 0f, 0f)).magnitude;

			Assert.LessOrEqual(went, 0.001f, "시계를 되감아 지갑을 채우면 안 된다");
		}

		[Test]
		public void 사람마다_지갑이_따로다()
		{
			MoveAllowance judge = new MoveAllowance();
			for (int i = 0; i < 100; i++)
				judge.Allow(1, 0L, new Vector3(1.5f, 0f, 0f));

			Vector3 granted = judge.Allow(2, 0L, new Vector3(0.15f, 0f, 0f));

			Assert.AreEqual(0.15f, granted.magnitude, 0.001f, "남이 뛴다고 내가 못 걸으면 안 된다");
		}

		[Test]
		public void 나간_사람의_지갑은_버린다()
		{
			MoveAllowance judge = new MoveAllowance();
			judge.Allow(1, 0L, new Vector3(0.15f, 0f, 0f));
			judge.Allow(2, 0L, new Vector3(0.15f, 0f, 0f));

			judge.Forget(1);

			Assert.AreEqual(1, judge.Count, "나간 사람 지갑이 남으면 사람 수만큼 쌓인다");
		}
	}
}
