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
		private static readonly Color DoorLabelColor = new Color(0.35f, 0.95f, 0.35f);
		private static readonly Color PlatformLabelColor = new Color(1f, 0.9f, 0.3f);
		private static readonly Color HazardLabelColor = new Color(1f, 0.45f, 0.45f);
		private static readonly Color CheckpointLabelColor = new Color(0.75f, 0.7f, 1f);
		private static readonly Color ZoneLabelColor = new Color(0.35f, 0.9f, 1f);
		private static bool isRegistered;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void Register()
		{
#if !UNITY_EDITOR
			if (Debug.isDebugBuild == false)
				return;
#endif
			if (isRegistered)
				return;

			SceneManager.sceneLoaded += OnSceneLoaded;
			isRegistered = true;
		}

		private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			// 2026-06-20. 당장 안써서 리턴
			return;

			if (Application.isPlaying == false)
			{
				UGCLog.Info($"[Installer] Skip scene='{scene.name}' (not playing)");
				return;
			}

			if (IsGameplayScene(scene) == false)
			{
				UGCLog.Info($"[Installer] Skip scene='{scene.name}' path='{scene.path}' (not gameplay scene)");
				return;
			}

			UGCLog.Info($"[Installer] Install start scene='{scene.name}' mode={mode}");

			UGCObjectRegistry.Clear();
			UGCConditionRuntime.Clear();
			GameObject root = GameObject.Find(RootName);
			if (root == null)
			{
				root = new GameObject(RootName);
			}

			MarkRuntimeOnly(root);

			if (UGCJsonLoader.TryLoadMapManifestFromSample(DefaultManifestFile, out UGCMapManifestData manifest, out string manifestError) == false)
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

			UGCDevSampleRunner sampleRunner = Object.FindAnyObjectByType<UGCDevSampleRunner>();
			if (sampleRunner == null)
			{
				GameObject runner = new GameObject("UGC_DevRunner");
				runner.transform.SetParent(root.transform, true);
				sampleRunner = runner.AddComponent<UGCDevSampleRunner>();
				UGCLog.Info("[Installer] Created UGC_DevRunner");
			}
			else
			{
				UGCLog.Info("[Installer] Reused existing UGCDevSampleRunner");
			}
			MarkRuntimeOnly(sampleRunner.gameObject);
			sampleRunner.Setup(DefaultManifestFile, DefaultTriggerFile, false);

			UGCLog.Info($"Test setup auto-installed in scene '{scene.name}'. Hotkeys: F6(open gate), F7(move platform), F8(checkpoint), F9(toggle hazard)");
		}

		private static void InstallFromManifest(UGCMapManifestData manifest, Transform root)
		{
			for (int i = 0; i < manifest.objects.Count; i++)
			{
				UGCMapObjectData data = manifest.objects[i];
				GameObject obj = FindOrCreateMapObject(data, root);
				SyncMapObjectTransform(data, obj, root);
				AttachReceiverByTags(data, obj);
				AttachDebugLabelByTags(data, obj);
				AttachGroundSurfaceByTags(data, obj);
			}

			for (int i = 0; i < manifest.checkpoints.Count; i++)
			{
				UGCCheckpointData checkpoint = manifest.checkpoints[i];
				GameObject obj = GameObject.Find(checkpoint.id);
				if (obj == null)
				{
					obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
					obj.name = checkpoint.id;
				}

				obj.transform.position = ToVector3(checkpoint.position, Vector3.zero);
				obj.transform.localScale = Vector3.one * 1.5f;
				obj.transform.SetParent(root, true);
				MarkRuntimeOnly(obj);

				if (obj.GetComponent<UGCTestCheckpointReceiver>() == null)
					obj.AddComponent<UGCTestCheckpointReceiver>();

				EnsureDebugLabel(obj, $"Checkpoint\n{checkpoint.id}", CheckpointLabelColor, 1.8f);
				EnsureGroundSurface(obj);
			}
		}

		private static void CreateFallbackObjects(Transform root)
		{
			GameObject door = GetOrCreatePrimitive("door_gate_01", PrimitiveType.Cube, new Vector3(12f, 1f, 8f), new Vector3(2f, 2f, 0.5f), root);
			if (door.GetComponent<UGCTestDoorReceiver>() == null)
				door.AddComponent<UGCTestDoorReceiver>();
			EnsureDebugLabel(door, "Door\ndoor_gate_01", DoorLabelColor, 2.2f);

			GameObject platform = GetOrCreatePrimitive("platform_move_01", PrimitiveType.Cube, new Vector3(20f, 2f, 10f), new Vector3(3f, 0.4f, 3f), root);
			if (platform.GetComponent<UGCTestPlatformReceiver>() == null)
				platform.AddComponent<UGCTestPlatformReceiver>();
			EnsureDebugLabel(platform, "Platform\nplatform_move_01", PlatformLabelColor, 1.4f);
			EnsureGroundSurface(platform);

			GameObject checkpoint = GetOrCreatePrimitive("checkpoint_03", PrimitiveType.Sphere, new Vector3(35f, 1f, 12f), Vector3.one * 1.5f, root);
			if (checkpoint.GetComponent<UGCTestCheckpointReceiver>() == null)
				checkpoint.AddComponent<UGCTestCheckpointReceiver>();
			EnsureDebugLabel(checkpoint, "Checkpoint\ncheckpoint_03", CheckpointLabelColor, 1.8f);
			EnsureGroundSurface(checkpoint);

			GameObject hazard = GetOrCreatePrimitive("hazard_spikes_01", PrimitiveType.Cube, new Vector3(26f, 0.6f, 11f), new Vector3(2.4f, 1.2f, 2.4f), root);
			if (hazard.GetComponent<UGCTestHazardReceiver>() == null)
				hazard.AddComponent<UGCTestHazardReceiver>();
			EnsureDebugLabel(hazard, "Hazard\nhazard_spikes_01", HazardLabelColor, 1.8f);
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
					if (string.IsNullOrWhiteSpace(zoneId) || zoneIds.Add(zoneId) == false)
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
			}
			MarkRuntimeOnly(zone);

			// Always sync transform from data so stale scene objects do not keep old trigger positions.
			zone.transform.position = position;
			zone.transform.localScale = scale;

			Collider collider = zone.GetComponent<Collider>();
			if (collider == null)
				collider = zone.AddComponent<BoxCollider>();

			collider.isTrigger = true;

			Renderer renderer = zone.GetComponent<Renderer>();
			if (renderer != null)
				renderer.enabled = false;

			UGCTriggerZone triggerZone = zone.GetComponent<UGCTriggerZone>();
			if (triggerZone == null)
				triggerZone = zone.AddComponent<UGCTriggerZone>();

			triggerZone.Setup(zoneId);
			EnsureDebugLabel(zone, $"Zone\n{zoneId}", ZoneLabelColor, scale.y * 0.6f + 0.8f);

			GroundSurface groundSurface = zone.GetComponent<GroundSurface>();
			if (groundSurface == null)
				groundSurface = zone.AddComponent<GroundSurface>();
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
			MarkRuntimeOnly(obj);

			return obj;
		}

		private static void SyncMapObjectTransform(UGCMapObjectData data, GameObject obj, Transform root)
		{
			obj.transform.position = ToVector3(data.position, Vector3.zero);
			obj.transform.eulerAngles = ToVector3(data.rotation, Vector3.zero);
			obj.transform.localScale = ToVector3(data.scale, Vector3.one);
			obj.transform.SetParent(root, true);
			MarkRuntimeOnly(obj);
		}

		private static void AttachReceiverByTags(UGCMapObjectData data, GameObject obj)
		{
			if (data.tags != null && data.tags.Contains("door") && obj.GetComponent<UGCTestDoorReceiver>() == null)
				obj.AddComponent<UGCTestDoorReceiver>();

			if (data.tags != null && data.tags.Contains("moving") && obj.GetComponent<UGCTestPlatformReceiver>() == null)
				obj.AddComponent<UGCTestPlatformReceiver>();

			if (data.tags != null && data.tags.Contains("hazard") && obj.GetComponent<UGCTestHazardReceiver>() == null)
				obj.AddComponent<UGCTestHazardReceiver>();
		}

		private static void AttachDebugLabelByTags(UGCMapObjectData data, GameObject obj)
		{
			if (data == null || obj == null)
				return;

			if (data.tags != null && data.tags.Contains("door"))
			{
				EnsureDebugLabel(obj, $"Door\n{data.id}", DoorLabelColor, obj.transform.localScale.y * 0.7f + 0.9f);
				return;
			}

			if (data.tags != null && data.tags.Contains("moving"))
			{
				EnsureDebugLabel(obj, $"Platform\n{data.id}", PlatformLabelColor, obj.transform.localScale.y * 0.7f + 0.9f);
				return;
			}

			if (data.tags != null && data.tags.Contains("hazard"))
			{
				EnsureDebugLabel(obj, $"Hazard\n{data.id}", HazardLabelColor, obj.transform.localScale.y * 0.7f + 0.9f);
				return;
			}

			EnsureDebugLabel(obj, $"Object\n{data.id}", Color.white, obj.transform.localScale.y * 0.7f + 0.9f);
		}

		private static void AttachGroundSurfaceByTags(UGCMapObjectData data, GameObject obj)
		{
			if (data == null || obj == null || data.tags == null)
				return;

			if (data.tags.Contains("platform") || data.tags.Contains("moving"))
				EnsureGroundSurface(obj);
		}

		private static void EnsureDebugLabel(GameObject obj, string text, Color color, float yOffset)
		{
			if (obj == null)
				return;

			UGCDebugNameLabel label = obj.GetComponent<UGCDebugNameLabel>();
			if (label == null)
				label = obj.AddComponent<UGCDebugNameLabel>();

			label.Setup(text, color, yOffset);
		}

		private static void EnsureGroundSurface(GameObject obj)
		{
			if (obj == null)
				return;

			GroundSurface groundSurface = obj.GetComponent<GroundSurface>();
			if (groundSurface == null)
				obj.AddComponent<GroundSurface>();
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

			MarkRuntimeOnly(obj);

			return obj;
		}

		private static void MarkRuntimeOnly(GameObject obj)
		{
			if (obj == null)
				return;

			obj.hideFlags = HideFlags.DontSave;
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

			if (string.IsNullOrEmpty(scene.path) == false && scene.path.Contains("/Stage/"))
				return true;

			return false;
		}
	}
}
