using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	// 릴리스에서 걸어서 닿는 스테이지의 자료 계약. 시작 스테이지에서 켜진 포털만 따라감
	public class ReleaseStageReachTest
	{
		private const string STAGE_MANAGER_PREFAB = "Assets/_WitchMendokusai/Core/Assets/Singletons/StageManager.prefab";
		// 릴리스에 열어 둔 스테이지. 새로 열 때 여기에 적음
		private static readonly string[] RELEASE_STAGES = { "WS_4_Lab" };

		[Test]
		public void Portals_from_start_stage_reach_only_release_stages()
		{
			StageManager stageManager = AssetDatabase.LoadAssetAtPath<GameObject>(STAGE_MANAGER_PREFAB).GetComponent<StageManager>();
			Assert.IsFalse(stageManager.StartStage.DevOnly, "시작 스테이지가 개발 전용");

			HashSet<string> reached = new();
			Queue<Stage> pending = new();
			pending.Enqueue(stageManager.StartStage);
			while (pending.Count > 0)
			{
				Stage stage = pending.Dequeue();
				if (reached.Add(stage.name) == false)
					continue;

				foreach (Portal portal in stage.Prefab.GetComponentsInChildren<Portal>(true))
				{
					// 꺼 둔 포털과 개발 전용 스테이지로 가는 포털은 릴리스에서 닫힘
					if (portal.gameObject.activeSelf == false || portal.TargetStage == null || portal.TargetStage.DevOnly)
						continue;
					pending.Enqueue(portal.TargetStage);
				}
			}

			CollectionAssert.IsSubsetOf(reached, RELEASE_STAGES);
		}

		[Test]
		public void Dungeons_open_in_release_have_waves()
		{
			foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(Dungeon)}"))
			{
				Dungeon dungeon = AssetDatabase.LoadAssetAtPath<Dungeon>(AssetDatabase.GUIDToAssetPath(guid));
				if (dungeon.DevBuildOnly)
					continue;
				Assert.Greater(dungeon.MonsterWaves.Count, 0, dungeon.name);
				// LoadStage 는 릴리스에서 개발 전용 스테이지를 조용히 거부하므로 열린 던전의 판이 안 바뀐 채 시작됨
				foreach (Stage stage in dungeon.Stages)
					Assert.IsFalse(stage.DevOnly, $"{dungeon.name} 의 {stage.name} 이 개발 전용");
			}
		}
	}
}
