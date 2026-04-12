using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace KarmoLab.KarmoEditor.Settings
{
	[CustomEditor(typeof(KarmoEditorSettings))]
	public class KarmoEditorSettingsEditor : Editor
	{
		private ReorderableList _mutexNamesList;
		private ReorderableList _fieldsToResetList;

		private void OnEnable()
		{
			// Mutex Names List
			_mutexNamesList = new ReorderableList(serializedObject, serializedObject.FindProperty("ApplicationMutexNames"), true, true, true, true);
			_mutexNamesList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Mutex Names (App instance prevention)");
			_mutexNamesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				var element = _mutexNamesList.serializedProperty.GetArrayElementAtIndex(index);
				rect.y += 2;
				EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), element, GUIContent.none);
			};

			// Fields To Reset List
			_fieldsToResetList = new ReorderableList(serializedObject, serializedObject.FindProperty("ReflectionFieldResets"), true, true, true, true);
			_fieldsToResetList.drawHeaderCallback = (Rect rect) => EditorGUI.LabelField(rect, "Reset Fields (Reflection based cleanup)");
			_fieldsToResetList.elementHeight = EditorGUIUtility.singleLineHeight * 2 + 10;
			_fieldsToResetList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				var element = _fieldsToResetList.serializedProperty.GetArrayElementAtIndex(index);
				rect.y += 5;

				float halfWidth = rect.width;
				EditorGUI.LabelField(new Rect(rect.x, rect.y, 100, EditorGUIUtility.singleLineHeight), "Full Type:");
				EditorGUI.PropertyField(new Rect(rect.x + 100, rect.y, halfWidth - 100, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("FullTypeName"), GUIContent.none);

				rect.y += EditorGUIUtility.singleLineHeight + 2;
				EditorGUI.LabelField(new Rect(rect.x, rect.y, 100, EditorGUIUtility.singleLineHeight), "Field Name:");
				EditorGUI.PropertyField(new Rect(rect.x + 100, rect.y, halfWidth - 100, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("FieldName"), GUIContent.none);
			};
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.Space(5);
			_mutexNamesList.DoLayoutList();

			EditorGUILayout.Space(10);
			_fieldsToResetList.DoLayoutList();

			serializedObject.ApplyModifiedProperties();
		}
	}
}
