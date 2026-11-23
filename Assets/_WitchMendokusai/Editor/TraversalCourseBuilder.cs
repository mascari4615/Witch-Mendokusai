using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	public static class TraversalCourseBuilder
	{
		public const string DIRECTORY = "Assets/_WitchMendokusai/Domain/World/Stage/1001_Traversal";
		public const string STAGE_PATH = DIRECTORY + "/WS_1001_이동시험코스.asset";
		private const string PREFAB_PATH = DIRECTORY + "/Stage_Traversal.prefab";
		private const string DEV_PREFAB = "Assets/_WitchMendokusai/Core/Assets/Singletons/DevWindowController.prefab";
		private const int STAGE_ID = 1001;

		public static void Build()
		{
			if (EditorApplication.isPlaying)
				throw new System.InvalidOperationException("Play 종료 후 코스 생성");
			Directory.CreateDirectory(DIRECTORY);
			AssetDatabase.Refresh();
			Material ground = Material("Ground", new Color(0.24f, 0.32f, 0.38f));
			Material climb = Material("Climb", new Color(0.12f, 0.75f, 0.68f));
			Material goal = Material("Goal", new Color(1f, 0.68f, 0.15f));
			GameObject root = new("Stage_Traversal");
			root.SetActive(false);
			try
			{
				root.AddComponent<StageObject>();
				TraversalCourse course = root.AddComponent<TraversalCourse>();
				GameObject start = Box(root, "Start", new Vector3(0f, -0.5f, 5f), new Vector3(12f, 1f, 16f), ground);
				Box(root, "Low ceiling", new Vector3(-3f, 0.9f, 5f), new Vector3(3f, 0.25f, 4f), goal);
				for (int i = 0; i < 3; i++)
					Box(root, "Step " + i, new Vector3(3f, 0.1f * (i + 1), 3f + i), new Vector3(3f, 0.2f * (i + 1), 1f), ground);
				Box(root, "Climb wall", new Vector3(0f, 3f, 14f), new Vector3(8f, 6f, 2f), climb).AddComponent<ClimbableSurface>();
				GameObject takeoff = Box(root, "Takeoff", new Vector3(0f, 5.5f, 18f), new Vector3(8f, 1f, 8f), ground);
				GameObject landing = Box(root, "Landing", new Vector3(0f, -0.5f, 40f), new Vector3(12f, 1f, 12f), goal);
				Box(root, "Blocked wall", new Vector3(5f, 1.5f, 10f), new Vector3(1f, 3f, 4f), ground);
				Transform[] checkpoints =
				{
					Anchor(root, "Start checkpoint", Vector3.zero),
					Anchor(root, "Takeoff checkpoint", new Vector3(0f, 6.05f, 19f)),
					Anchor(root, "Goal checkpoint", new Vector3(0f, 0.05f, 37f))
				};
				Label(root, "START  /  MOVE + JUMP", new Vector3(0f, 1.8f, 3f));
				Label(root, "CLIMB  /  PUSH FORWARD", new Vector3(0f, 4f, 12.9f));
				Label(root, "GLIDE  /  SPACE IN AIR", new Vector3(0f, 7.8f, 21f));
				Label(root, "LAND HERE", new Vector3(0f, 2f, 42f));
				SerializedObject courseData = new(course);
				SerializedProperty checkpointData = courseData.FindProperty("checkpoints");
				checkpointData.arraySize = checkpoints.Length;
				for (int i = 0; i < checkpoints.Length; i++)
					checkpointData.GetArrayElementAtIndex(i).objectReferenceValue = checkpoints[i];
				Collider[] platforms = { start.GetComponent<Collider>(), takeoff.GetComponent<Collider>(), landing.GetComponent<Collider>() };
				SerializedProperty platformData = courseData.FindProperty("checkpointPlatforms");
				platformData.arraySize = platforms.Length;
				for (int i = 0; i < platforms.Length; i++)
					platformData.GetArrayElementAtIndex(i).objectReferenceValue = platforms[i];
				courseData.FindProperty("hudStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_WitchMendokusai/Domain/World/Stage/TraversalCourse.uss");
				courseData.ApplyModifiedPropertiesWithoutUndo();
				root.SetActive(true);
				GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
				WorldStage stage = AssetDatabase.LoadAssetAtPath<WorldStage>(STAGE_PATH);
				if (stage == null)
				{
					stage = ScriptableObject.CreateInstance<WorldStage>();
					AssetDatabase.CreateAsset(stage, STAGE_PATH);
				}
				stage.ID = STAGE_ID;
				stage.Name = "이동 시험 코스";
				stage.Description = "지상 이동, 6m 등반, 12m 활공 간격, 체크포인트 복귀";
				SerializedObject stageData = new(stage);
				stageData.FindProperty("<DisplayName>k__BackingField").stringValue = stage.Name;
				stageData.FindProperty("<Type>k__BackingField").enumValueIndex = 0;
				stageData.FindProperty("<ShareWorldPosition>k__BackingField").boolValue = false;
				stageData.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab.GetComponent<StageObject>();
				stageData.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(stage);
				GameObject dev = PrefabUtility.LoadPrefabContents(DEV_PREFAB);
				try
				{
					SerializedObject devData = new(dev.GetComponent<DevWindowController>());
					devData.FindProperty("traversalStage").objectReferenceValue = stage;
					devData.ApplyModifiedPropertiesWithoutUndo();
					PrefabUtility.SaveAsPrefabAsset(dev, DEV_PREFAB);
				}
				finally
				{
					PrefabUtility.UnloadPrefabContents(dev);
				}
				BuildGlider(goal);
				ExplorationCourseBuilder.Build(ground, climb, goal);
				AssetDatabase.SaveAssets();
				Debug.Log("[TraversalCourse] Stage 1001 생성. World의 개발창 > 이동 시험에서 입장");
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static Material Material(string name, Color color)
		{
			string path = DIRECTORY + "/" + name + ".mat";
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material == null)
			{
				material = CombatPrimitive.CreateMaterial(unlit: true);
				AssetDatabase.CreateAsset(material, path);
			}
			Material shaderSource = CombatPrimitive.CreateMaterial(unlit: true);
			material.shader = shaderSource.shader;
			Object.DestroyImmediate(shaderSource);
			material.color = color;
			EditorUtility.SetDirty(material);
			return material;
		}

		private static void BuildGlider(Material material)
		{
			const string PLAYER_PATH = "Assets/_WitchMendokusai/Domain/Doll/_Common/Player.prefab";
			GameObject player = PrefabUtility.LoadPrefabContents(PLAYER_PATH);
			try
			{
				Transform existing = player.transform.Find("TraversalGlider");
				GameObject glider = existing != null ? existing.gameObject : new GameObject("TraversalGlider");
				glider.transform.SetParent(player.transform, false);
				glider.transform.localPosition = Vector3.up * 1.35f;
				if (existing == null)
				{
					GameObject wing = Box(glider, "Canopy", Vector3.zero, new Vector3(2f, 0.07f, 0.8f), material);
					Object.DestroyImmediate(wing.GetComponent<Collider>());
				}
				TraversalVisual visual = player.GetComponent<TraversalVisual>();
				if (visual == null)
					visual = player.AddComponent<TraversalVisual>();
				SerializedObject visualData = new(visual);
				visualData.FindProperty("glider").objectReferenceValue = glider;
				visualData.ApplyModifiedPropertiesWithoutUndo();
				glider.SetActive(false);
				PrefabUtility.SaveAsPrefabAsset(player, PLAYER_PATH);
			}
			finally
			{
				PrefabUtility.UnloadPrefabContents(player);
			}
		}

		internal static GameObject Box(GameObject root, string name, Vector3 position, Vector3 size, Material material)
		{
			GameObject block = CombatPrimitive.Create(PrimitiveType.Cube);
			Object.DestroyImmediate(block.GetComponent<Renderer>().sharedMaterial);
			block.GetComponent<Renderer>().sharedMaterial = material;
			block.name = name;
			block.transform.SetParent(root.transform, false);
			block.transform.localPosition = position;
			block.transform.localScale = size;
			block.AddComponent<GroundSurface>();
			return block;
		}

		internal static Transform Anchor(GameObject root, string name, Vector3 position)
		{
			GameObject anchor = new(name);
			anchor.transform.SetParent(root.transform, false);
			anchor.transform.localPosition = position;
			return anchor.transform;
		}

		internal static void Label(GameObject root, string text, Vector3 position)
		{
			TextMeshPro label = Anchor(root, text, position).gameObject.AddComponent<TextMeshPro>();
			label.font = TMP_Settings.defaultFontAsset;
			label.text = text;
			label.fontSize = 3f;
			label.alignment = TextAlignmentOptions.Center;
			label.rectTransform.sizeDelta = new Vector2(10f, 2f);
		}
	}
}
