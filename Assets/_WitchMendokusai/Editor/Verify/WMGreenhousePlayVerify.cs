using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Farming;

namespace WitchMendokusai.EditorTools
{
	// 마도 온실(TASK-WM-167) PlayMode 자율 behavior-verify — 사용자 0클릭.
	// WM Play 부팅 중 외부 명령 400/503 가능 →
	// 이 하네스가 *에디터 안에서* 스스로: Play 진입 → World 씬 준비 대기 → WitchGreenhouseObject 스폰(Start 자립
	// 구축) → demoTick 몇 초 → 칸 상태 로그(유니크 prefix) → 스크린샷 → 자동 ExitPlaymode. Editor.log 가 ground-truth.
	// [[wm-playmode-autoverify-bootready-gate]] 패턴. 하드 타임아웃 = 절대 Play 에 안 물리게(공유 에디터 보호).
	[InitializeOnLoad]
	public static class WMGreenhousePlayVerify
	{
		private const string ARM_PREF = "WM_GH_PLAYVERIFY_ARMED";
		private const string TAG = "[GH-PLAY-9d4]";
		private const double SETTLE_SECONDS = 2.0;   // World 준비 후 Start(자립 구축) 실행 대기
		private const double HARD_TIMEOUT = 40.0;     // 이 시간 넘으면 무조건 Play 탈출(안전망)
		private const int SAMPLE_LOOT_ID = 90000167;  // 달빛이끼 잎(placeholder 샘플 수확물) — 수확 시 인벤토리 지급 대상

		private static double playStart;
		private static double spawnAt = -1.0;
		private static bool spawned;
		private static WitchGreenhouseObject house;

		static WMGreenhousePlayVerify()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		[MenuItem("WM/Verify/Greenhouse Play Verify")]
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
				spawnAt = -1.0;
				spawned = false;
				house = null;
				EditorApplication.update += Tick;
				Debug.Log(TAG + " EnteredPlayMode — World 대기 시작");
			}
		}

		private static void Tick()
		{
			double now = EditorApplication.timeSinceStartup;

			// 안전망: 무슨 일이 있어도 HARD_TIMEOUT 넘으면 Play 탈출(공유 에디터 보호).
			if (now - playStart > HARD_TIMEOUT)
			{
				Debug.LogError(TAG + " TIMEOUT — World 미준비 또는 행. Play 강제 종료.");
				Finish();
				return;
			}

			// World 씬 준비 전엔 대기.
			if (spawned == false)
			{
				Scene active = SceneManager.GetActiveScene();
				if (active.IsValid() == false || active.name != "World" || active.isLoaded == false)
				{
					return;
				}

				// World 준비 — 온실 스폰(Start 가 자립 구축+placeholder+demoTick 시작).
				GameObject go = new("[Verify] 마도 온실");
				house = go.AddComponent<WitchGreenhouseObject>();
				spawned = true;
				spawnAt = now;
				Debug.Log(TAG + " World 준비 — 온실 스폰됨. demoTick 관찰 " + SETTLE_SECONDS + "s");
				return;
			}

			// demoTick 관찰 후 결과 로그 + 종료.
			if (now - spawnAt >= SETTLE_SECONDS)
			{
				ReportAndFinish();
			}
		}

		private static void ReportAndFinish()
		{
			if (house == null || house.Model == null || house.Model.PlotCount == 0)
			{
				Debug.LogError(TAG + " FAIL — house/Model/plots null (자립 구축 안 됨)");
				Finish();
				return;
			}

			// ── 1. 부트 파이프라인이 마도 식물 종을 실제로 로드했나 (DataLoader → SOManager) ──
			//    edit-mode 는 SOManager 를 수동 주입했지만, 여기선 *진짜 부트*가 Addressable 로 로드한 결과.
			Dictionary<int, DataSO> registered = null;
			bool speciesLoaded = SOManagerBridge.HasInstance
				&& SOManagerBridge.DataSOs.TryGetValue(typeof(WitchPlantSO), out registered)
				&& registered.Count > 0;
			string regName = "-";
			if (speciesLoaded)
			{
				foreach (DataSO dataSO in registered.Values)
				{
					if (dataSO is WitchPlantSO plant)
					{
						regName = plant.Name;
						break;
					}
				}
			}

			// ── 2. 「봐줘야 진짜」 루프 결정적 구동: 관찰 → 강제 개화 → 수확 ──
			GreenhousePlot plot0 = house.Model.GetPlot(0);
			int plantedId = plot0 == null ? -999 : plot0.PlantDataId;
			bool observed = house.Observe(0);                     // 시들기 전 관찰(진짜화 자격)
			int ticks = 0;
			while (plot0 != null && plot0.Phase != PlotPhase.Bloomed && ticks < 40)
			{
				house.TickDay();                                  // carer 가 생기 유지 → 개화까지 생장
				ticks++;
			}
			bool bloomed = plot0 != null && plot0.Phase == PlotPhase.Bloomed;
			bool harvested = house.Harvest(0);                    // 개화+관찰 → IsSpecimen → HandleSpecimen

			// ── 2b. 수확물이 실제 인벤토리에 지급됐나 (GrantHarvestItem → ItemInventory.Add) ──
			//    EditMode 는 by-construction(Add 호출)만 봤고, 진짜 부트의 Inventory 지급은 미검증이었음.
			int lootInInventory = SOManagerBridge.HasInstance && SOManagerBridge.ItemInventory != null
				? SOManagerBridge.ItemInventory.GetItemAmount(SAMPLE_LOOT_ID)
				: -1;

			// ── 3. 영구 표본이 도감 데이터(DataManager)에 박혔나 (수확해 사라져도 영원) ──
			bool specimenRecorded = DataManager.TryGetExistingInstance(out DataManager dataManager)
				&& dataManager.SpecimenCollected.TryGetValue(plantedId, out bool collected) && collected;

			// ── 4. Discovery 「마도 식물」 탭이 종을 나열 + 표본 텍스트를 보여주나 ──
			PlantDiscoveryCategory discovery = new();
			discovery.OnActivate();
			IReadOnlyList<EntryDescriptor> entries = discovery.GetEntries();
			int discoveryCount = entries.Count;
			bool discoverySpecimenText = false;
			if (discoveryCount > 0)
			{
				VisualElement detail = discovery.BuildDetail(entries[0]);
				foreach (VisualElement element in detail.Children())
				{
					if (element is Label label && label.text.Contains("표본으로 남음"))
					{
						discoverySpecimenText = true;
						break;
					}
				}
			}

			bool loopOk = speciesLoaded && observed && bloomed && harvested && specimenRecorded && discoveryCount > 0 && discoverySpecimenText && lootInInventory >= 1;
			Debug.Log(TAG + (loopOk ? " LOOP OK ✅" : " LOOP FAIL ❌")
				+ " speciesLoaded=" + speciesLoaded + " regName=" + regName + " plantedId=" + plantedId
				+ " observed=" + observed + " bloomTicks=" + ticks + " bloomed=" + bloomed + " harvested=" + harvested
				+ " lootInInventory=" + lootInInventory
				+ " specimenRecorded=" + specimenRecorded + " discoveryCount=" + discoveryCount + " discoverySpecimenText=" + discoverySpecimenText);

			string shot = "Temp/gh-play-verify.png";
			ScreenCapture.CaptureScreenshot(shot);
			Debug.Log(TAG + " screenshot → " + shot);

			Finish();
		}

		private static void Finish()
		{
			EditorApplication.update -= Tick;
			if (EditorApplication.isPlaying)
			{
				EditorApplication.ExitPlaymode();
			}
		}
	}
}
