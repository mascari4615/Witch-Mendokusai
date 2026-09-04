using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Idle;
using WitchMendokusai.Idle;

namespace WitchMendokusai.Idle.Editor
{
	/// <summary>
	/// V2 작전 씬을 코드로 짓는다 (concept-v2 — 쿼터뷰 무대 + HUD).
	///
	/// ★ 손으로 안 만드는 이유·순서(에셋 먼저, 씬은 뒤)·다시 열어 검사하는 이유는
	///   V1 빌더(2026-08-30 삭제)에서 실측으로 얻은 규칙 그대로.
	/// </summary>
	public static class IdleSceneBuilder
	{
		private const string SCENE_PATH = "Assets/_WitchMendokusai/Scenes/Idle/Idle.unity";
		private const string PANEL_PATH = "Assets/_WitchMendokusai/Scenes/Idle/PS_0001_Idle.asset";
		private const string TUNING_PATH = "Assets/_WitchMendokusai/Scenes/Idle/TU_0001_Idle.asset";
		private const string HERO_CATALOG_PATH = "Assets/_WitchMendokusai/Idle/Data/Assets/HC_0001_Idle.asset";
		private const string UI_CONTENT_PATH = "Assets/_WitchMendokusai/Idle/Data/Assets/UI_0001_Idle.asset";
		private const string GEAR_PRESENTATION_PATH = "Assets/_WitchMendokusai/Idle/Data/Assets/GP_0001_Idle.asset";
		private const string BATTLE_PRESENTATION_PATH = "Assets/_WitchMendokusai/Idle/Data/Assets/BP_0001_Idle.asset";
		private const string RUNTIME_SETTINGS_PATH = "Assets/_WitchMendokusai/Idle/Data/Assets/RT_0001_Idle.asset";
		private const string STYLE_PATH = "Assets/_WitchMendokusai/Idle/UI/BattleScreen.uss";
		private const string SCREEN_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleBattleScreen.uxml";
		private const string DOLL_PAGE_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleDollPage.uxml";
		private const string ITEM_PAGE_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleItemPage.uxml";
		private const string BAG_CELL_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleBagCell.uxml";
		private const string FORGE_KIND_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleForgeKind.uxml";
		private const string BATTLE_HUD_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleBattleHud.uxml";
		private const string CARD_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleCard.uxml";
		private const string QUEUE_CHIP_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleQueueChip.uxml";
		private const string CHOICE_CARD_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleChoiceCard.uxml";
		private const string CODEX_PAGE_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleCodexPage.uxml";
		private const string SHOP_PAGE_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleShopPage.uxml";
		private const string LAB_PAGE_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleLabPage.uxml";
		private const string DUNGEON_PAGE_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleDungeonPage.uxml";
		private const string INVEST_PAGE_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleInvestPage.uxml";
		private const string PRODUCER_ROW_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleProducerRow.uxml";
		private const string GEAR_POPUP_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleGearPopup.uxml";
		private const string MAP_POPUP_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleMapPopup.uxml";
		private const string HERO_POPUP_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleHeroPopup.uxml";
		private const string GOLD_POPUP_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleGoldPopup.uxml";
		private const string SETTINGS_POPUP_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleSettingsPopup.uxml";
		private const string AWAY_POPUP_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleAwayPopup.uxml";
		private const string WAVE_DOT_PATH = "Assets/_WitchMendokusai/Idle/UI/IdleWaveDot.uxml";
		private const string TAG = "[IdleScene]";

		[MenuItem("WM/Idle/Open and Play %#u")]
		public static void OpenAndPlay()
		{
			if (EditorApplication.isPlaying)
			{
				EditorApplication.isPlaying = false;
				return;
			}

			if (File.Exists(SCENE_PATH) == false)
			{
				Debug.LogError(TAG + " 씬이 없다. 먼저 Dev Panel 씬 짓기: " + SCENE_PATH);
				return;
			}

			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
			{
				return;
			}

			EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
			EditorSceneManager.playModeStartScene = null;
			EditorApplication.isPlaying = true;
		}

		[MenuItem("WM/Idle/V2 Build Scene")]
		public static void Build()
		{
			// 에셋 먼저 디스크에 확정. 씬을 연 뒤 경로로 다시 읽기 (V1 빌더의 실측 규칙)
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

			PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PANEL_PATH);
			TuningSO tuning = AssetDatabase.LoadAssetAtPath<TuningSO>(TUNING_PATH);
			HeroCatalogSO heroCatalog = AssetDatabase.LoadAssetAtPath<HeroCatalogSO>(HERO_CATALOG_PATH);
			UIContentSO uiContent = AssetDatabase.LoadAssetAtPath<UIContentSO>(UI_CONTENT_PATH);
			GearPresentationSO gearPresentation = AssetDatabase.LoadAssetAtPath<GearPresentationSO>(GEAR_PRESENTATION_PATH);
			BattlePresentationSO battlePresentation = AssetDatabase.LoadAssetAtPath<BattlePresentationSO>(BATTLE_PRESENTATION_PATH);
			RuntimeSettingsSO runtimeSettings = AssetDatabase.LoadAssetAtPath<RuntimeSettingsSO>(RUNTIME_SETTINGS_PATH);
			StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH);
			VisualTreeAsset screenAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SCREEN_PATH);
			VisualTreeAsset dollPage = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DOLL_PAGE_PATH);
			VisualTreeAsset itemPage = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ITEM_PAGE_PATH);
			VisualTreeAsset bagCell = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BAG_CELL_PATH);
			VisualTreeAsset forgeKind = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FORGE_KIND_PATH);
			VisualTreeAsset battleHud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BATTLE_HUD_PATH);
			VisualTreeAsset card = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CARD_PATH);
			VisualTreeAsset queueChip = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(QUEUE_CHIP_PATH);
			VisualTreeAsset choiceCard = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CHOICE_CARD_PATH);
			VisualTreeAsset codexpage = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(CODEX_PAGE_PATH);
			VisualTreeAsset shoppage = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SHOP_PAGE_PATH);
			VisualTreeAsset labpage = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LAB_PAGE_PATH);
			VisualTreeAsset dungeonpage = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DUNGEON_PAGE_PATH);
			VisualTreeAsset investpage = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(INVEST_PAGE_PATH);
			VisualTreeAsset producerrow = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(PRODUCER_ROW_PATH);
			VisualTreeAsset gearpopup = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GEAR_POPUP_PATH);
			VisualTreeAsset mappopup = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MAP_POPUP_PATH);
			VisualTreeAsset heropopup = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HERO_POPUP_PATH);
			VisualTreeAsset goldpopup = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GOLD_POPUP_PATH);
			VisualTreeAsset settingspopup = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SETTINGS_POPUP_PATH);
			VisualTreeAsset awaypopup = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AWAY_POPUP_PATH);
			VisualTreeAsset waveDot = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(WAVE_DOT_PATH);
			if (panel == null || tuning == null || heroCatalog == null || uiContent == null || gearPresentation == null
				|| battlePresentation == null || runtimeSettings == null || style == null)
			{
				Debug.LogError(TAG + " 붙일 것을 못 읽었다. panel/tuning/heroes/style = "
					+ (panel != null) + "/" + (tuning != null) + "/" + (heroCatalog != null) + "/" + (style != null)
					+ " (panel·tuning 은 WM/Idle/씬 짓기가 만든다)");
				return;
			}

			// 카메라 — 쿼터뷰. 하늘색 단색이 배경이다 (V2 톤: 밝은 판).
			GameObject cameraObject = new GameObject("Main Camera");
			cameraObject.tag = "MainCamera";
			Camera camera = cameraObject.AddComponent<Camera>();
			camera.clearFlags = CameraClearFlags.SolidColor;
			camera.backgroundColor = battlePresentation.CameraBackgroundColor;
			// ★ 쿼터뷰 (사용자 방향 1, 자동전투+카드 개입 계열) — 45° 로 내려다보고 45° 로 비껴 본다.
			//   정면(요 0)이면 옆뷰가 되고, 90° 면 탑뷰가 된다. 그 사이가 「쿼터」다.
			// 내려다보는 각은 45°, 비껴 보는 각은 30° — 정면(0°)이면 옆뷰, 45°면 땅 모서리가 든다.
			camera.fieldOfView = battlePresentation.CameraFieldOfView;
			Quaternion look = Quaternion.Euler(battlePresentation.CameraEuler);
			camera.transform.rotation = look;
			cameraObject.transform.position = look * new Vector3(0f, 0f, -battlePresentation.CameraDistance)
				+ battlePresentation.CameraTargetOffset;

			GameObject lightObject = new GameObject("Directional Light");
			Light light = lightObject.AddComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = battlePresentation.LightIntensity;
			light.shadows = LightShadows.Soft;
			lightObject.transform.rotation = Quaternion.Euler(battlePresentation.LightEuler);

			GameObject stageObject = new GameObject("BattleStage");
			BattleStage stage = stageObject.AddComponent<BattleStage>();
			AssignPrivateField(stage, "presentationAsset", battlePresentation);

			GameObject screenObject = new GameObject("BattleScreen");
			UIDocument document = screenObject.AddComponent<UIDocument>();
			document.panelSettings = panel;

			BattleScreen screen = screenObject.AddComponent<BattleScreen>();
			AssignPrivateField(screen, "tuningAsset", tuning);
			AssignPrivateField(screen, "heroCatalogAsset", heroCatalog);
			AssignPrivateField(screen, "uiContentAsset", uiContent);
			AssignPrivateField(screen, "gearPresentationAsset", gearPresentation);
			AssignPrivateField(screen, "runtimeSettingsAsset", runtimeSettings);
			AssignPrivateField(screen, "styleSheet", style);
			AssignPrivateField(screen, "screenAsset", screenAsset);
			AssignPrivateField(screen, "dollPageAsset", dollPage);
			AssignPrivateField(screen, "itemPageAsset", itemPage);
			AssignPrivateField(screen, "bagCellAsset", bagCell);
			AssignPrivateField(screen, "forgeKindAsset", forgeKind);
			AssignPrivateField(screen, "battleHudAsset", battleHud);
			AssignPrivateField(screen, "cardAsset", card);
			AssignPrivateField(screen, "queueChipAsset", queueChip);
			AssignPrivateField(screen, "choiceCardAsset", choiceCard);
			AssignPrivateField(screen, "codexPageAsset", codexpage);
			AssignPrivateField(screen, "shopPageAsset", shoppage);
			AssignPrivateField(screen, "labPageAsset", labpage);
			AssignPrivateField(screen, "dungeonPageAsset", dungeonpage);
			AssignPrivateField(screen, "investPageAsset", investpage);
			AssignPrivateField(screen, "producerRowAsset", producerrow);
			AssignPrivateField(screen, "gearPopupAsset", gearpopup);
			AssignPrivateField(screen, "mapPopupAsset", mappopup);
			AssignPrivateField(screen, "heroPopupAsset", heropopup);
			AssignPrivateField(screen, "goldPopupAsset", goldpopup);
			AssignPrivateField(screen, "settingsPopupAsset", settingspopup);
			AssignPrivateField(screen, "awayPopupAsset", awaypopup);
			AssignPrivateField(screen, "waveDotAsset", waveDot);
			AssignPrivateField(screen, "stage", stage);

			// 이게 없으면 버튼이 안 눌린다 — 화면은 멀쩡해 눈으로 못 잡는다.
			GameObject eventSystem = new GameObject("EventSystem");
			eventSystem.AddComponent<EventSystem>();
			eventSystem.AddComponent<InputSystemUIInputModule>();

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene, SCENE_PATH);

			AddToBuildSettings();
			AssetDatabase.SaveAssets();

			if (Verify() == false)
			{
				return;
			}

			Debug.Log(TAG + " 씬을 지었다: " + SCENE_PATH);
		}

		/// <summary>저장된 씬을 다시 열어 빈 참조를 본다 — 메모리와 디스크는 다른 말이다.</summary>
		[MenuItem("WM/Idle/V2 Verify Scene")]
		public static bool Verify()
		{
			Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

			BattleScreen screen = Object.FindAnyObjectByType<BattleScreen>();
			BattleStage stage = Object.FindAnyObjectByType<BattleStage>();
			UIDocument document = Object.FindAnyObjectByType<UIDocument>();
			EventSystem events = Object.FindAnyObjectByType<EventSystem>();
			Camera camera = Object.FindAnyObjectByType<Camera>();

			List<string> missing = new List<string>();

			if (screen == null) { missing.Add("BattleScreen"); }
			if (stage == null) { missing.Add("BattleStage"); }
			if (camera == null) { missing.Add("Main Camera (없으면 무대가 안 보인다)"); }
			if (events == null) { missing.Add("EventSystem (없으면 버튼이 안 눌린다)"); }

			if (document == null)
			{
				missing.Add("UIDocument");
			}
			else if (document.panelSettings == null)
			{
				missing.Add("UIDocument.panelSettings");
			}

			if (screen != null)
			{
				SerializedObject serialized = new SerializedObject(screen);
				if (serialized.FindProperty("styleSheet").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.styleSheet");
				}
				if (serialized.FindProperty("screenAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.screenAsset");
				}

				if (serialized.FindProperty("dollPageAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.dollPageAsset");
				}
				if (serialized.FindProperty("itemPageAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.itemPageAsset");
				}
				if (serialized.FindProperty("bagCellAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.bagCellAsset");
				}
				if (serialized.FindProperty("forgeKindAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.forgeKindAsset");
				}
				if (serialized.FindProperty("battleHudAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.battleHudAsset");
				}
				if (serialized.FindProperty("cardAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.cardAsset");
				}
				if (serialized.FindProperty("queueChipAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.queueChipAsset");
				}
				if (serialized.FindProperty("choiceCardAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.choiceCardAsset");
				}
				if (serialized.FindProperty("waveDotAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.waveDotAsset");
				}

				if (serialized.FindProperty("codexPageAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.codexPageAsset");
				}

				if (serialized.FindProperty("shopPageAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.shopPageAsset");
				}

				if (serialized.FindProperty("labPageAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.labPageAsset");
				}

				if (serialized.FindProperty("dungeonPageAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.dungeonPageAsset");
				}

				if (serialized.FindProperty("investPageAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.investPageAsset");
				}

				if (serialized.FindProperty("producerRowAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.producerRowAsset");
				}

				if (serialized.FindProperty("gearPopupAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.gearPopupAsset");
				}
				if (serialized.FindProperty("mapPopupAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.mapPopupAsset");
				}
				if (serialized.FindProperty("heroPopupAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.heroPopupAsset");
				}
				if (serialized.FindProperty("goldPopupAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.goldPopupAsset");
				}
				if (serialized.FindProperty("settingsPopupAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.settingsPopupAsset");
				}
				if (serialized.FindProperty("awayPopupAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.awayPopupAsset");
				}
				if (serialized.FindProperty("tuningAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.tuningAsset");
				}
				if (serialized.FindProperty("heroCatalogAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.heroCatalogAsset");
				}
				if (serialized.FindProperty("uiContentAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.uiContentAsset");
				}
				if (serialized.FindProperty("gearPresentationAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.gearPresentationAsset");
				}
				if (serialized.FindProperty("runtimeSettingsAsset").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.runtimeSettingsAsset");
				}
				if (serialized.FindProperty("stage").objectReferenceValue == null)
				{
					missing.Add("BattleScreen.stage (무대 없이 HUD 만 뜬다)");
				}
			}

			if (stage != null)
			{
				SerializedObject serializedStage = new SerializedObject(stage);
				if (serializedStage.FindProperty("presentationAsset").objectReferenceValue == null)
				{
					missing.Add("BattleStage.presentationAsset");
				}
			}

			if (missing.Count > 0)
			{
				Debug.LogError(TAG + " 씬이 비었다 — " + string.Join(" · ", missing));
				return false;
			}

			Debug.Log(TAG + " 검사 통과 — 붙을 것이 다 붙어 있다 (" + scene.name + ")");
			return true;
		}

		private static void AssignPrivateField(Object target, string fieldName, Object value)
		{
			SerializedObject serialized = new SerializedObject(target);
			serialized.FindProperty(fieldName).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void AddToBuildSettings()
		{
			List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

			foreach (EditorBuildSettingsScene listed in scenes)
			{
				if (listed.path == SCENE_PATH)
				{
					return;
				}
			}

			scenes.Add(new EditorBuildSettingsScene(SCENE_PATH, true));
			EditorBuildSettings.scenes = scenes.ToArray();
		}
	}
}
