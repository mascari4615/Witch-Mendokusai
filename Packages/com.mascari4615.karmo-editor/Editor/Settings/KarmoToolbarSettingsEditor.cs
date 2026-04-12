using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace KarmoLab.KarmoEditor.Settings
{
	[CustomEditor(typeof(KarmoToolbarSettings))]
	public class KarmoToolbarSettingsEditor : Editor
	{
		private ReorderableList _favoriteScenesList;
		private ReorderableList _targetFoldersList;
		private ReorderableList _favoriteAssetsList;

		private void OnEnable()
		{
			// Scene Selector - Favorite Scenes
			_favoriteScenesList = new ReorderableList(
				serializedObject,
				serializedObject.FindProperty(nameof(KarmoToolbarSettings.FavoriteScenes)),
				draggable: true,
				displayHeader: true,
				displayAddButton: true,
				displayRemoveButton: true
			);
			_favoriteScenesList.drawHeaderCallback = rect =>
			{
				EditorGUI.LabelField(rect, "Favorite Scenes");
			};
			_favoriteScenesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				SerializedProperty element = _favoriteScenesList.serializedProperty.GetArrayElementAtIndex(index);
				rect.y += 2;
				EditorGUI.PropertyField(
					new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
					element,
					GUIContent.none
				);
			};

			// Scene Selector - Target Folders
			_targetFoldersList = new ReorderableList(
				serializedObject,
				serializedObject.FindProperty(nameof(KarmoToolbarSettings.TargetFolders)),
				draggable: true,
				displayHeader: true,
				displayAddButton: true,
				displayRemoveButton: true
			);
			_targetFoldersList.drawHeaderCallback = rect =>
			{
				EditorGUI.LabelField(rect, "Target Folders (Auto-include scenes)");
			};
			_targetFoldersList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				SerializedProperty element = _targetFoldersList.serializedProperty.GetArrayElementAtIndex(index);
				rect.y += 2;
				EditorGUI.PropertyField(
					new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
					element,
					GUIContent.none
				);
			};

			// Asset Selector - Favorite Assets
			_favoriteAssetsList = new ReorderableList(
				serializedObject,
				serializedObject.FindProperty(nameof(KarmoToolbarSettings.FavoriteAssets)),
				draggable: true,
				displayHeader: true,
				displayAddButton: true,
				displayRemoveButton: true
			);
			_favoriteAssetsList.drawHeaderCallback = rect =>
			{
				EditorGUI.LabelField(rect, "Favorite Assets");
			};
			_favoriteAssetsList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				SerializedProperty element = _favoriteAssetsList.serializedProperty.GetArrayElementAtIndex(index);
				rect.y += 2;
				EditorGUI.PropertyField(
					new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
					element,
					GUIContent.none
				);
			};
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			// Scene Selector 섹션
			EditorGUILayout.LabelField("Scene Selector", EditorStyles.boldLabel);
			EditorGUILayout.Space(5);
			_favoriteScenesList.DoLayoutList();

			EditorGUILayout.Space(5);
			_targetFoldersList.DoLayoutList();

			EditorGUILayout.Space(5);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.PropertyField(
				serializedObject.FindProperty(nameof(KarmoToolbarSettings.ShowOnlyBuildSettingsScenes))
			);
			EditorGUILayout.EndVertical();

			EditorGUILayout.Space(15);

			// Asset Selector 섹션
			EditorGUILayout.LabelField("Asset Selector", EditorStyles.boldLabel);
			EditorGUILayout.Space(5);
			_favoriteAssetsList.DoLayoutList();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
