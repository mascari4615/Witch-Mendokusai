using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WitchMendokusai
{
	// 시작 화면 무대 프리팹 굽기와 Lobby 씬 설치
	public static class TitleStageBuilder
	{
		private const string FOLDER = "Assets/_WitchMendokusai/Domain/Title/Stage";
		private const string RECIPE_PATH = FOLDER + "/TitleStageRecipe.asset";
		private const string PREFAB_PATH = FOLDER + "/[Stage] Title.prefab";
		private const string GROUND_MATERIAL_PATH = FOLDER + "/TitleGround.mat";
		private const string LOBBY_SCENE_PATH = "Assets/_WitchMendokusai/Scenes/Lobby.unity";
		private const string STAGE_INSTANCE_NAME = "[Stage] Title";
		private const string SCENERY_NAME = "[Title] Scenery";
		private const string SUN_NAME = "[Title] Sun";
		private const string LOBBY_BACKGROUND_PATH = "Canvas/[Block] Background";
		private const string LOBBY_TITLE_PATH = "Canvas/[Block] Texts/[Text] Title";
		private const string LOBBY_VERSION_PATH = "Canvas/[Block] Texts/[Text] Version";
		private const string LOBBY_COPYRIGHT_PATH = "Canvas/[Block] Texts/[Text] CopyRight";
		private const string LOBBY_BUTTONS_PATH = "Canvas/[Block] Buttons";

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

		[MenuItem("WM/Title/Build Title Stage")]
		public static void BuildStage()
		{
			TitleStageRecipe recipe = LoadOrCreateRecipe();
			Material ground = LoadOrCreateGroundMaterial(recipe);
			System.Random random = new(recipe.Seed);

			GameObject root = new(STAGE_INSTANCE_NAME);
			GameObject terrainObject = new("Terrain", typeof(MeshFilter), typeof(MeshRenderer), typeof(TitleTerrain));
			terrainObject.transform.SetParent(root.transform, false);
			terrainObject.GetComponent<MeshRenderer>().sharedMaterial = ground;
			TitleTerrain terrain = terrainObject.GetComponent<TitleTerrain>();
			terrain.Build();

			Transform props = new GameObject("Props").transform;
			props.SetParent(root.transform, false);

			Place(recipe, terrain, props, recipe.TreePrefab, recipe.BigTreePosition, recipe.BigTreeScale, (float)random.NextDouble() * 360f);

			if (recipe.CharacterModel != null)
			{
				Vector2 characterPoint = recipe.BigTreePosition + recipe.CharacterOffset;
				GameObject character = Place(recipe, terrain, props, recipe.CharacterModel, characterPoint, recipe.CharacterScale, recipe.CharacterYaw);
				character.name = "Character";
				Animator animator = character.GetComponentInChildren<Animator>();
				if (animator == null)
					animator = character.AddComponent<Animator>();
				animator.runtimeAnimatorController = recipe.CharacterAnimator;
			}

			for (int i = 0; i < recipe.ForestCount; i++)
			{
				Vector2 point = RandomRing(random, recipe.ForestRadius.x, recipe.ForestRadius.y, recipe.ForestFrontGapDegrees);
				Place(recipe, terrain, props, recipe.TreePrefab, point, Lerp(random, recipe.ForestScale), (float)random.NextDouble() * 360f);
			}
			for (int i = 0; i < recipe.ShrubCount; i++)
			{
				Vector2 point = RandomDisk(random, recipe.ShrubRadius);
				if (point.y < recipe.ShrubFrontLimit)
					point.y = -point.y;
				Place(recipe, terrain, props, recipe.ShrubPrefab, point, Lerp(random, recipe.ShrubScale), (float)random.NextDouble() * 360f);
			}
			for (int i = 0; i < recipe.GrassCount; i++)
				Place(recipe, terrain, props, recipe.GrassPrefab, RandomDisk(random, recipe.GrassRadius), Lerp(random, recipe.GrassScale), (float)random.NextDouble() * 360f);
			for (int i = 0; i < recipe.GravelCount; i++)
				Place(recipe, terrain, props, recipe.GravelPrefab, RandomDisk(random, recipe.GravelRadius), 1f, (float)random.NextDouble() * 360f);

			// 메시는 런타임 생성이라 프리팹에 넣지 않음
			terrainObject.GetComponent<MeshFilter>().sharedMesh = null;
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
			scenery.FindProperty("skyPreset").objectReferenceValue = recipe.SkyPreset;
			scenery.FindProperty("skyboxMaterial").objectReferenceValue = recipe.SkyboxMaterial;
			scenery.FindProperty("hour").floatValue = recipe.Hour;
			scenery.FindProperty("sun").objectReferenceValue = sun;
			scenery.FindProperty("cameraTransform").objectReferenceValue = camera.transform;
			scenery.ApplyModifiedPropertiesWithoutUndo();
			sceneryObject.GetComponent<TitleScenery>().ApplySky();

			GameObject background = GameObject.Find(LOBBY_BACKGROUND_PATH);
			if (background != null)
				background.SetActive(false);
			LayoutLobbyUI(recipe);

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("[TitleStageBuilder] Lobby installed");
		}

		// 무대가 보이게 제목은 하늘 위, 메뉴는 아래 한 줄, 판 없는 글자 버튼
		private static void LayoutLobbyUI(TitleStageRecipe recipe)
		{
			Anchor(LOBBY_TITLE_PATH, new Vector2(0.5f, 1f), recipe.TitleOffset, recipe.TitleSize);
			Anchor(LOBBY_BUTTONS_PATH, new Vector2(0.5f, 0f), recipe.MenuOffset, recipe.MenuSize);
			Anchor(LOBBY_VERSION_PATH, new Vector2(1f, 0f), new Vector2(-recipe.FooterMargin.x, recipe.FooterMargin.y), recipe.FooterSize);
			Anchor(LOBBY_COPYRIGHT_PATH, new Vector2(0f, 0f), recipe.FooterMargin, recipe.FooterSize);
			Align(LOBBY_VERSION_PATH, TMPro.TextAlignmentOptions.BottomRight);
			Align(LOBBY_COPYRIGHT_PATH, TMPro.TextAlignmentOptions.BottomLeft);
			GameObject buttons = GameObject.Find(LOBBY_BUTTONS_PATH);
			foreach (UnityEngine.UI.Button button in buttons.GetComponentsInChildren<UnityEngine.UI.Button>(true))
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
				TMPro.TextMeshProUGUI label = button.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
				if (label != null)
				{
					label.enableAutoSizing = false;
					label.fontSize = recipe.MenuFontSize;
					EditorUtility.SetDirty(label);
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
		}

		private static void Align(string path, TMPro.TextAlignmentOptions alignment)
		{
			TMPro.TextMeshProUGUI text = GameObject.Find(path).GetComponent<TMPro.TextMeshProUGUI>();
			text.alignment = alignment;
			EditorUtility.SetDirty(text);
		}

		private static void Anchor(string path, Vector2 anchor, Vector2 offset, Vector2 size)
		{
			GameObject target = GameObject.Find(path);
			if (target == null)
				throw new System.InvalidOperationException($"{path} 없음");
			RectTransform rect = (RectTransform)target.transform;
			rect.anchorMin = anchor;
			rect.anchorMax = anchor;
			rect.pivot = anchor;
			rect.anchoredPosition = offset;
			rect.sizeDelta = size;
			EditorUtility.SetDirty(rect);
		}

		private static GameObject Place(TitleStageRecipe recipe, TitleTerrain terrain, Transform parent, GameObject prefab, Vector2 point, float scale, float yaw)
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
					if (materials[i] == recipe.GrassSource && recipe.GrassLit != null)
					{
						materials[i] = recipe.GrassLit;
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
			AssetDatabase.CreateAsset(recipe, RECIPE_PATH);
			return recipe;
		}

		private static Material LoadOrCreateGroundMaterial(TitleStageRecipe recipe)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(GROUND_MATERIAL_PATH);
			if (material == null)
			{
				material = new Material(recipe.GroundSource);
				AssetDatabase.CreateAsset(material, GROUND_MATERIAL_PATH);
			}
			else
			{
				material.CopyPropertiesFromMaterial(recipe.GroundSource);
			}
			if (material.HasProperty("_BaseColor"))
				material.SetColor("_BaseColor", recipe.GroundSource.GetColor("_BaseColor") * recipe.GroundColorScale);
			if (material.HasProperty("_Color"))
				material.SetColor("_Color", recipe.GroundSource.GetColor("_Color") * recipe.GroundColorScale);
			EditorUtility.SetDirty(material);
			return material;
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
