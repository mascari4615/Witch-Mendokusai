using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 지은 자리마다 솥이 따로 있다 (TASK-WM-217).
	///
	/// ★ 왜: 세계에 솥이 하나면 둘이 동시에 조리할 수 없고, 「솥을 짓는다」가 아무 뜻이 없다.
	/// </summary>
	public sealed class WorldCauldronsTests
	{
		private static readonly Vector3Int Mine = new Vector3Int(2, 0, 2);
		private static readonly Vector3Int Yours = new Vector3Int(9, 0, 9);

		private static BrewStep Push(float dx, float dy) => new BrewStep { Direction = new BrewVector(dx, dy), Grind = 1f };

		[Test]
		public void 자리마다_다른_솥이다()
		{
			WorldCauldrons pots = new WorldCauldrons();
			pots.Place(Mine);
			pots.Place(Yours);

			pots.At(Mine).AddStep(Push(1f, 0f));

			Assert.AreEqual(1, pots.At(Mine).State.StepCount);
			Assert.AreEqual(0, pots.At(Yours).State.StepCount, "남의 솥이 내 손에 따라 움직이면 안 된다");
		}

		[Test]
		public void 없는_자리엔_솥이_없다()
		{
			Assert.IsNull(new WorldCauldrons().At(Mine));
		}

		[Test]
		public void 멀면_못_젓는다()
		{
			WorldCauldrons pots = new WorldCauldrons();
			pots.Place(Mine);

			Assert.IsNull(pots.Reachable(Mine, Mine.x + 50f, Mine.z), "멀리서 젓는 건 판정이 아니다");
			Assert.IsNotNull(pots.Reachable(Mine, Mine.x + 1f, Mine.z));
		}

		[Test]
		public void 다시_놓아도_젓던_것이_안_지워진다()
		{
			WorldCauldrons pots = new WorldCauldrons();
			pots.Place(Mine);
			pots.At(Mine).AddStep(Push(1f, 0f));

			pots.Place(Mine);

			Assert.AreEqual(1, pots.At(Mine).State.StepCount);
		}

		[Test]
		public void 부수면_솥도_사라진다()
		{
			WorldCauldrons pots = new WorldCauldrons();
			pots.Place(Mine);

			Assert.IsTrue(pots.Remove(Mine));
			Assert.IsFalse(pots.Has(Mine));
			Assert.IsFalse(pots.Remove(Mine), "두 번 부술 수는 없다");
		}

		[Test]
		public void 바뀌면_수가_오른다()
		{
			WorldCauldrons pots = new WorldCauldrons();
			int before = pots.Version;

			pots.Place(Mine);
			Assert.Greater(pots.Version, before, "안 오르면 창이 낡은 화면을 그대로 본다");
		}
	}
}
