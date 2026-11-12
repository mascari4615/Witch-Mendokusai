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

			for (int i = 0; i < recipe.ForestCount; i++)
			{
				Vector2 point = RandomRing(random, recipe.ForestRadius.x, recipe.ForestRadius.y, recipe.ForestFrontGapDegrees);
				Place(recipe, terrain, props, recipe.TreePrefab, point, Lerp(random, recipe.ForestScale), (float)random.NextDouble() * 360f);
			}
			for (int i = 0; i < recipe.ShrubCount; i++)
				Place(recipe, terrain, props, recipe.ShrubPrefab, RandomDisk(random, recipe.ShrubRadius), Lerp(random, recipe.ShrubScale), (float)random.NextDouble() * 360f);
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

			PrefabUtility.InstantiatePrefab(prefab, scene);

			GameObject sunObject = new(SUN_NAME, typeof(Light));
			SceneManager.MoveGameObjectToScene(sunObject, scene);
			Light sun = sunObject.GetComponent<Light>();
			sun.type = LightType.Directional;
			sun.shadows = LightShadows.Soft;

			Camera camera = FindMainCamera(scene);
			camera.transform.SetPositionAndRotation(recipe.CameraPosition, Quaternion.Euler(recipe.CameraEuler));
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

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log("[TitleStageBuilder] Lobby installed");
		}

		private static void Place(TitleStageRecipe recipe, TitleTerrain terrain, Transform parent, GameObject prefab, Vector2 point, float scale, float yaw)
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
