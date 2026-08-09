using NUnit.Framework;
using WitchMendokusai.Net;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests.EditMode.Net
{
	/// <summary>
	/// 서버 권위의 빠진 반쪽 (TASK-WM-217) — 내 화면의 나만 앞서가지 않게.
	/// </summary>
	public sealed class PositionCorrectionTests
	{
		[Test]
		public void 조금_다른_건_그냥_둔다()
		{
			PositionCorrection.Action action = PositionCorrection.Resolve(
				new Vector3(0f, 1f, 0f), new Vector3(0.2f, 0f, 0f), 0.02f, out Vector3 corrected);

			// 20Hz 스냅샷 사이의 정상적인 차이 — 여기서 당기면 늘 떨린다.
			Assert.That(action, Is.EqualTo(PositionCorrection.Action.Keep));
			Assert.That(corrected.x, Is.EqualTo(0f));
		}

		[Test]
		public void 벌어지면_슬쩍_당긴다()
		{
			PositionCorrection.Action action = PositionCorrection.Resolve(
				new Vector3(0f, 1f, 0f), new Vector3(2f, 0f, 0f), 0.1f, out Vector3 corrected);

			Assert.That(action, Is.EqualTo(PositionCorrection.Action.Pull));
			Assert.That(corrected.x, Is.GreaterThan(0f));
			Assert.That(corrected.x, Is.LessThan(2f));
		}

		[Test]
		public void 많이_벌어지면_바로_옮긴다()
		{
			PositionCorrection.Action action = PositionCorrection.Resolve(
				Vector3.zero, new Vector3(50f, 0f, 0f), 0.1f, out Vector3 corrected);

			Assert.That(action, Is.EqualTo(PositionCorrection.Action.Snap));
			Assert.That(corrected.x, Is.EqualTo(50f));
		}

		[Test]
		public void 높이는_건드리지_않는다()
		{
			PositionCorrection.Resolve(new Vector3(0f, 7.5f, 0f), new Vector3(50f, 0f, 0f), 0.1f, out Vector3 snapped);
			PositionCorrection.Resolve(new Vector3(0f, 7.5f, 0f), new Vector3(2f, 0f, 0f), 0.1f, out Vector3 pulled);

			// 지형·점프는 세계가 모르는 값이다 — 끌어당기다 땅에 박히면 안 된다.
			Assert.That(snapped.y, Is.EqualTo(7.5f));
			Assert.That(pulled.y, Is.EqualTo(7.5f));
		}

		[Test]
		public void 한_번에_지나치지_않는다()
		{
			// 프레임이 길어도(dt 큼) 목표를 넘어가면 진동한다.
			PositionCorrection.Resolve(Vector3.zero, new Vector3(2f, 0f, 0f), 10f, out Vector3 corrected);

			Assert.That(corrected.x, Is.EqualTo(2f).Within(0.0001f));
		}
	}
}
