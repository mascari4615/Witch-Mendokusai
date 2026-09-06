using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 짓기 목록은 <b>세계가 아는 것</b>이고, 재료가 보인다 (TASK-WM-217).
	///
	/// ★ 왜: 자기 자산을 늘어놓으면 세계가 모르는 것을 고를 수 있고, 그건 내 화면에만 섰다가 사라진다.
	///   재료가 안 보이면 왜 안 지어지는지도 알 수 없다.
	/// </summary>
	public sealed class BuildAffordabilityTests
	{
		private static BuildingCatalogEntry Entry(int id, string name, int costItemId, int costAmount)
		{
			return new BuildingCatalogEntry
			{
				id = id, name = name, w = 1, l = 1, costItemId = costItemId, costAmount = costAmount,
			};
		}

		private static System.Func<int, int> Bag(params (int itemId, int amount)[] carried)
		{
			Dictionary<int, int> bag = new Dictionary<int, int>();
			for (int i = 0; i < carried.Length; i++)
				bag[carried[i].itemId] = carried[i].amount;

			return itemId => bag.TryGetValue(itemId, out int amount) ? amount : 0;
		}

		[Test]
		public void 세계가_아는_것만_목록에_오른다()
		{
			List<BuildOption> options = BuildAffordability.Options(
				new[] { Entry(4000, "솥", 0, 2) }, Bag());

			Assert.AreEqual(1, options.Count);
			Assert.AreEqual(4000, options[0].BuildingId);
			Assert.AreEqual("솥", options[0].Name);
		}

		[Test]
		public void 재료가_모자라면_못_짓는_칸이다()
		{
			List<BuildOption> options = BuildAffordability.Options(
				new[] { Entry(4000, "솥", 0, 2) }, Bag((0, 1)));

			Assert.IsFalse(options[0].Affordable);
			Assert.AreEqual(1, options[0].Carrying, "들고 있는 수가 안 보이면 사람은 얼마가 더 필요한지 모른다");
		}

		[Test]
		public void 재료가_되면_지을_수_있는_칸이다()
		{
			List<BuildOption> options = BuildAffordability.Options(
				new[] { Entry(4000, "솥", 0, 2) }, Bag((0, 5)));

			Assert.IsTrue(options[0].Affordable);
		}

		[Test]
		public void 나무는_0번이라도_진짜_재료다()
		{
			// ⚠ 번호 0 을 「없음」으로 거르면 나무로 짓는 것이 전부 공짜가 된다(하루에 세 번 밟은 함정).
			List<BuildOption> options = BuildAffordability.Options(
				new[] { Entry(1, "마녀의 집", 0, 2) }, Bag());

			Assert.IsFalse(options[0].Affordable, "빈손인데 지을 수 있다고 하면 줍기가 뜻을 잃는다");
			Assert.AreEqual(2, options[0].CostAmount);
		}

		[Test]
		public void 공짜인_것은_빈손으로도_짓는다()
		{
			List<BuildOption> options = BuildAffordability.Options(
				new[] { Entry(2, "임시 블럭", 0, 0) }, Bag());

			Assert.IsTrue(options[0].Affordable);
			Assert.AreEqual(string.Empty, BuildAffordability.CostText(options[0], id => "나무"),
				"공짜인 것에 「나무 0/0」을 붙이면 사람이 재료가 든다고 읽는다");
		}

		[Test]
		public void 재료_표시는_이름과_수를_같이_보여_준다()
		{
			List<BuildOption> options = BuildAffordability.Options(
				new[] { Entry(4000, "솥", 0, 2) }, Bag((0, 1)));

			Assert.AreEqual("나무 1/2", BuildAffordability.CostText(options[0], id => id == 0 ? "나무" : null));
		}

		[Test]
		public void 세계가_아직_목록을_안_줬으면_빈_목록이다()
		{
			Assert.AreEqual(0, BuildAffordability.Options(null, Bag()).Count,
				"목록을 못 받았을 때 자기 자산으로 채우면 세계가 모르는 것을 짓게 된다");
		}
	}
}
