using System.Collections.Generic;
using NUnit.Framework;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Tests
{
	/// <summary>세계에 선 것을 화면이 따라 그린다 (TASK-WM-217).</summary>
	public sealed class BuildingFollowPlanTests
	{
		private static Vector3Int At(int x, int z) => new Vector3Int(x, 0, z);

		private readonly List<Vector3Int> toSpawn = new List<Vector3Int>();
		private readonly List<Vector3Int> toDespawn = new List<Vector3Int>();

		[Test]
		public void 남이_지은_집이_화면에_선다()
		{
			BuildingFollowPlan.Compute(
				new[] { At(1, 1), At(5, 5) },
				new[] { At(1, 1) },
				null, toSpawn, toDespawn);

			Assert.AreEqual(1, toSpawn.Count);
			Assert.AreEqual(At(5, 5), toSpawn[0], "세계엔 있는데 화면에 없으면 세워야 한다");
			Assert.AreEqual(0, toDespawn.Count);
		}

		[Test]
		public void 남이_부순_집은_내_화면에서도_사라진다()
		{
			BuildingFollowPlan.Compute(
				new Vector3Int[0],
				new[] { At(2, 2) },
				null, toSpawn, toDespawn);

			Assert.AreEqual(1, toDespawn.Count, "내 화면에만 남으면 그 자리에 다시 못 짓는다");
			Assert.AreEqual(At(2, 2), toDespawn[0]);
		}

		[Test]
		public void 방금_내가_세운_집은_안_지운다()
		{
			// 아직 세계의 답이 안 왔다 — 여기서 지우면 짓자마자 깜빡이며 사라진다.
			BuildingFollowPlan.Compute(
				new Vector3Int[0],
				new[] { At(3, 3) },
				new[] { At(3, 3) },
				toSpawn, toDespawn);

			Assert.AreEqual(0, toDespawn.Count);
		}

		[Test]
		public void 같으면_아무것도_안_한다()
		{
			BuildingFollowPlan.Compute(
				new[] { At(1, 1), At(2, 2) },
				new[] { At(2, 2), At(1, 1) },
				null, toSpawn, toDespawn);

			Assert.AreEqual(0, toSpawn.Count);
			Assert.AreEqual(0, toDespawn.Count, "매 프레임 세우고 지우면 화면이 떤다");
		}
	}
}
