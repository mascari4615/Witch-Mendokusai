using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai.EditorTools
{
	// 마계 야수 사냥(TASK-WM-182 트랙③) PlayMode 자율 behavior-verify — 사용자 0클릭. WMGreenhousePlayVerify 동형.
	// 야외(World, IsDungeon=false)서 야수 스폰(풀=DI 주입) → 도살(UnitHealth.ReceiveDamage) →
	// WildBeastObject.HandleDeathEffects 가 IsDungeon 게이트 무시하고 DropLoot → 마수 전리품 ItemObject 드랍 검증.
	// = 트랙③ 핵심(야외 사냥 전리품). flee 방향은 WildBeastFleeTest(unit 3/3), 전리품 테이블은 WildBeastLootTableTest.
	// MCP Play wedge 회피 = 하네스 자체 구동 + Editor.log ground-truth + HARD_TIMEOUT auto-exit.
	[InitializeOnLoad]
	public static class WMHuntPlayVerify
	{
		private const string ARM_PREF = "WM_HUNT_PLAYVERIFY_ARMED";
		private const string TAG = "[HUNT-PLAY-3b8]";
		private const double SETTLE_SECONDS = 2.0;
		private const double HARD_TIMEOUT = 45.0;
		private const int MOB_MASU_BOAR = 18220;

		private static double playStart;
		private static double readyAt = -1.0;
		private static bool ran;

		static WMHuntPlayVerify()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		[MenuItem("WM/Verify/마계 야수 사냥 Play 자율검증")]
		public static void Arm()
		{
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ARM_PREF, false))
			{
				EditorPrefs.SetBool(ARM_PREF, false);
				playStart = EditorApplication.timeSinceStartup;
				readyAt = -1.0;
				ran = false;
				EditorApplication.update += Tick;
				Debug.Log(TAG + " EnteredPlayMode — World 대기 시작");
			}
		}

		private static void Tick()
		{
			double now = EditorApplication.timeSinceStartup;
			if (now - playStart > HARD_TIMEOUT)
			{
				Debug.LogError(TAG + " TIMEOUT — World 미준비 또는 행. Play 강제 종료.");
				Finish();
				return;
			}
			if (ran)
				return;

			Scene active = SceneManager.GetActiveScene();
			bool worldReady = active.IsValid() && active.name == "World" && active.isLoaded;
			bool ready = worldReady
				&& ObjectPoolManager.TryGetExistingInstance(out _)
				&& SOManagerBridge.HasInstance;
			if (ready == false)
				return;

			if (readyAt < 0.0) { readyAt = now; return; }
			if (now - readyAt < SETTLE_SECONDS) return;

			ran = true;
			RunVerify();
		}

		private static Monster FindBoar()
		{
			if (SOManagerBridge.HasInstance == false)
				return null;
			if (SOManagerBridge.DataSOs.TryGetValue(typeof(Monster), out Dictionary<int, DataSO> monsters) == false)
				return null;
			foreach (DataSO dataSO in monsters.Values)
			{
				if (dataSO is Monster monster && monster.ID == MOB_MASU_BOAR)
					return monster;
			}
			return null;
		}

		private static void RunVerify()
		{
			try
			{
				bool isDungeon = DungeonManagerBridge.IsDungeon; // World = false (야외 사냥 컨텍스트)

				Monster boar = FindBoar();
				if (boar == null || boar.Prefab == null)
				{
					Debug.LogError(TAG + " FAIL — MOB_18220 마수멧돼지/Prefab 미로드 (boar=" + (boar != null) + ")");
					Finish();
					return;
				}

				ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager pool);

				// 드랍률 = 전리품 가중치 합(5/3/2=10) + Probability.shouldFill100Percent → 킬당 ~10% (디자이너 수치, 버그 X).
				// → 단발 킬은 90% 꽝. N회 사냥 루프로 드랍 잡음(40회 = ~98.5% ≥1 드랍).
				const int HUNT_TRIES = 40;
				int lootStart = CountAllLoot();
				bool everSpawnedOk = false, everKilled = false, everDeactivated = false, componentsOk = false;
				bool diagLogged = false;

				for (int t = 0; t < HUNT_TRIES; t++)
				{
					GameObject beast = pool.Spawn(boar.Prefab);
					beast.transform.position = new Vector3(t * 3f, 0f, 0f);
					WildBeastObject wildBeast = beast.GetComponent<WildBeastObject>();
					FSMWildBeast fsm = beast.GetComponent<FSMWildBeast>();
					UnitHealth health = beast.GetComponent<UnitHealth>();
					wildBeast?.Init(boar);
					beast.SetActive(true);

					if (t == 0)
					{
						componentsOk = wildBeast != null && fsm != null && health != null;
						object glRef = typeof(MonsterObject).GetField("gameLogic", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(wildBeast);
						Debug.Log(TAG + " DIAG gameLogic=" + (glRef != null) + " lootTableN=" + (boar.Loots != null ? boar.Loots.Count : -1)
							+ " lootPrefab=" + (ResourceManager.Instance != null && ResourceManager.Instance.LootItemPrefab != null)
							+ " 드랍률≈10%(가중합/100) → " + HUNT_TRIES + "회 루프");
						diagLogged = true;
					}
					everSpawnedOk |= (wildBeast != null && fsm != null && health != null);
					if (health != null && health.IsAlive)
					{
						everSpawnedOk &= true;
						health.ReceiveDamage(new DamageInfo { damage = 99999, ignoreInvincible = true });
						everKilled |= health.IsAlive == false;
					}
					everDeactivated |= (beast == null || beast.activeSelf == false);

					if (CountAllLoot() > lootStart) break; // 드랍 잡힘 — 조기 종료
				}

				int lootEnd = CountAllLoot();
				int drops = lootEnd - lootStart;
				bool lootDropped = drops > 0;
				string dropItemName = FirstLootItemName();

				bool loopOk = isDungeon == false && componentsOk && everSpawnedOk && everKilled && everDeactivated && lootDropped;
				Debug.Log(TAG + (loopOk ? " LOOP OK ✅" : " LOOP FAIL ❌")
					+ " isDungeon=" + isDungeon + "(false기대) componentsOk=" + componentsOk
					+ " spawned=" + everSpawnedOk + " killed=" + everKilled + " deactivated=" + everDeactivated
					+ " drops=" + drops + " (in " + HUNT_TRIES + " hunts) dropItem=" + dropItemName + " diag=" + diagLogged);

				ScreenCapture.CaptureScreenshot("Temp/hunt-play-verify.png");
				Debug.Log(TAG + " screenshot → Temp/hunt-play-verify.png");
			}
			catch (System.Exception e)
			{
				Debug.LogError(TAG + " EXCEPTION — " + e.GetType().Name + ": " + e.Message);
			}
			finally
			{
				Finish();
			}
		}

		// 전체 전리품 ItemObject 수 (inactive 포함 — 풀 inactive 루트 밑 스폰도 잡음).
		private static int CountAllLoot()
		{
			return Object.FindObjectsByType<ItemObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
		}

		// 드랍된 전리품 첫 항목 이름 (ItemObject.itemData private — reflection). 마수 고기/가죽/뼈 확인용.
		private static string FirstLootItemName()
		{
			ItemObject[] items = Object.FindObjectsByType<ItemObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
			if (items.Length == 0)
				return "-";
			var field = typeof(ItemObject).GetField("itemData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			var data = field?.GetValue(items[0]) as ItemData;
			return data != null ? data.name : "(null)";
		}

		private static void Finish()
		{
			EditorApplication.update -= Tick;
			if (EditorApplication.isPlaying)
				EditorApplication.ExitPlaymode();
		}
	}
}
