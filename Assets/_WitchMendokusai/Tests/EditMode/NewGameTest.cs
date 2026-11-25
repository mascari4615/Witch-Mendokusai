using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace WitchMendokusai.Tests
{
	// 새 게임과 불러오기가 앞 게임 값을 남기지 않는 것. 전체 흐름 (로비 초기화) 은 시험 세이브 Play 로 확인
	public sealed class NewGameTest
	{
		private SOManager soManager;

		[SetUp]
		public void SetUp()
		{
			soManager = ScriptableObject.CreateInstance<SOManager>();
			SOManagerBridge.Register(soManager);
		}

		[TearDown]
		public void TearDown()
		{
			SOManagerBridge.Register(null);
			UnityEngine.Object.DestroyImmediate(soManager);
		}

		private static Dictionary<WorkListType, List<Work>> Works(params Work[] works) => new()
		{
			{ WorkListType.DollWork, new() },
			{ WorkListType.DummyWork, new() },
			{ WorkListType.VQuestWork, new(works) },
		};

		private static Work QuestWork(Guid? questGuid) => new(WorkConstants.NONE_WORKER_ID, WorkType.QuestWork, questGuid, 10f);

		[Test]
		public void New_game_is_blank_except_starting_values()
		{
			List<WindowLayoutEntry> layouts = new() { new WindowLayoutEntry { windowId = "Inventory", x = 12, y = 34 } };

			GameData newGame = NewGameData.Create(layouts);

			Assert.AreEqual(SaveManager.SAVE_VERSION, newGame.saveVersion);
			Assert.AreEqual(0, newGame.curDollIndex);
			Assert.AreEqual(1, newGame.dummyDollCount);
			Assert.AreEqual(NewGameData.STARTING_NYANG, newGame.gameStats[GameStatType.NYANG]);
			Assert.IsEmpty(newGame.inventoryItems);
			Assert.IsEmpty(newGame.hotbarItems);
			Assert.IsEmpty(newGame.runtimeQuests);
			Assert.IsEmpty(newGame.worldStages);
			Assert.IsEmpty(newGame.upgrades);
			Assert.IsEmpty(newGame.dungeons);
			Assert.IsEmpty(newGame.featureSaves);
			Assert.IsEmpty(newGame.dialogueHistory.StartedDialogueIds);
			foreach (List<Work> works in newGame.works.Values)
				Assert.IsEmpty(works);
		}

		[Test]
		public void New_game_keeps_window_layouts()
		{
			List<WindowLayoutEntry> layouts = new() { new WindowLayoutEntry { windowId = "Inventory", x = 12, y = 34 } };

			Assert.AreEqual("Inventory", NewGameData.Create(layouts).windowLayouts[0].windowId);
		}

		[Test]
		public void Works_without_their_quest_are_dropped_on_load()
		{
			Guid? kept = Guid.NewGuid();
			WorkManager workManager = new();
			LogAssert.Expect(LogType.Warning, new Regex("퀘스트가 없는 작업 1개"));

			workManager.Init(Works(QuestWork(kept), QuestWork(Guid.NewGuid())), new HashSet<Guid?> { kept });

			Assert.AreEqual(1, workManager.GetWorkCount(WorkListType.VQuestWork));
			Assert.IsTrue(workManager.TryGetWorkByQuestGuid(kept, out Work _));
		}

		[Test]
		public void Finished_work_leaves_even_when_finishing_throws()
		{
			Guid? questGuid = Guid.NewGuid();
			Work work = QuestWork(questGuid);
			work.CurTime = work.Time;
			Dictionary<WorkListType, List<Work>> works = Works(work);
			WorkManager workManager = new();
			workManager.Init(works, new HashSet<Guid?> { questGuid });

			// 퀘스트 관리자가 없어 끝내기가 터짐. 터지는 것은 그대로 보이고 작업만 남지 않아야 함
			Assert.Throws<NullReferenceException>(() => workManager.TickWorks(works[WorkListType.VQuestWork]));

			Assert.AreEqual(0, workManager.GetWorkCount(WorkListType.VQuestWork));
		}

		[Test]
		public void Loading_blank_stage_empties_it()
		{
			WorldStage stage = ScriptableObject.CreateInstance<WorldStage>();
			stage.GridData.AddBuildingAt(new Numerics.Vector3Int(1, 0, 1), new BuildingInstanceData(2));
			stage.CityEconomy.AddStock(new ResourceId(1), 5f);

			stage.Load(default);

			Assert.IsEmpty(stage.GridData.BuildingData);
			Assert.IsEmpty(stage.CityEconomy.Stock);
			UnityEngine.Object.DestroyImmediate(stage);
		}

		[Test]
		public void Deleting_chunks_leaves_terrain_cache()
		{
			string basePath = Path.Combine(Application.temporaryCachePath, $"{nameof(NewGameTest)}-{Guid.NewGuid():N}");
			string world = Path.Combine(basePath, "Saves", "DefaultWorld");
			Directory.CreateDirectory(Path.Combine(world, "Chunks"));
			Directory.CreateDirectory(Path.Combine(world, "TerrainRegions"));
			File.WriteAllText(Path.Combine(world, "Chunks", "chunk_0_0.dat"), "x");
			File.WriteAllText(Path.Combine(world, "TerrainRegions", "region.dat"), "x");

			ChunkStorage.DeleteAll(basePath);
			// 지울 것이 없어도 조용히
			ChunkStorage.DeleteAll(basePath);

			Assert.IsFalse(Directory.Exists(Path.Combine(world, "Chunks")));
			Assert.IsTrue(File.Exists(Path.Combine(world, "TerrainRegions", "region.dat")));
			Directory.Delete(basePath, true);
		}
	}
}
