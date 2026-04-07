using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public static class UGCDevAutoSceneInstaller
	{
		private const string RootName = "UGC_TestSetup";
		private const string DefaultManifestFile = "wm_jump_001.manifest.json";
		private const string DefaultTriggerFile = "wm_jump_001.triggers.json";
		private static bool isRegistered;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Register()
		{
#if !UNITY_EDITOR
			if (!Debug.isDebugBuild)
				return;
#endif
			if (isRegistered)
				return;

			SceneManager.sceneLoaded += OnSceneLoaded;
			isRegistered = true;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (!IsGameplayScene(scene))
				return;

			UGCObjectRegistry.Clear();
			UGCConditionRuntime.Clear();

			if (GameObject.Find(RootName) != null)
				return;

			if (Object.FindAnyObjectByType<UGCDevSampleRunner>() != null)
				return;

			GameObject root = new(RootName);
			root.hideFlags = HideFlags.DontSave;

			if (!UGCJsonLoader.TryLoadMapManifestFromSample(DefaultManifestFile, out UGCMapManifestData manifest, out string manifestError))
			{
				UGCLog.Warn($"Manifest load failed, fallback setup will be used: {manifestError}");
				CreateFallbackObjects(root.transform);
			}
			else
			{
				InstallFromManifest(manifest, root.transform);
				InstallZonesFromManifest(manifest, root.transform);
			}

			if (manifest == null || manifest.zones == null || manifest.zones.Count == 0)
			{
				if (UGCJsonLoader.TryLoadTriggerEventsFromSample(DefaultTriggerFile, out List<UGCTriggerEventData> triggerEvents, out string triggerError))
					InstallTriggerZonesFromEvents(triggerEvents, root.transform);
				else
					UGCLog.Warn($"Trigger file load failed while creating trigger zones: {triggerError}");
			}

			GameObject runner = new("UGC_DevRunner");
			runner.transform.SetParent(root.transform, true);
			UGCDevSampleRunner sampleRunner = runner.AddComponent<UGCDevSampleRunner>();
			sampleRunner.Setup(DefaultManifestFile, DefaultTriggerFile, false);

			UGCLog.Info($"Test setup auto-installed in scene '{scene.name}'. Hotkeys: F6(open gate), F7(move platform), F8(checkpoint)");
		}

		private static void InstallFromManifest(UGCMapManifestData manifest, Transform root)
		{
			for (int i = 0; i < manifest.objects.Count; i++)
			{
				UGCMapObjectData data = manifest.objects[i];
				GameObject obj = FindOrCreateMapObject(data, root);
				AttachReceiverByTags(data, obj);
			}

			for (int i = 0; i < manifest.checkpoints.Count; i++)
			{
				UGCCheckpointData checkpoint = manifest.checkpoints[i];
				GameObject obj = GameObject.Find(checkpoint.id);
				if (obj == null)
				{
					obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
					obj.name = checkpoint.id;
					obj.transform.position = ToVector3(checkpoint.position, Vector3.zero);
					obj.transform.localScale = Vector3.one * 1.5f;
					obj.transform.SetParent(root, true);
				}

				if (obj.GetComponent<UGCTestCheckpointReceiver>() == null)
					obj.AddComponent<UGCTestCheckpointReceiver>();
			}
		}

		private static void CreateFallbackObjects(Transform root)
		{
			GameObject door = GetOrCreatePrimitive("door_gate_01", PrimitiveType.Cube, new Vector3(12f, 1f, 8f), new Vector3(2f, 2f, 0.5f), root);
			if (door.GetComponent<UGCTestDoorReceiver>() == null)
				door.AddComponent<UGCTestDoorReceiver>();

			GameObject platform = GetOrCreatePrimitive("platform_move_01", PrimitiveType.Cube, new Vector3(20f, 2f, 10f), new Vector3(3f, 0.4f, 3f), root);
			if (platform.GetComponent<UGCTestPlatformReceiver>() == null)
				platform.AddComponent<UGCTestPlatformReceiver>();

			GameObject checkpoint = GetOrCreatePrimitive("checkpoint_03", PrimitiveType.Sphere, new Vector3(35f, 1f, 12f), Vector3.one * 1.5f, root);
			if (checkpoint.GetComponent<UGCTestCheckpointReceiver>() == null)
				checkpoint.AddComponent<UGCTestCheckpointReceiver>();
		}

		private static void InstallZonesFromManifest(UGCMapManifestData manifest, Transform root)
		{
			if (manifest?.zones == null)
				return;

			for (int i = 0; i < manifest.zones.Count; i++)
			{
				UGCZoneData zone = manifest.zones[i];
				EnsureZone(zone.id, root, ToVector3(zone.position, new Vector3(0f, 1f, 0f)), ToVector3(zone.scale, new Vector3(4f, 2f, 4f)));
			}
		}

		private static void InstallTriggerZonesFromEvents(List<UGCTriggerEventData> triggerEvents, Transform root)
		{
			HashSet<string> zoneIds = new();
			for (int i = 0; i < triggerEvents.Count; i++)
			{
				UGCTriggerEventData evt = triggerEvents[i];
				for (int c = 0; c < evt.conditions.Count; c++)
				{
					UGCConditionData condition = evt.conditions[c];
					if (condition?.type != "OnEnterZone")
						continue;

					string zoneId = condition.target?.id;
					if (string.IsNullOrWhiteSpace(zoneId) || !zoneIds.Add(zoneId))
						continue;

					EnsureZone(zoneId, root, new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 4f));
				}
			}
		}

		private static void EnsureZone(string zoneId, Transform root, Vector3 position, Vector3 scale)
		{
			GameObject zone = GameObject.Find(zoneId);
			if (zone == null)
			{
				zone = GameObject.CreatePrimitive(PrimitiveType.Cube);
				zone.name = zoneId;
				zone.transform.SetParent(root, true);
				zone.transform.position = position;
				zone.transform.localScale = scale;
			}

			Collider collider = zone.GetComponent<Collider>();
			if (collider == null)
				collider = zone.AddComponent<BoxCollider>();

			collider.isTrigger = true;

			UGCTriggerZone triggerZone = zone.GetComponent<UGCTriggerZone>();
			if (triggerZone == null)
				triggerZone = zone.AddComponent<UGCTriggerZone>();

			triggerZone.Setup(zoneId);
		}

		private static GameObject FindOrCreateMapObject(UGCMapObjectData data, Transform root)
		{
			GameObject obj = GameObject.Find(data.id);
			if (obj != null)
				return obj;

			PrimitiveType primitive = data.tags != null && data.tags.Contains("door") ? PrimitiveType.Cube : PrimitiveType.Cube;
			obj = GameObject.CreatePrimitive(primitive);
			obj.name = data.id;
			obj.transform.position = ToVector3(data.position, Vector3.zero);
			obj.transform.eulerAngles = ToVector3(data.rotation, Vector3.zero);
			obj.transform.localScale = ToVector3(data.scale, Vector3.one);
			obj.transform.SetParent(root, true);

			return obj;
		}

		private static void AttachReceiverByTags(UGCMapObjectData data, GameObject obj)
		{
			if (data.tags != null && data.tags.Contains("door") && obj.GetComponent<UGCTestDoorReceiver>() == null)
				obj.AddComponent<UGCTestDoorReceiver>();

			if (data.tags != null && data.tags.Contains("moving") && obj.GetComponent<UGCTestPlatformReceiver>() == null)
				obj.AddComponent<UGCTestPlatformReceiver>();
		}

		private static GameObject GetOrCreatePrimitive(string objectName, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Transform parent)
		{
			GameObject obj = GameObject.Find(objectName);
			if (obj == null)
			{
				obj = GameObject.CreatePrimitive(primitiveType);
				obj.name = objectName;
				obj.transform.position = position;
				obj.transform.localScale = scale;
			}

			if (obj.transform.parent == null)
				obj.transform.SetParent(parent, true);

			return obj;
		}

		private static Vector3 ToVector3(UGCVector3Data value, Vector3 defaultValue)
		{
			if (value == null)
				return defaultValue;

			return new Vector3(value.x, value.y, value.z);
		}

		private static bool IsGameplayScene(Scene scene)
		{
			if (scene.name == "World")
				return true;

			if (scene.name.StartsWith("Stage_"))
				return true;

			if (!string.IsNullOrEmpty(scene.path) && scene.path.Contains("/Stage/"))
				return true;

			return false;
		}
	}
}
