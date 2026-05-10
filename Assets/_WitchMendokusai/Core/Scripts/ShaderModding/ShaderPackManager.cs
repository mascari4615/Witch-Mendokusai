using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchMendokusai
{
	public class ShaderPackManager : MonoBehaviour
	{
		public static ShaderPackManager Instance { get; private set; }

		public static bool TryGetExistingInstance(out ShaderPackManager mgr)
		{
			mgr = Instance;
			return mgr != null;
		}

		public const string SHADERPACKS_FOLDER_NAME = "shaderpacks";
		public const string MANIFEST_FILE_NAME = "manifest.json";
		public const string PREF_KEY_ACTIVE_PACK = "shadermod.active_pack_id";

		private readonly Dictionary<string, IShaderPackSlot> registeredSlots = new();
		private readonly List<ShaderPackEntry> availablePacks = new();
		private ShaderPackEntry activePack;
		private AssetBundle activeBundle;

		public IReadOnlyList<ShaderPackEntry> AvailablePacks => availablePacks;
		public ShaderPackEntry ActivePack => activePack;

		public string ShaderPacksDirectory => Path.Combine(Application.persistentDataPath, SHADERPACKS_FOLDER_NAME);

		private void Awake()
		{
			if (Instance != null && Instance != this) { Destroy(gameObject); return; }
			Instance = this;
			RegisterSlots();
			ScanShaderPacks();
			RestoreActivePack();
		}

		private void RegisterSlots()
		{
			RegisterSlot(new PostProcessSlot());
			RegisterSlot(new SkyboxSlot());
			RegisterSlot(new WaterSlot());
		}

		private void RegisterSlot(IShaderPackSlot slot)
		{
			registeredSlots[slot.SlotId] = slot;
		}

		public void ScanShaderPacks()
		{
			availablePacks.Clear();

			string root = ShaderPacksDirectory;
			if (Directory.Exists(root) == false)
			{
				Directory.CreateDirectory(root);
				Debug.Log($"[ShaderPackManager] Created shaderpacks folder: {root}");
				return;
			}

			foreach (string packDirectory in Directory.GetDirectories(root))
			{
				string manifestPath = Path.Combine(packDirectory, MANIFEST_FILE_NAME);
				if (File.Exists(manifestPath) == false)
				{
					Debug.LogWarning($"[ShaderPackManager] No {MANIFEST_FILE_NAME} in {packDirectory}, skipping.");
					continue;
				}

				string manifestText = File.ReadAllText(manifestPath);
				ShaderPackManifest manifest = JsonUtility.FromJson<ShaderPackManifest>(manifestText);
				if (manifest == null)
				{
					Debug.LogWarning($"[ShaderPackManager] Failed to parse {manifestPath}, skipping.");
					continue;
				}

				ShaderPackEntry entry = new ShaderPackEntry
				{
					Id = Path.GetFileName(packDirectory),
					Path = packDirectory,
					Manifest = manifest
				};
				availablePacks.Add(entry);
			}

			Debug.Log($"[ShaderPackManager] Scanned {availablePacks.Count} shader packs from {root}");
		}

		public void Apply(string packId)
		{
			if (activePack != null && activePack.Id == packId)
			{
				Debug.Log($"[ShaderPackManager] Pack '{packId}' already active, skipping Apply (idempotent).");
				return;
			}

			ShaderPackEntry target = availablePacks.Find(entry => entry.Id == packId);
			if (target == null)
			{
				Debug.LogWarning($"[ShaderPackManager] Pack '{packId}' not found.");
				return;
			}

			Revert();

			string bundleFile = target.Manifest.bundleFile;
			if (string.IsNullOrEmpty(bundleFile))
			{
				Debug.LogError($"[ShaderPackManager] Pack '{packId}' has no bundleFile in manifest.");
				return;
			}

			string bundlePath = Path.Combine(target.Path, bundleFile);
			activeBundle = AssetBundle.LoadFromFile(bundlePath);
			if (activeBundle == null)
			{
				Debug.LogError($"[ShaderPackManager] Failed to load AssetBundle: {bundlePath}");
				return;
			}

			foreach (ShaderPackSlotInfo slotInfo in target.Manifest.slots)
			{
				if (registeredSlots.TryGetValue(slotInfo.id, out IShaderPackSlot slot) == false)
				{
					Debug.LogWarning($"[ShaderPackManager] No slot registered for id '{slotInfo.id}' (manifest of '{packId}'). Skipping.");
					continue;
				}
				slot.Apply(activeBundle, slotInfo);
			}

			activePack = target;
			PlayerPrefs.SetString(PREF_KEY_ACTIVE_PACK, packId);
			PlayerPrefs.Save();
			Debug.Log($"[ShaderPackManager] Applied pack: {target.Manifest.name} ({packId})");
		}

		public void Revert()
		{
			if (activePack == null)
				return;

			foreach (ShaderPackSlotInfo slotInfo in activePack.Manifest.slots)
			{
				if (registeredSlots.TryGetValue(slotInfo.id, out IShaderPackSlot slot))
					slot.Revert();
			}

			if (activeBundle != null)
			{
				activeBundle.Unload(true);
				activeBundle = null;
			}

			Debug.Log($"[ShaderPackManager] Reverted pack: {activePack.Manifest.name}");
			activePack = null;
			PlayerPrefs.DeleteKey(PREF_KEY_ACTIVE_PACK);
			PlayerPrefs.Save();
		}

		// 씬 전환 시 ShaderPackManager destroy → activeBundle 메모리 잔존 → 다음 인스턴스 Awake 의
		// AssetBundle.LoadFromFile 가 "another AssetBundle with the same files is already loaded" fail.
		// destroy 시점에 명시적 unload 로 file handle 정리.
		private void OnDestroy()
		{
			if (activeBundle != null)
			{
				activeBundle.Unload(true);
				activeBundle = null;
			}

			if (Instance == this)
				Instance = null;
		}

		private void RestoreActivePack()
		{
			if (PlayerPrefs.HasKey(PREF_KEY_ACTIVE_PACK) == false)
				return;

			string savedId = PlayerPrefs.GetString(PREF_KEY_ACTIVE_PACK);
			if (string.IsNullOrEmpty(savedId))
				return;

			Apply(savedId);

			if (activePack == null)
			{
				Debug.LogWarning($"[ShaderPackManager] Saved active pack '{savedId}' not found, clearing preference.");
				PlayerPrefs.DeleteKey(PREF_KEY_ACTIVE_PACK);
				PlayerPrefs.Save();
			}
		}
	}

	public class ShaderPackEntry
	{
		public string Id;
		public string Path;
		public ShaderPackManifest Manifest;
	}
}
