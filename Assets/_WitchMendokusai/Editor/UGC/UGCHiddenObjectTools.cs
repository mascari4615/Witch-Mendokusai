using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WitchMendokusai
{
	public static class UGCHiddenObjectTools
	{
		private const string RootName = "UGC_TestSetup";

		[MenuItem("WM/UGC/Select Hidden Runtime Objects")]
		public static void SelectHiddenRuntimeObjects()
		{
			Object[] objects = FindHiddenRuntimeObjects().ToArray();
			Selection.objects = objects;
			LogObjects("Selected hidden runtime objects", objects);
		}

		[MenuItem("WM/UGC/Clear Hidden Runtime Objects")]
		public static void ClearHiddenRuntimeObjects()
		{
			List<GameObject> objects = FindHiddenRuntimeObjects();
			int removed = 0;

			for (int i = 0; i < objects.Count; i++)
			{
				GameObject obj = objects[i];
				if (obj == null)
					continue;

				Object.DestroyImmediate(obj);
				removed++;
			}

			Debug.Log($"[UGC][Editor] Cleared {removed} hidden runtime object(s).");
			EditorSceneManager.MarkAllScenesDirty();
		}

		[MenuItem("WM/UGC/Refresh Hidden Runtime Objects")]
		public static void RefreshHiddenRuntimeObjects()
		{
			List<GameObject> objects = FindHiddenRuntimeObjects();
			LogObjects("Hidden runtime objects", objects.Cast<Object>().ToArray());
		}

		[InitializeOnLoadMethod]
		private static void RegisterPlayModeCleanup()
		{
			EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
			EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
		}

		private static void OnPlayModeStateChanged(PlayModeStateChange state)
		{
			if (state != PlayModeStateChange.EnteredEditMode)
				return;

			List<GameObject> objects = FindHiddenRuntimeObjects();
			if (objects.Count == 0)
				return;

			for (int i = 0; i < objects.Count; i++)
			{
				GameObject obj = objects[i];
				if (obj == null)
					continue;

				Object.DestroyImmediate(obj);
			}

			Debug.Log($"[UGC][Editor] Auto-cleared {objects.Count} hidden runtime object(s) after exiting Play Mode.");
			EditorSceneManager.MarkAllScenesDirty();
		}

		private static List<GameObject> FindHiddenRuntimeObjects()
		{
			GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
			List<GameObject> results = new();

			for (int i = 0; i < allObjects.Length; i++)
			{
				GameObject obj = allObjects[i];
				if (obj == null)
					continue;

				if (EditorUtility.IsPersistent(obj))
					continue;

				if (IsHiddenRuntimeObject(obj) == false)
					continue;

				results.Add(obj);
			}

			return results;
		}

		private static bool IsHiddenRuntimeObject(GameObject obj)
		{
			if (obj.name == RootName || obj.name == "UGC_DevRunner")
				return true;

			if ((obj.hideFlags & HideFlags.DontSave) == 0 && (obj.hideFlags & HideFlags.HideInHierarchy) == 0)
				return false;

			Transform transform = obj.transform;
			while (transform != null)
			{
				if (transform.name == RootName)
					return true;

				transform = transform.parent;
			}

			return obj.name.StartsWith("zone_") || obj.name.StartsWith("checkpoint_") || obj.name.StartsWith("door_") || obj.name.StartsWith("platform_") || obj.name.StartsWith("hazard_");
		}

		private static void LogObjects(string title, Object[] objects)
		{
			if (objects == null || objects.Length == 0)
			{
				Debug.Log($"[UGC][Editor] {title}: none.");
				return;
			}

			string[] lines = new string[objects.Length];
			for (int i = 0; i < objects.Length; i++)
			{
				Object obj = objects[i];
				lines[i] = obj == null ? "<null>" : $"{obj.name} [{obj.hideFlags}]";
			}

			Debug.Log($"[UGC][Editor] {title}:\n- {string.Join("\n- ", lines)}");
		}
	}
}
