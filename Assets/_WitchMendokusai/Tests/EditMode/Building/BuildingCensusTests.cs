using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 세워진 건물 세기가 <b>엔진 없이도</b> 같은 답을 낸다 (TASK-WM-215).
	/// 「솥이 몇 개냐」는 게임 규칙이고, 서버도 같은 수를 세야 한다.
	/// </summary>
	public sealed class BuildingCensusTests
	{
		private static List<BuildingInstanceData> Village()
		{
			return new List<BuildingInstanceData>
			{
				new BuildingInstanceData(4000),
				new BuildingInstanceData(4000),
				new BuildingInstanceData(4004),
				new BuildingInstanceData(1000),
			};
		}

		[Test]
		public void 같은_번호의_건물을_센다()
		{
			Assert.AreEqual(2, BuildingCensus.CountById(Village(), 4000));
			Assert.AreEqual(1, BuildingCensus.CountById(Village(), 4004));
		}

		[Test]
		public void 없는_건물은_0_이다()
		{
			Assert.AreEqual(0, BuildingCensus.CountById(Village(), 9999));
		}

		[Test]
		public void 목록이_없어도_터지지_않는다()
		{
			Assert.AreEqual(0, BuildingCensus.CountById(null, 4000));
			Assert.AreEqual(0, BuildingCensus.CountAll(null).Count);
		}

		[Test]
		public void 한_번_훑어_전부_센_결과가_하나씩_센_것과_같다()
		{
			Dictionary<int, int> all = BuildingCensus.CountAll(Village());

			Assert.AreEqual(3, all.Count, "서로 다른 건물 종류 수");
			Assert.AreEqual(2, all[4000]);
			Assert.AreEqual(1, all[4004]);
			Assert.AreEqual(1, all[1000]);
		}

		[Test]
		public void 아무것도_안_지었으면_전부_0()
		{
			List<BuildingInstanceData> empty = new List<BuildingInstanceData>();

			Assert.AreEqual(0, BuildingCensus.CountById(empty, 4000));
			Assert.AreEqual(0, BuildingCensus.CountAll(empty).Count);
		}
	}
}
