using System.Collections.Generic;
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
	/// 데이터 기반 직사각 맵 회귀 — Build 가 바닥+4벽 생성 / GetSpawns 가 2팀 대칭 위치·개수·X분산.
	/// EditMode 에서 GameObject 생성·검증 후 정리(PlayMode 0). WM-165 item 7(맵 데이터화).
	/// </summary>
	public class RectangleArenaMapTests
	{
		private static RectangleArenaMap Map()
		{
			return ScriptableObject.CreateInstance<RectangleArenaMap>();
		}

		[Test]
		public void Build_CreatesGroundAndFourWalls()
		{
			RectangleArenaMap map = Map();
			GameObject root = new("TestArena");
			try
			{
				map.Build(root.transform);

				Assert.IsNotNull(root.transform.Find("Ground"), "바닥 생성");
				Transform walls = root.transform.Find("Walls");
				Assert.IsNotNull(walls, "벽 홀더 생성");
				Assert.AreEqual(4, walls.childCount, "경계 4벽");
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void GetSpawns_CountMatchesPerTeam()
		{
			RectangleArenaMap map = Map();
			Assert.AreEqual(map.SpawnsPerTeam, map.GetSpawns(0).Count);
			Assert.AreEqual(map.SpawnsPerTeam, map.GetSpawns(1).Count);
		}

		[Test]
		public void GetSpawns_TeamsOnOppositeZSides()
		{
			RectangleArenaMap map = Map();
			Assert.Less(map.GetSpawns(0)[0].z, 0f, "팀0 = -Z");
			Assert.Greater(map.GetSpawns(1)[0].z, 0f, "팀1 = +Z");
		}

		[Test]
		public void GetSpawns_MembersSpreadOnXAxis()
		{
			RectangleArenaMap map = Map();
			IReadOnlyList<Vector3> spawns = map.GetSpawns(0);
			// PerTeam 기본 3 → 멤버 X 가 서로 달라야(겹침 X).
			Assert.AreNotEqual(spawns[0].x, spawns[1].x);
			Assert.AreNotEqual(spawns[1].x, spawns[2].x);
		}
	}
}
