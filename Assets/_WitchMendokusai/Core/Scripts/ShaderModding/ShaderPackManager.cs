using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace WitchMendokusai
{
	public class ShaderPackManager : Singleton<ShaderPackManager>
	{
		public const string SHADERPACKS_FOLDER_NAME = "shaderpacks";
		public const string MANIFEST_FILE_NAME = "manifest.json";

		private readonly Dictionary<string, IShaderPackSlot> registeredSlots = new();
		private readonly List<ShaderPackEntry> availablePacks = new();
		private ShaderPackEntry activePack;
		private AssetBundle activeBundle;

		public IReadOnlyList<ShaderPackEntry> AvailablePacks => availablePacks;
		public ShaderPackEntry ActivePack => activePack;

		public string ShaderPacksDirectory => Path.Combine(Application.persistentDataPath, SHADERPACKS_FOLDER_NAME);

		protected override void Awake()
		{
			base.Awake();
			RegisterSlots();
			ScanShaderPacks();
		}

		private void RegisterSlots()
		{
			RegisterSlot(new PostProcessSlot());
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
		}
	}

	public class ShaderPackEntry
	{
		public string Id;
		public string Path;
		public ShaderPackManifest Manifest;
	}
}
