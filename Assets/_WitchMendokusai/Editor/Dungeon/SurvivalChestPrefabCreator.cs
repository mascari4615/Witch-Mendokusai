using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 회복 아이템 프리팹을 복사해 상자 프리팹 생성, SurvivalRunRules에 연결
	public static class SurvivalChestPrefabCreator
	{
		private const string HEAL_PREFAB_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Loot/GameItem/HealObject.prefab";
		private const string CHEST_PREFAB_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Loot/GameItem/ChestObject.prefab";
		private const string RULES_PATH = "Assets/_WitchMendokusai/Domain/Dungeon/Survival/SurvivalRunRules.asset";
		private const string MOVE_SPEED_FIELD = "moveSpeed";
		private const string PICKUP_DISTANCE_FIELD = "pickupDistance";
		private const string CHEST_PREFAB_FIELD = nameof(SurvivalRunRules.ChestPrefab);

		[MenuItem("WM/Survival/Create Chest Prefab")]
		public static void Create()
		{
			if (AssetDatabase.LoadAssetAtPath<GameObject>(CHEST_PREFAB_PATH) == null && AssetDatabase.CopyAsset(HEAL_PREFAB_PATH, CHEST_PREFAB_PATH) == false)
			{
				Debug.LogError($"프리팹 복사 실패: {HEAL_PREFAB_PATH}");
				return;
			}

			GameObject root = PrefabUtility.LoadPrefabContents(CHEST_PREFAB_PATH);
			HealObject healObject = root.GetComponent<HealObject>();
			if (healObject != null)
			{
				SerializedObject healSerialized = new(healObject);
				float moveSpeed = healSerialized.FindProperty(MOVE_SPEED_FIELD).floatValue;
				float pickupDistance = healSerialized.FindProperty(PICKUP_DISTANCE_FIELD).floatValue;
				Object.DestroyImmediate(healObject);

				ChestObject chestObject = root.AddComponent<ChestObject>();
				SerializedObject chestSerialized = new(chestObject);
				chestSerialized.FindProperty(MOVE_SPEED_FIELD).floatValue = moveSpeed;
				chestSerialized.FindProperty(PICKUP_DISTANCE_FIELD).floatValue = pickupDistance;
				chestSerialized.ApplyModifiedPropertiesWithoutUndo();
			}
			root.name = nameof(ChestObject);
			PrefabUtility.SaveAsPrefabAsset(root, CHEST_PREFAB_PATH);
			PrefabUtility.UnloadPrefabContents(root);

			SurvivalRunRules rules = AssetDatabase.LoadAssetAtPath<SurvivalRunRules>(RULES_PATH);
			SerializedObject rulesSerialized = new(rules);
			rulesSerialized.FindProperty(CHEST_PREFAB_FIELD).objectReferenceValue = AssetDatabase.LoadAssetAtPath<ChestObject>(CHEST_PREFAB_PATH);
			rulesSerialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(rules);
			AssetDatabase.SaveAssets();
			Debug.Log($"상자 프리팹 생성과 연결 완료: {CHEST_PREFAB_PATH}");
		}
	}
}
