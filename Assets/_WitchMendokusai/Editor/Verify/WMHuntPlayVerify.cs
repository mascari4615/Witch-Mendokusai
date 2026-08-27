using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 마계 야수 사냥(TASK-WM-182 트랙③) PlayMode 자율 behavior-verify — 사용자 0클릭. lifecycle = <see cref="WMPlayVerifyBase"/>.
	///
	/// 야외(World, IsDungeon=false)서 야수 스폰(풀=DI 주입) → 도살(UnitHealth.ReceiveDamage) → WildBeastObject
	/// .HandleDeathEffects 가 IsDungeon 게이트 무시하고 DropLoot → 마수 전리품 ItemObject 드랍 검증. 드랍률 ~10%
	/// (전리품 가중치 합 10 + Probability.shouldFill100Percent — 디자이너 수치)라 N회 사냥 루프로 드랍 잡음.
	/// flee 방향은 WildBeastFleeTest(unit 3/3), 전리품 테이블은 WildBeastLootTableTest.
	/// </summary>
	[InitializeOnLoad]
	public sealed class WMHuntPlayVerify : WMPlayVerifyBase
	{
		private const int MOB_MASU_BOAR = 18220;
		private const int HUNT_TRIES = 40;            // 드랍률 ~10% → 40회 = ~98.5% 확률로 ≥1 드랍
		private const int LETHAL_DAMAGE = 99999;      // 즉살 보장 오버킬
		private const float BEAST_SPACING = 3f;       // 스폰 야수 간 간격(겹침 회피)
		private const string SCREENSHOT_PATH = "Temp/hunt-play-verify.png";

		// [InitializeOnLoad] → static ctor 보장 → Instance 필드 init → base ctor 가 playModeStateChanged 구독.
		private static readonly WMHuntPlayVerify Instance = new();
		static WMHuntPlayVerify() { }

		[MenuItem("WM/Verify/Demon Beast Hunt Play Verify")]
		private static void ArmFromMenu() => Instance.Arm();

		protected override string ArmPref => "WM_HUNT_PLAYVERIFY_ARMED";
		protected override string Tag => "[HUNT-PLAY-3b8]";

		protected override bool IsReady()
		{
			return SceneIsWorld()
				&& ObjectPoolManager.TryGetExistingInstance(out _)
				&& SOManagerBridge.HasInstance;
		}

		protected override void RunVerify()
		{
			bool isDungeon = DungeonManagerBridge.IsDungeon; // World = false (야외 사냥 컨텍스트)

			Monster boar = FindBoar();
			if (boar == null || boar.Prefab == null)
			{
				Log("FAIL — MOB_18220 마수멧돼지/Prefab 미로드 (boar=" + (boar != null) + ")");
				return;
			}

			ObjectPoolManager.TryGetExistingInstance(out ObjectPoolManager pool);

			int lootStart = FindAllLoot().Length;
			bool componentsOk = false;
			bool everKilled = false;
			bool everDeactivated = false;

			for (int t = 0; t < HUNT_TRIES; t++)
			{
				GameObject beast = pool.Spawn(boar.Prefab);
				beast.transform.position = new Vector3(t * BEAST_SPACING, 0f, 0f);
				WildBeastObject wildBeast = beast.GetComponent<WildBeastObject>();
				FSMWildBeast fsm = beast.GetComponent<FSMWildBeast>();
				UnitHealth health = beast.GetComponent<UnitHealth>();
				wildBeast?.Init(boar);
				beast.SetActive(true);

				if (t == 0)
					componentsOk = wildBeast != null && fsm != null && health != null;

				if (health != null && health.IsAlive)
				{
					health.ReceiveDamage(new DamageInfo { damage = LETHAL_DAMAGE, ignoreInvincible = true });
					everKilled |= health.IsAlive == false;
				}
				everDeactivated |= beast == null || beast.activeSelf == false;

				if (FindAllLoot().Length > lootStart)
					break; // 드랍 잡힘 — 조기 종료
			}

			int drops = FindAllLoot().Length - lootStart;
			bool lootDropped = drops > 0;
			string dropItemName = FirstLootItemName();

			bool loopOk = isDungeon == false && componentsOk && everKilled && everDeactivated && lootDropped;
			Log((loopOk ? "LOOP OK ✅" : "LOOP FAIL ❌")
				+ " isDungeon=" + isDungeon + "(false기대) componentsOk=" + componentsOk
				+ " killed=" + everKilled + " deactivated=" + everDeactivated
				+ " drops=" + drops + "/" + HUNT_TRIES + " hunts dropItem=" + dropItemName);

			ScreenCapture.CaptureScreenshot(SCREENSHOT_PATH);
			Log("screenshot → " + SCREENSHOT_PATH);
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

		// 씬 전체 전리품 ItemObject (inactive 포함 — 풀 inactive 루트 밑 스폰도 잡음).
		private static ItemObject[] FindAllLoot()
		{
			return Object.FindObjectsByType<ItemObject>(FindObjectsInactive.Include);
		}

		// 드랍된 전리품 첫 항목 이름 (ItemObject.itemData private — reflection). 마수 고기/가죽/뼈 확인용.
		private static string FirstLootItemName()
		{
			ItemObject[] items = FindAllLoot();
			if (items.Length == 0)
				return "-";
			FieldInfo field = typeof(ItemObject).GetField("itemData", BindingFlags.NonPublic | BindingFlags.Instance);
			ItemData data = field?.GetValue(items[0]) as ItemData;
			return data != null ? data.name : "(null)";
		}
	}
}
