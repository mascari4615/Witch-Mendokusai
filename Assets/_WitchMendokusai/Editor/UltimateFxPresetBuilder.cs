using System;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 연출 프리셋 에셋을 만들거나 연결을 갱신. 연출마다 클래스 하나에 에셋 하나
	/// - UltimateFx 파생 클래스를 전부 찾아 CreateAssetMenu 의 파일 이름으로 에셋을 만듦. 새 연출은 클래스만 추가
	/// - 새로 만들 때만 클래스 기본값이 들어감. 이미 있는 에셋은 셰이더와 임시 풀 연결만 다시 넣고 값은 그대로 둠
	/// 메뉴: WM/CombatFx/Build Ultimate Presets. 개발 창 명령 fx ultimate [이름 일부] 로 재생
	/// </summary>
	public static class UltimateFxPresetBuilder
	{
		private const string PRESET_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Presets";

		[MenuItem("WM/CombatFx/Build Ultimate Presets")]
		public static void Build()
		{
			Shader sprite = Shader.Find("WM/Fx/UltimateSprite");
			Shader shockwave = Shader.Find("WM/Fx/UltimateShockwave");
			Shader grade = Shader.Find("WM/Fx/UltimateGrade");
			if (sprite == null || shockwave == null || grade == null)
				throw new InvalidOperationException("셰이더 WM/Fx/UltimateSprite, UltimateShockwave, UltimateGrade 를 찾지 못함 (임포트 전)");
			GameObject grass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_WitchMendokusai/Domain/World/Parts/Grass/Grass.prefab");
			GameObject shrub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_WitchMendokusai/Domain/World/Parts/Grass/Shrub.prefab");
			GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_WitchMendokusai/Domain/World/Parts/Tree/Tree.prefab");

			int count = 0;
			foreach (Type kind in TypeCache.GetTypesDerivedFrom<UltimateFx>())
			{
				if (kind.IsAbstract)
					continue;
				CreateAssetMenuAttribute menu = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(kind, typeof(CreateAssetMenuAttribute));
				if (menu == null)
					throw new InvalidOperationException($"{kind.Name} 에 CreateAssetMenu (fileName) 가 없음");
				string assetName = menu.fileName;
				count++;
				string path = PRESET_FOLDER + "/" + assetName + ".asset";
				UltimateFx fx = AssetDatabase.LoadAssetAtPath<UltimateFx>(path);
				bool created = fx == null;
				if (created)
				{
					fx = (UltimateFx)ScriptableObject.CreateInstance(kind);
					AssetDatabase.CreateAsset(fx, path);
				}

				SerializedObject serialized = new(fx);
				serialized.FindProperty("spriteShader").objectReferenceValue = sprite;
				serialized.FindProperty("shockwaveShader").objectReferenceValue = shockwave;
				serialized.FindProperty("gradeShader").objectReferenceValue = grade;
				if (created)
				{
					SerializedProperty foliage = serialized.FindProperty("foliage");
					foliage.arraySize = 3;
					SetFoliage(foliage.GetArrayElementAtIndex(0), grass, 220, 1f, new Vector2(1.2f, 2f), 1.2f);
					SetFoliage(foliage.GetArrayElementAtIndex(1), shrub, 18, 0.75f, new Vector2(0.9f, 1.4f), 3f);
					SetFoliage(foliage.GetArrayElementAtIndex(2), tree, 6, 0.3f, new Vector2(0.45f, 0.7f), 6.5f);
				}
				serialized.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(fx);
			}
			AssetDatabase.SaveAssets();
			Debug.Log($"[UltimateFx] 프리셋 {count}개 갱신");
		}

		private static void SetFoliage(SerializedProperty element, GameObject prefab, int count, float flexibility, Vector2 scaleRange, float minDistance)
		{
			element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
			element.FindPropertyRelative("Count").intValue = count;
			element.FindPropertyRelative("Flexibility").floatValue = flexibility;
			element.FindPropertyRelative("ScaleRange").vector2Value = scaleRange;
			element.FindPropertyRelative("MinDistance").floatValue = minDistance;
		}
	}
}
