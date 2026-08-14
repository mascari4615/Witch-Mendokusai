using System.Linq;
using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.Numerics;
using WitchMendokusai.Server;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// 적은 것이 <b>그대로 되살아나나</b> (TASK-WM-362).
	///
	/// ★ 왜 이것까지 필요한가: 판 번호(WM-360)는 되돌리기를 막고, 못(WM-361)은 이름 바뀜을 막는다.
	///   그런데 <b>이름도 그대로고 판도 맞는데 안 되살아나는</b> 경우가 남는다 —
	///   적기(Save)에는 있는데 되살리기(Load)가 그 칸을 안 보는 경우다.
	///   그러면 껐다 켤 때마다 그 값이 조용히 0 이 된다(상자 안, 솥 자국, 뽑힌 자리…).
	///   컴파일도 되고 이름 못도 초록이다 — <b>값으로</b> 확인하는 수밖에 없다.
	///
	/// 그래서 여기서는 <b>칸마다 값을 넣고</b> 적었다 되살려 <b>같은지</b> 본다.
	/// </summary>
	/// <remarks>
	/// [빨강-확인] 되살리기에서 한 줄씩 꺼 보니 각각 그 줄만 빨갰다 (2026-08-14):
	/// 들판 되살리기(`Gatherables.Load`)를 끄면 「뽑힌 자리가…」 · 상자 되살리기를 끄면 「상자 안에 든 것이…」.
	/// </remarks>
	public sealed class SaveRoundTripTests
	{
		/// <summary>
		/// 세계마다 <b>제 들판</b>을 준다 (TASK-WM-362).
		///
		/// ⚠ 처음엔 `ServerGatherables.Field` 를 두 세계가 <b>같이</b> 썼다 — 그러면 「되살렸나」를 물어도
		///   앞 세계가 남긴 자국을 그대로 보게 되어, 되살리기를 <b>아예 껐는데도 초록</b>이었다.
		///   무대가 그 길을 안 간 것이다(관문 규율 ⑧).
		/// </summary>
		private static WorldSim FreshWorld()
		{
			return new WorldSim
			{
				Gatherables = new WorldGatherables(WorldSeeds.Gatherables()),
				Buildables = ServerBuildingCatalog.Catalog,
			};
		}

		private static WorldSim WorldWithSomethingInEveryDrawer()
		{
			// ⚠ 맨 세계에는 <b>지을 것도 들판도 없다</b> — 그 목록은 세계를 띄울 때 서버가 끼운다.
			//   그걸 안 끼우면 이 시험은 「못 지었다」로 서고, 그건 제품이 아니라 무대 이야기다.
			WorldSim world = FreshWorld();

			// 시각
			world.Calendar.Set(3, 2, 17, 21, 45);

			// 지은 것 — 상자 하나(안에 물건을 넣으려면 건물이 먼저 있어야 한다)와 솥 하나.
			Assert.That(world.TryPlaceBuilding(new Vector3Int(2, 0, 2), 4005, world.Buildables), Is.True, "상자를 못 지었다");
			Assert.That(world.TryPlaceBuilding(new Vector3Int(4, 0, 4), 4000, world.Buildables), Is.True, "솥을 못 지었다");

			return world;
		}

		[Test]
		public void 적은_것이_그대로_되살아난다()
		{
			WorldSim before = WorldWithSomethingInEveryDrawer();
			WorldSaveData written = before.Save();

			WorldSim after = FreshWorld();
			after.Load(written);

			Assert.That(after.Calendar.TotalMinutes(), Is.EqualTo(before.Calendar.TotalMinutes()),
				"세계의 시각이 안 돌아오면 껐다 켤 때마다 하루가 사라진다");
			Assert.That(after.Buildings().Length, Is.EqualTo(before.Buildings().Length), "지은 것이 안 돌아왔다");
		}

		/// <summary>★ 상자 안 — 여기가 새면 사람이 넣어 둔 것이 사라진다(제일 아픈 자리).</summary>
		[Test]
		public void 상자_안에_든_것이_그대로_되살아난다()
		{
			WorldSim before = WorldWithSomethingInEveryDrawer();
			Vector3Int chest = new Vector3Int(2, 0, 2);
			int leftover = before.Storages.Put(chest, ServerItemCatalog.Find(10), 4, chest.x, chest.z);
			Assert.That(leftover, Is.Zero, "상자에 못 넣었다 — 이 시험이 잴 것이 없다");

			WorldSim after = FreshWorld();
			after.Load(before.Save(), ServerItemCatalog.Catalog);

			int[] amounts = after.Storages.Contents(chest).Select((one) => one.amount).ToArray();
			Assert.That(amounts, Is.EquivalentTo(new[] { 4 }),
				"상자 안이 안 돌아오면 사람이 넣어 둔 것이 껐다 켤 때 사라진다");
		}

		/// <summary>뽑힌 자리 — 안 돌아오면 껐다 켤 때마다 들판이 통째로 다시 자란다(줍기가 뜻을 잃는다).</summary>
		[Test]
		public void 뽑힌_자리가_그대로_되살아난다()
		{
			WorldSim before = FreshWorld();
			before.Calendar.Set(1, 0, 2, 9, 0);

			var standingOn = before.Gatherables.Alive(before.Calendar.TotalMinutes()).First();
			bool took = before.Gatherables.TryTake(standingOn.Id, standingOn.X, standingOn.Z,
				before.Calendar.TotalMinutes(), out int _, out int _);
			Assert.That(took, Is.True, "하나도 못 뽑았다 — 이 시험이 잴 것이 없다");

			int emptyBefore = before.Gatherables.Save().Count();

			WorldSim after = FreshWorld();
			after.Load(before.Save());

			Assert.That(after.Gatherables.Save().Count(), Is.EqualTo(emptyBefore),
				"뽑힌 자리가 안 돌아오면 껐다 켤 때마다 들판이 통째로 다시 자란다");
		}
	}
}
