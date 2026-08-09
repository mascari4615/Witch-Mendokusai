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

		/// <summary>
		/// 여러 칸 건물은 <b>한 칸으로 접히지 않는다</b> (TASK-WM-217).
		///
		/// ★ 실측 2026-08-10: 세계는 크기를 알고 보내는데 게임 쪽 자료형이 그걸 버렸다. 그래서 화면이
		///   들고 있는 4칸 중 pivot 만 「세계에 있는 것」이 되고 나머지 3칸이 매 프레임 지워졌다.
		/// </summary>
		[Test]
		public void 두칸짜리_건물의_나머지_칸을_지우지_않는다()
		{
			DomainSDK.Building.BuildingPlacement big = new DomainSDK.Building.BuildingPlacement
			{
				CellX = 3, CellY = 0, CellZ = 3, BuildingId = 3, Width = 2, Length = 2,
			};

			BuildingFollowPlan.Compute(
				new[] { big },
				// 발자국은 pivot 에서 -x 로 뻗는다 (BuildingFootprint 정본).
				new[] { At(3, 3), At(2, 3), At(3, 4), At(2, 4) },
				null, toSpawn, toDespawn);

			Assert.AreEqual(0, toDespawn.Count, "깔고 앉은 칸을 지우면 여러 칸 건물이 한 칸으로 접힌다");
			Assert.AreEqual(0, toSpawn.Count, "이미 서 있는 건물을 또 세우면 한 채가 네 채가 된다");
		}

		[Test]
		public void 두칸짜리_건물은_pivot_한_곳에서만_선다()
		{
			DomainSDK.Building.BuildingPlacement big = new DomainSDK.Building.BuildingPlacement
			{
				CellX = 7, CellY = 0, CellZ = 7, BuildingId = 3, Width = 2, Length = 2,
			};

			BuildingFollowPlan.Compute(new[] { big }, new Vector3Int[0], null, toSpawn, toDespawn);

			Assert.AreEqual(1, toSpawn.Count, "칸마다 세우면 한 건물이 네 채가 된다");
			Assert.AreEqual(At(7, 7), toSpawn[0]);
		}

		[Test]
		public void 부순_두칸짜리_건물은_네_칸_모두_사라진다()
		{
			BuildingFollowPlan.Compute(
				new DomainSDK.Building.BuildingPlacement[0],
				new[] { At(3, 3), At(2, 3), At(3, 4), At(2, 4) },
				null, toSpawn, toDespawn);

			Assert.AreEqual(4, toDespawn.Count, "한 칸만 지우면 나머지가 유령으로 남아 그 자리에 못 짓는다");
		}

		[Test]
		public void 크기를_안_적은_옛_통로는_한_칸으로_읽는다()
		{
			DomainSDK.Building.BuildingPlacement old = new DomainSDK.Building.BuildingPlacement
			{
				CellX = 1, CellY = 0, CellZ = 1, BuildingId = 1,
			};

			BuildingFollowPlan.Compute(new[] { old }, new Vector3Int[0], null, toSpawn, toDespawn);

			Assert.AreEqual(1, toSpawn.Count, "0 을 「크기 없음」으로 읽어 버리면 옛 통로가 아무것도 못 세운다");
			Assert.AreEqual(At(1, 1), toSpawn[0]);
		}

	}
}
