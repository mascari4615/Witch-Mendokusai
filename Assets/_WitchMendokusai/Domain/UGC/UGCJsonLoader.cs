using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace WitchMendokusai
{
	public static class UGCJsonLoader
	{
		private static readonly JsonSerializerSettings JsonSettings = new()
		{
			TypeNameHandling = TypeNameHandling.None,
			MissingMemberHandling = MissingMemberHandling.Ignore,
			NullValueHandling = NullValueHandling.Include,
		};

		public static bool TryLoadTriggerEventsFromSample(string fileName, out List<UGCTriggerEventData> events, out string error)
		{
			string path = UGCPathResolver.GetSamplePath(fileName);
			if (TryReadJsonFile(path, out string json, out error) == false)
			{
				events = null;
				return false;
			}

			try
			{
				events = JsonConvert.DeserializeObject<List<UGCTriggerEventData>>(json, JsonSettings);
			}
			catch (Exception ex)
			{
				events = null;
				error = $"Failed to deserialize trigger events JSON: {ex.Message}";
				return false;
			}

			if (events == null)
			{
				error = "Trigger events JSON was empty.";
				return false;
			}

			for (int i = 0; i < events.Count; i++)
			{
				if (UGCJsonValidator.TryValidateTriggerEvent(events[i], out error) == false)
				{
					error = $"{Path.GetFileName(path)}[{i}] {error}";
					return false;
				}
			}

			error = null;
			return true;
		}

		public static bool TryLoadMapManifestFromSample(string fileName, out UGCMapManifestData manifest, out string error)
		{
			string path = UGCPathResolver.GetSamplePath(fileName);
			if (TryReadJsonFile(path, out string json, out error) == false)
			{
				manifest = null;
				return false;
			}

			try
			{
				manifest = JsonConvert.DeserializeObject<UGCMapManifestData>(json, JsonSettings);
			}
			catch (Exception ex)
			{
				manifest = null;
				error = $"Failed to deserialize map manifest JSON: {ex.Message}";
				return false;
			}

			if (UGCJsonValidator.TryValidateManifest(manifest, out error) == false)
			{
				error = $"{Path.GetFileName(path)} {error}";
				return false;
			}

			error = null;
			return true;
		}

		public static bool TryLoadSeedFromSample(string fileName, out SeedSaveData seedData, out string error)
		{
			string path = UGCPathResolver.GetSamplePath(fileName);
			return TryLoadSeedFromPath(path, out seedData, out error);
		}

		public static bool TryLoadSeedFromPath(string path, out SeedSaveData seedData, out string error)
		{
			seedData = default;
			error = null;

			if (string.IsNullOrWhiteSpace(path))
			{
				error = "Path is null or empty";
				return false;
			}

			if (File.Exists(path) == false)
			{
				error = $"File not found: {path}";
				return false;
			}

			try
			{
				string jsonContent = File.ReadAllText(path);
				UGCSeedManifestData manifest = JsonConvert.DeserializeObject<UGCSeedManifestData>(jsonContent, JsonSettings);

				if (UGCJsonValidator.TryValidateSeed(manifest, out string validationError) == false)
				{
					error = $"Validation failed: {validationError}";
					return false;
				}

				seedData = manifest.seedData;
				return true;
			}
			catch (Exception ex)
			{
				error = $"Failed to load JSON: {ex.Message}";
				return false;
			}
		}

		public static bool TrySaveSeedToPath(string path, SeedSaveData seedData, int seedId, string author, out string error)
		{
			error = null;

			if (string.IsNullOrWhiteSpace(path))
			{
				error = "Path is null or empty";
				return false;
			}

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");

				UGCSeedManifestData manifest = new UGCSeedManifestData
				{
					// 우리가 쓰는 저장 스키마 = 검증기가 아는 그 버전. 리터럴로 박으면 스키마를 올릴 때
					// 여기만 옛 버전으로 남아 우리가 쓴 파일을 우리 검증기가 거부한다.
					schemaVersion = UGCJsonValidator.CURRENT_SCHEMA_VERSION,
					seedId = seedId,
					version = 1,
					author = author ?? "unknown",
					seedData = seedData,
					tags = new(),
					meta = null
				};

				if (UGCJsonValidator.TryValidateSeed(manifest, out string validationError) == false)
				{
					error = $"Validation failed: {validationError}";
					return false;
				}

				string jsonContent = JsonConvert.SerializeObject(manifest, JsonSettings);
				File.WriteAllText(path, jsonContent);
				return true;
			}
			catch (Exception ex)
			{
				error = $"Failed to save JSON: {ex.Message}";
				return false;
			}
		}

		private static bool TryReadJsonFile(string path, out string json, out string error)
		{
			if (File.Exists(path) == false)
			{
				json = null;
				error = $"File not found: {path}";
				return false;
			}

			try
			{
				json = File.ReadAllText(path);
			}
			catch (Exception ex)
			{
				json = null;
				error = $"Failed to read file '{path}': {ex.Message}";
				return false;
			}

			error = null;
			return true;
		}
	}
}
