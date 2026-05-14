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
