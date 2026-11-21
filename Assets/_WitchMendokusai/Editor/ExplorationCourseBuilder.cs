using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	public static class ExplorationCourseBuilder
	{
		public const string STAGE_PATH = "Assets/_WitchMendokusai/Domain/World/Stage/1002_Exploration/WS_1002_Exploration.asset";
		private const string PREFAB_PATH = "Assets/_WitchMendokusai/Domain/World/Stage/1002_Exploration/Stage_Exploration.prefab";
		private const string DEV_PATH = "Assets/_WitchMendokusai/Core/Assets/Singletons/DevWindowController.prefab";

		public static void Build(Material ground, Material climb, Material goal)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(STAGE_PATH));
			GameObject root = new("Stage_Exploration");
			root.SetActive(false);
			try
			{
				root.AddComponent<StageObject>();
				TraversalCourse course = root.AddComponent<TraversalCourse>();
				TraversalCameraDriver driver = root.AddComponent<TraversalCameraDriver>();
				GameObject start = Box(root, "Courtyard", new Vector3(0f, -0.5f, 22f), new Vector3(12f, 1f, 56f), ground);
				GameObject east = Box(root, "East gallery", new Vector3(45f, -0.5f, 50f), new Vector3(102f, 1f, 12f), ground);
				GameObject north = Box(root, "North gallery", new Vector3(90f, -0.5f, 75f), new Vector3(12f, 1f, 62f), ground);
				GameObject west = Box(root, "Return gallery", new Vector3(45f, -0.5f, 100f), new Vector3(102f, 1f, 12f), ground);
				GameObject approach = Box(root, "Cliff approach", new Vector3(0f, -0.5f, 120f), new Vector3(12f, 1f, 48f), ground);
				Box(root, "Climb face", new Vector3(0f, 3f, 145f), new Vector3(10f, 6f, 2f), climb).AddComponent<ClimbableSurface>();
				GameObject takeoff = Box(root, "Takeoff", new Vector3(0f, 5.5f, 151f), new Vector3(10f, 1f, 10f), ground);
				Box(root, "Arrival island", new Vector3(0f, -0.5f, 186f), new Vector3(16f, 1f, 36f), ground);
				GameObject finish = Box(root, "Goal pad", new Vector3(0f, 0.025f, 197f), new Vector3(8f, 0.05f, 8f), goal);
				Box(root, "Goal beacon", new Vector3(0f, 6f, 203f), new Vector3(1f, 12f, 1f), goal);
				// 시야 시험: 좁은 벽길, 꺾이는 통로, 기둥 스침. 진행 방향은 열린 상태
				Box(root, "West corridor wall", new Vector3(-3.5f, 2f, 20f), new Vector3(1f, 4f, 22f), ground);
				Box(root, "East corridor wall", new Vector3(3.5f, 2f, 20f), new Vector3(1f, 4f, 22f), ground);
				for (int index = 0; index < 6; index++)
				{
					float positionX = 15f + index * 12f;
					Box(root, "Gallery pillar " + index, new Vector3(positionX, 2f, 54f), new Vector3(1f, 4f, 1f), climb).AddComponent<CameraFadeObstacle>();
				}
				Box(root, "Turn wall", new Vector3(95f, 2f, 84f), new Vector3(1f, 4f, 25f), ground);
				Vector3[] positions =
				{
					Vector3.zero, new(4f, 0.05f, 50f), new(90f, 0.05f, 56f),
					new(84f, 0.05f, 100f), new(0f, 0.05f, 107f), new(0f, 6.05f, 151f), new(0f, 0.1f, 197f)
				};
				GameObject[] platforms = { start, east, north, west, approach, takeoff, finish };
				string[] instructions =
				{
					"벽길을 지나 청록 표식까지 전진. 오른쪽으로 꺾이는 긴 통로 찾기.",
					"동쪽 긴 통로 끝까지 이동. 기둥 옆에서 마우스로 시야 조절.",
					"왼쪽으로 돌아 북쪽 통로 끝까지. 금빛 기둥이 최종 목표.",
					"다시 왼쪽, 서쪽 통로 끝의 청록 절벽 찾기.",
					"오른쪽 청록 절벽으로 이동. 벽을 향해 전진하면 등반, 꼭대기까지 밀기.",
					"발판 밖으로 걸은 뒤 Space 한 번으로 활공. 건너편 섬에 착지 후 금빛 기둥까지.",
					"목표 도착. Tab으로 커서를 풀고 이전 Stage로 귀환. 이동과 시점 설정 자동 복원."
				};
				SerializedObject data = new(course);
				SerializedProperty anchors = data.FindProperty("checkpoints");
				SerializedProperty colliders = data.FindProperty("checkpointPlatforms");
				SerializedProperty messages = data.FindProperty("routeInstructions");
				anchors.arraySize = colliders.arraySize = messages.arraySize = positions.Length;
				for (int index = 0; index < positions.Length; index++)
				{
					anchors.GetArrayElementAtIndex(index).objectReferenceValue = TraversalCourseBuilder.Anchor(root, "Checkpoint " + index, positions[index]);
					colliders.GetArrayElementAtIndex(index).objectReferenceValue = platforms[index].GetComponent<Collider>();
					messages.GetArrayElementAtIndex(index).stringValue = instructions[index];
					GameObject marker = Box(root, "Route marker " + index, positions[index] + new Vector3(-2f, 0.025f, 0f), new Vector3(1f, 0.01f, 1f), climb);
					Object.DestroyImmediate(marker.GetComponent<Collider>());
				}
				data.FindProperty("courseName").stringValue = "금빛 기둥 탐험";
				data.FindProperty("cameraDriver").objectReferenceValue = driver;
				data.FindProperty("hudStyle").objectReferenceValue = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/_WitchMendokusai/Domain/World/Stage/TraversalCourse.uss");
				data.ApplyModifiedPropertiesWithoutUndo();
				SerializedObject profiles = new(driver);
				SetProfile(profiles.FindProperty("ground"), 7f, 15f);
				SetProfile(profiles.FindProperty("climb"), 6f, 0f);
				SetProfile(profiles.FindProperty("glide"), 9f, 20f);
				profiles.ApplyModifiedPropertiesWithoutUndo();
				TraversalCourseBuilder.Label(root, "FOLLOW THE TEAL MARKERS", new Vector3(0f, 3f, 6f));
				TraversalCourseBuilder.Label(root, "CLIMB", new Vector3(0f, 4f, 143.9f));
				TraversalCourseBuilder.Label(root, "WALK OFF THEN SPACE", new Vector3(0f, 8f, 155f));
				TraversalCourseBuilder.Label(root, "GOAL", new Vector3(0f, 3f, 202f));
				root.SetActive(true);
				GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
				WorldStage stage = AssetDatabase.LoadAssetAtPath<WorldStage>(STAGE_PATH);
				if (stage == null)
				{
					stage = ScriptableObject.CreateInstance<WorldStage>();
					AssetDatabase.CreateAsset(stage, STAGE_PATH);
				}
				stage.ID = 1002;
				stage.Name = "금빛 기둥 탐험";
				stage.Description = "약 3분 목표의 벽길, 회랑, 등반과 활공 탐험";
				SerializedObject stageData = new(stage);
				stageData.FindProperty("<DisplayName>k__BackingField").stringValue = stage.Name;
				stageData.FindProperty("<Type>k__BackingField").enumValueIndex = 0;
				stageData.FindProperty("<ShareWorldPosition>k__BackingField").boolValue = false;
				stageData.FindProperty("<Prefab>k__BackingField").objectReferenceValue = prefab.GetComponent<StageObject>();
				stageData.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(stage);
				GameObject dev = PrefabUtility.LoadPrefabContents(DEV_PATH);
				try
				{
					SerializedObject devData = new(dev.GetComponent<DevWindowController>());
					devData.FindProperty("explorationStage").objectReferenceValue = stage;
					devData.ApplyModifiedPropertiesWithoutUndo();
					PrefabUtility.SaveAsPrefabAsset(dev, DEV_PATH);
				}
				finally
				{
					PrefabUtility.UnloadPrefabContents(dev);
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static void SetProfile(SerializedProperty profile, float distance, float pitch)
		{
			profile.FindPropertyRelative("<Distance>k__BackingField").floatValue = distance;
			profile.FindPropertyRelative("<Pitch>k__BackingField").floatValue = pitch;
		}

		private static GameObject Box(GameObject root, string name, Vector3 position, Vector3 size, Material material)
			=> TraversalCourseBuilder.Box(root, name, position, size, material);
	}
}
