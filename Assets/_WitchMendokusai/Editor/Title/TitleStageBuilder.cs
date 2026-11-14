using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	// 시작 화면 무대 프리팹 굽기와 Lobby 씬 설치
	public static partial class TitleStageBuilder
	{
		private const string FOLDER = "Assets/_WitchMendokusai/Domain/Title/Stage";
		private const string RECIPE_PATH = FOLDER + "/Title_0_StageRecipe.asset";
		private const string PREFAB_PATH = FOLDER + "/[Stage] Title.prefab";
		private const string GROUND_MATERIAL_PATH = FOLDER + "/TitleGround.mat";
		private const string GRASS_MATERIAL_PATH = FOLDER + "/TitleGrass.mat";
		private const string LEAF_MATERIAL_PATH = FOLDER + "/TitleLeaf.mat";
		private const string FLOWER_MATERIAL_PATH = FOLDER + "/TitleFlower{0}.mat";
		private const string SKY_PRESET_PATH = FOLDER + "/SkyPreset_1_Title.asset";
		private const string LOGO_FONT_PATH = FOLDER + "/Font_1_TitleLogo.asset";
		private const string SUBTITLE_FONT_PATH = FOLDER + "/Font_2_TitleSubtitle.asset";
		private const string LOBBY_SCENE_PATH = "Assets/_WitchMendokusai/Scenes/Lobby.unity";
		private const string STAGE_INSTANCE_NAME = "[Stage] Title";
		private const string SCENERY_NAME = "[Title] Scenery";
		private const string SUN_NAME = "[Title] Sun";
		private const string LOBBY_BACKGROUND_PATH = "Canvas/[Block] Background";
		private const string LOBBY_TITLE_PATH = "Canvas/[Block] Texts/[Text] Title";
		private const string LOBBY_VERSION_PATH = "Canvas/[Block] Texts/[Text] Version";
		private const string LOBBY_COPYRIGHT_PATH = "Canvas/[Block] Texts/[Text] CopyRight";
		private const string LOBBY_BUTTONS_PATH = "Canvas/[Block] Buttons";
		private const string LOBBY_TEXTS_PATH = "Canvas/[Block] Texts";
		private const string LOGO_NAME = "[Title] Logo";
		private const string PRESS_NAME = "[Title] Press";

		// 기본 참조 (레시피를 새로 만들 때만 사용)
		private const string GROUND_SOURCE_GUID = "96cb66d3f55ab69458576aa757bd763a";
		private const string GRASS_SOURCE_GUID = "2a27b3985f33f324185d30511b4aec4a";
		private const string GRASS_LIT_GUID = "9cf35727358132d48aa379b2e13ba56f";
		private const string TREE_GUID = "c0c27574d0d91ea44894908f417f36d4";
		private const string GRASS_GUID = "a26a53e463177194db6585765f564431";
		private const string SHRUB_GUID = "3540440509c30ba49b2125f242729c46";
		private const string GRAVEL_GUID = "871875c89c8ec7142a27c6271f62c2e0";
		private const string SKY_PRESET_GUID = "7dfb6657b0a0df944b421b00a3a88c09";
		private const string SKYBOX_GUID = "282b50081561f8f4289b7d1b999c03de";
		private const string CHARACTER_MODEL_GUID = "8403658c3298ac34db76445e300408d8";
		private const string CHARACTER_ANIMATOR_GUID = "a54ae9bdcf3a33143b27ba56208bee76";
		private const string LEAF_SOURCE_GUID = "4489b195897487d43bb2ed6b4f9ae62f";
		private const string LOGO_FONT_GUID = "45ed480f13a258f468b3269727966fa4";
		private const string SUBTITLE_FONT_GUID = "7ab417d8e5984216b4465d412b63103c";

		[MenuItem("WM/Title/Build Title Stage")]
		public static void BuildStage()
		{
			TitleStageRecipe recipe = LoadOrCreateRecipe();
			Material ground = LoadOrCreateGroundMaterial(recipe);
			Dictionary<Material, Material> swaps = BuildMaterialSwaps(recipe);
			System.Random random = new(recipe.Seed);

			GameObject root = new(STAGE_INSTANCE_NAME);
			GameObject terrainObject = new("Terrain", typeof(MeshFilter), typeof(MeshRenderer), typeof(TitleTerrain));
			terrainObject.transform.SetParent(root.transform, false);
			terrainObject.GetComponent<MeshRenderer>().sharedMaterial = ground;
			TitleTerrain terrain = terrainObject.GetComponent<TitleTerrain>();
			terrain.Build();

			Transform props = new GameObject("Props").transform;
			props.SetParent(root.transform, false);

			Place(swaps, terrain, props, recipe.TreePrefab, recipe.BigTreePosition, recipe.BigTreeScale, (float)random.NextDouble() * 360f);

			if (recipe.CharacterModel != null)
			{
				Vector2 characterPoint = recipe.BigTreePosition + recipe.CharacterOffset;
				GameObject character = Place(swaps, terrain, props, recipe.CharacterModel, characterPoint, recipe.CharacterScale, recipe.CharacterYaw);
				character.name = "Character";
				Animator animator = character.GetComponentInChildren<Animator>();
				if (animator == null)
					animator = character.AddComponent<Animator>();
				animator.runtimeAnimatorController = recipe.CharacterAnimator;
			}

			for (int i = 0; i < recipe.ForestCount; i++)
			{
				Vector2 point = RandomRing(random, recipe.ForestRadius.x, recipe.ForestRadius.y, recipe.ForestFrontGapDegrees);
				Place(swaps, terrain, props, recipe.TreePrefab, point, Lerp(random, recipe.ForestScale), (float)random.NextDouble() * 360f);
			}
			for (int i = 0; i < recipe.ShrubCount; i++)
			{
				Vector2 point = RandomDisk(random, recipe.ShrubRadius);
				if (point.y < recipe.ShrubFrontLimit)
					point.y = -point.y;
				Place(swaps, terrain, props, recipe.ShrubPrefab, point, Lerp(random, recipe.ShrubScale), (float)random.NextDouble() * 360f);
			}
			for (int i = 0; i < recipe.GrassCount; i++)
				Place(swaps, terrain, props, recipe.GrassPrefab, RandomDisk(random, recipe.GrassRadius), Lerp(random, recipe.GrassScale), (float)random.NextDouble() * 360f);
			for (int i = 0; i < recipe.GravelCount; i++)
				Place(swaps, terrain, props, recipe.GravelPrefab, RandomDisk(random, recipe.GravelRadius), 1f, (float)random.NextDouble() * 360f);

			GameObject flowerObject = new("Flowers", typeof(MeshFilter), typeof(MeshRenderer), typeof(TitleFlowerField));
			flowerObject.transform.SetParent(root.transform, false);
			flowerObject.GetComponent<MeshRenderer>().sharedMaterials = LoadOrCreateFlowerMaterials(recipe);
			flowerObject.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			SerializedObject flowerField = new(flowerObject.GetComponent<TitleFlowerField>());
			flowerField.FindProperty("terrain").objectReferenceValue = terrain;
			flowerField.ApplyModifiedPropertiesWithoutUndo();

			// 메시는 런타임 생성이라 프리팹에 넣지 않음
			terrainObject.GetComponent<MeshFilter>().sharedMesh = null;
			flowerObject.GetComponent<MeshFilter>().sharedMesh = null;
			PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
			Object.DestroyImmediate(root);
			AssetDatabase.SaveAssets();
			Debug.Log($"[TitleStageBuilder] {PREFAB_PATH} saved");
		}

		[MenuItem("WM/Title/Install Into Lobby")]
		public static void InstallIntoLobby()
		{
			TitleStageRecipe recipe = LoadOrCreateRecipe();
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
			if (prefab == null)
				throw new System.InvalidOperationException($"{PREFAB_PATH} 없음. Build Title Stage 먼저");

			Scene scene = EditorSceneManager.OpenScene(LOBBY_SCENE_PATH, OpenSceneMode.Single);
			DestroyRoot(scene, STAGE_INSTANCE_NAME);
			DestroyRoot(scene, SCENERY_NAME);
			DestroyRoot(scene, SUN_NAME);

			GameObject stage = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
			TitleTerrain terrain = stage.GetComponentInChildren<TitleTerrain>();

			GameObject sunObject = new(SUN_NAME, typeof(Light));
			SceneManager.MoveGameObjectToScene(sunObject, scene);
			Light sun = sunObject.GetComponent<Light>();
			sun.type = LightType.Directional;
			sun.shadows = LightShadows.Soft;

			Camera camera = FindMainCamera(scene);
			Vector3 cameraPosition = recipe.CameraPosition;
			cameraPosition.y += terrain.HeightAt(cameraPosition.x, cameraPosition.z);
			camera.transform.SetPositionAndRotation(cameraPosition, Quaternion.Euler(recipe.CameraEuler));
			camera.orthographic = false;
			camera.fieldOfView = recipe.CameraFieldOfView;
			camera.clearFlags = CameraClearFlags.Skybox;
			camera.farClipPlane = 1000f;

			GameObject sceneryObject = new(SCENERY_NAME, typeof(TitleScenery));
			SceneManager.MoveGameObjectToScene(sceneryObject, scene);
			SerializedObject scenery = new(sceneryObject.GetComponent<TitleScenery>());
			scenery.FindProperty("skyPreset").objectReferenceValue = LoadOrCreateSkyPreset(recipe);
			scenery.FindProperty("fogDensityScale").floatValue = recipe.FogDensityScale;
			scenery.FindProperty("postSaturation").floatValue = recipe.PostSaturation;
			scenery.FindProperty("postExposure").floatValue = recipe.PostExposure;
			scenery.FindProperty("skyboxMaterial").objectReferenceValue = recipe.SkyboxMaterial;
			scenery.FindProperty("hour").floatValue = recipe.Hour;
			scenery.FindProperty("sun").objectReferenceValue = sun;
			scenery.FindProperty("cameraTransform").objectReferenceValue = camera.transform;
			scenery.ApplyModifiedPropertiesWithoutUndo();
			sceneryObject.GetComponent<TitleScenery>().ApplySky();

			GameObject background = GameObject.Find(LOBBY_BACKGROUND_PATH);
			if (background != null)
				background.SetActive(false);
			LayoutLobbyUI(recipe, sceneryObject);

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("[TitleStageBuilder] Lobby installed");
		}

		// C 시안: 풍경 위 가운데 가는 영문 로고, 아래 안내 한 줄. 키를 누르면 그 자리에 세로 메뉴
		private static void LayoutLobbyUI(TitleStageRecipe recipe, GameObject sceneryObject)
		{
			TMP_FontAsset logoFont = LoadOrCreateFont(recipe.LogoFontSource, LOGO_FONT_PATH, recipe);
			TMP_FontAsset subtitleFont = LoadOrCreateFont(recipe.SubtitleFontSource, SUBTITLE_FONT_PATH, recipe);
			Transform texts = GameObject.Find(LOBBY_TEXTS_PATH).transform;
			GameObject.Find(LOBBY_TITLE_PATH).SetActive(false);

			RectTransform logo = Child(texts, LOGO_NAME);
			Anchor(logo, new Vector2(0.5f, 1f), new Vector2(0f, -recipe.LogoTop), new Vector2(recipe.TextRowWidth, 0f));
			float logoHeight = recipe.LogoFontSize * recipe.LineHeightScale;
			TextMeshProUGUI english = Label(Child(logo, "English"), logoFont, recipe.LogoText, recipe.LogoFontSize, recipe.LogoCharacterSpacing, recipe.TextColor);
			Anchor((RectTransform)english.transform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(recipe.TextRowWidth, logoHeight));
			RectTransform rule = Child(logo, "Rule");
			UnityEngine.UI.Image ruleImage = rule.GetComponent<UnityEngine.UI.Image>();
			if (ruleImage == null)
				ruleImage = rule.gameObject.AddComponent<UnityEngine.UI.Image>();
			ruleImage.color = recipe.TextColor;
			ruleImage.raycastTarget = false;
			Anchor(rule, new Vector2(0.5f, 1f), new Vector2(0f, -(logoHeight + recipe.RuleGap)), new Vector2(recipe.RuleWidth, recipe.RuleThickness));
			TextMeshProUGUI korean = Label(Child(logo, "Korean"), subtitleFont, recipe.SubtitleText, recipe.SubtitleFontSize, recipe.SubtitleCharacterSpacing, recipe.TextColor);
			Anchor((RectTransform)korean.transform, new Vector2(0.5f, 1f), new Vector2(0f, -(logoHeight + recipe.RuleGap * 2f + recipe.RuleThickness)), new Vector2(recipe.TextRowWidth, recipe.SubtitleFontSize * recipe.LineHeightScale));

			RectTransform pressRect = Child(texts, PRESS_NAME);
			TextMeshProUGUI press = Label(pressRect, logoFont, recipe.PressText, recipe.PressFontSize, recipe.PressCharacterSpacing, recipe.TextColor);
			Anchor(pressRect, new Vector2(0.5f, 0f), new Vector2(0f, recipe.PressBottom), new Vector2(recipe.TextRowWidth, recipe.PressFontSize * recipe.LineHeightScale));

			foreach (string path in new[] { LOBBY_VERSION_PATH, LOBBY_COPYRIGHT_PATH })
			{
				TextMeshProUGUI footer = GameObject.Find(path).GetComponent<TextMeshProUGUI>();
				Style(footer, logoFont, recipe.FooterFontSize, recipe.FooterCharacterSpacing, recipe.FooterColor);
				footer.fontStyle = FontStyles.UpperCase;
			}
			Anchor((RectTransform)GameObject.Find(LOBBY_VERSION_PATH).transform, new Vector2(1f, 0f), new Vector2(-recipe.FooterMargin.x, recipe.FooterMargin.y), recipe.FooterSize);
			Anchor((RectTransform)GameObject.Find(LOBBY_COPYRIGHT_PATH).transform, new Vector2(0f, 0f), recipe.FooterMargin, recipe.FooterSize);
			Align(LOBBY_VERSION_PATH, TextAlignmentOptions.BottomRight);
			Align(LOBBY_COPYRIGHT_PATH, TextAlignmentOptions.BottomLeft);

			GameObject buttons = GameObject.Find(LOBBY_BUTTONS_PATH);
			UnityEngine.UI.GridLayoutGroup grid = buttons.GetComponent<UnityEngine.UI.GridLayoutGroup>();
			grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
			grid.constraintCount = 1;
			grid.cellSize = recipe.MenuCellSize;
			grid.spacing = new Vector2(0f, recipe.MenuSpacing);
			grid.childAlignment = TextAnchor.LowerCenter;
			// 옛 한 줄 배치의 아래 여백 200 이 남아 메뉴가 위로 밀림
			grid.padding = new RectOffset();
			EditorUtility.SetDirty(grid);
			// 디버그 빌드는 멀티 버튼이 하나 더 붙음. 그 줄 수까지 들어갈 높이를 잡고 아래로 맞춤
			float menuHeight = recipe.MenuCellSize.y * recipe.MenuMaxRows + recipe.MenuSpacing * (recipe.MenuMaxRows - 1);
			Anchor((RectTransform)buttons.transform, new Vector2(0.5f, 0f), new Vector2(0f, recipe.MenuBottom), new Vector2(recipe.MenuCellSize.x, menuHeight));
			CanvasGroup menuGroup = buttons.GetComponent<CanvasGroup>();
			if (menuGroup == null)
				menuGroup = buttons.AddComponent<CanvasGroup>();
			foreach (UnityEngine.UI.Button button in buttons.GetComponentsInChildren<UnityEngine.UI.Button>(true))
				StyleButton(recipe, button, subtitleFont);

			TitleIntro intro = sceneryObject.GetComponent<TitleIntro>();
			if (intro == null)
				intro = sceneryObject.AddComponent<TitleIntro>();
			SerializedObject introObject = new(intro);
			introObject.FindProperty("menuGroup").objectReferenceValue = menuGroup;
			introObject.FindProperty("pressText").objectReferenceValue = press;
			introObject.ApplyModifiedPropertiesWithoutUndo();
		}

		// 판 없는 글자 버튼
		private static void StyleButton(TitleStageRecipe recipe, UnityEngine.UI.Button button, TMP_FontAsset font)
		{
			// 판 이미지가 Mask 그래픽이라 투명하게 하면 글자까지 가려짐. Mask 와 무늬 끄기
			UnityEngine.UI.Mask mask = button.GetComponent<UnityEngine.UI.Mask>();
			if (mask != null)
			{
				mask.enabled = false;
				EditorUtility.SetDirty(mask);
			}
			foreach (UnityEngine.UI.Image decoration in button.GetComponentsInChildren<UnityEngine.UI.Image>(true))
			{
				if (decoration.gameObject != button.gameObject)
				{
					decoration.enabled = false;
					EditorUtility.SetDirty(decoration);
				}
			}
			TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
			if (label != null)
			{
				Style(label, font, recipe.MenuFontSize, recipe.MenuCharacterSpacing, recipe.TextColor);
				label.alignment = TextAlignmentOptions.Center;
			}
			UnityEngine.UI.Image plate = button.GetComponent<UnityEngine.UI.Image>();
			if (plate != null)
			{
				Color color = plate.color;
				color.a = 0f;
				plate.color = color;
				EditorUtility.SetDirty(plate);
			}
		}

		private static TextMeshProUGUI Label(RectTransform rect, TMP_FontAsset font, string text, float size, float spacing, Color color)
		{
			TextMeshProUGUI label = rect.GetComponent<TextMeshProUGUI>();
			if (label == null)
				label = rect.gameObject.AddComponent<TextMeshProUGUI>();
			label.text = text;
			label.alignment = TextAlignmentOptions.Center;
			label.textWrappingMode = TextWrappingModes.NoWrap;
			label.raycastTarget = false;
			Style(label, font, size, spacing, color);
			return label;
		}

		private static void Style(TextMeshProUGUI label, TMP_FontAsset font, float size, float spacing, Color color)
		{
			label.font = font;
			label.fontSharedMaterial = font.material;
			label.enableAutoSizing = false;
			label.fontSize = size;
			label.characterSpacing = spacing;
			label.color = color;
			label.fontStyle = FontStyles.Normal;
			// 넓은 글자 간격에서 짝 간격이 섞이면 간격이 들쭉날쭉
			label.fontFeatures.Remove(UnityEngine.TextCore.OTL_FeatureTag.kern);
			EditorUtility.SetDirty(label);
		}

		private static RectTransform Child(Transform parent, string name)
		{
			Transform found = parent.Find(name);
			if (found == null)
			{
				GameObject created = new(name, typeof(RectTransform));
				created.layer = parent.gameObject.layer;
				created.transform.SetParent(parent, false);
				found = created.transform;
			}
			return (RectTransform)found;
		}

		private static void Align(string path, TextAlignmentOptions alignment)
		{
			TextMeshProUGUI text = GameObject.Find(path).GetComponent<TextMeshProUGUI>();
			text.alignment = alignment;
			EditorUtility.SetDirty(text);
		}

		private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
		{
			rect.anchorMin = anchor;
			rect.anchorMax = anchor;
			rect.pivot = anchor;
			rect.anchoredPosition = offset;
			rect.sizeDelta = size;
			EditorUtility.SetDirty(rect);
		}

		private static GameObject Place(Dictionary<Material, Material> swaps, TitleTerrain terrain, Transform parent, GameObject prefab, Vector2 point, float scale, float yaw)
		{
			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			instance.transform.SetParent(parent, false);
			instance.transform.localPosition = new Vector3(point.x, terrain.HeightAt(point.x, point.y), point.y);
			instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
			instance.transform.localScale = Vector3.one * scale;
			foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
			{
				Material[] materials = renderer.sharedMaterials;
				bool changed = false;
				for (int i = 0; i < materials.Length; i++)
				{
					if (materials[i] != null && swaps.TryGetValue(materials[i], out Material replacement))
					{
						materials[i] = replacement;
						changed = true;
					}
				}
				if (changed)
					renderer.sharedMaterials = materials;
			}
			return instance;
		}

		private static Vector2 RandomDisk(System.Random random, float radius)
		{
			float angle = (float)random.NextDouble() * Mathf.PI * 2f;
			float distance = Mathf.Sqrt((float)random.NextDouble()) * radius;
			return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
		}

		private static Vector2 RandomRing(System.Random random, float inner, float outer, float frontGapDegrees)
		{
			// 카메라 방향 (-z, 각도 270) 주변은 비움
			float gap = frontGapDegrees * 0.5f;
			float angle = Mathf.Lerp(270f + gap, 270f + 360f - gap, (float)random.NextDouble()) * Mathf.Deg2Rad;
			float distance = Mathf.Lerp(inner, outer, (float)random.NextDouble());
			return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
		}

		private static float Lerp(System.Random random, Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

		private static TitleStageRecipe LoadOrCreateRecipe()
		{
			TitleStageRecipe recipe = AssetDatabase.LoadAssetAtPath<TitleStageRecipe>(RECIPE_PATH);
			if (recipe != null)
				return recipe;
			System.IO.Directory.CreateDirectory(FOLDER);
			recipe = ScriptableObject.CreateInstance<TitleStageRecipe>();
			recipe.GroundSource = Load<Material>(GROUND_SOURCE_GUID);
			recipe.GrassSource = Load<Material>(GRASS_SOURCE_GUID);
			recipe.GrassLit = Load<Material>(GRASS_LIT_GUID);
			recipe.TreePrefab = Load<GameObject>(TREE_GUID);
			recipe.GrassPrefab = Load<GameObject>(GRASS_GUID);
			recipe.ShrubPrefab = Load<GameObject>(SHRUB_GUID);
			recipe.GravelPrefab = Load<GameObject>(GRAVEL_GUID);
			recipe.SkyPreset = Load<SkyPresetSO>(SKY_PRESET_GUID);
			recipe.SkyboxMaterial = Load<Material>(SKYBOX_GUID);
			recipe.CharacterModel = Load<GameObject>(CHARACTER_MODEL_GUID);
			recipe.CharacterAnimator = Load<RuntimeAnimatorController>(CHARACTER_ANIMATOR_GUID);
			recipe.LeafSource = Load<Material>(LEAF_SOURCE_GUID);
			recipe.LogoFontSource = Load<Font>(LOGO_FONT_GUID);
			recipe.SubtitleFontSource = Load<Font>(SUBTITLE_FONT_GUID);
			AssetDatabase.CreateAsset(recipe, RECIPE_PATH);
			return recipe;
		}

		private static T Load<T>(string guid) where T : Object => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

		private static void DestroyRoot(Scene scene, string name)
		{
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				if (root.name == name)
					Object.DestroyImmediate(root);
			}
		}

		private static Camera FindMainCamera(Scene scene)
		{
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				Camera camera = root.GetComponentInChildren<Camera>(true);
				if (camera != null && root.CompareTag("MainCamera"))
					return camera;
			}
			foreach (GameObject root in scene.GetRootGameObjects())
			{
				Camera camera = root.GetComponentInChildren<Camera>(true);
				if (camera != null)
					return camera;
			}
			throw new System.InvalidOperationException("Lobby 씬에 카메라 없음");
		}
	}
}
