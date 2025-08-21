using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace KarmoLabs
{
	public class SaveTool
	{
		private static readonly JsonSerializerSettings JsonSettings = new() { TypeNameHandling = TypeNameHandling.Auto, };
		private static string GetSaveFilePath(string fileName) => Path.Combine(Application.dataPath, fileName);
		private static bool IsSaveFileExists(string fileName) => File.Exists(GetSaveFilePath(fileName));

		public static void SaveFile<T>(string fileName, T data)
		{
			string json = JsonConvert.SerializeObject(data, Formatting.Indented, JsonSettings);
			File.WriteAllText(GetSaveFilePath(fileName), json);
		}

		public static bool TryLoadFile<T>(string fileName, out T data)
		{
			if (IsSaveFileExists(fileName))
			{
				string json = File.ReadAllText(GetSaveFilePath(fileName));
				data = JsonConvert.DeserializeObject<T>(json, JsonSettings);
				return true;
			}
			else
			{
				data = default;
				return false;
			}
		}

		public static void DeleteSaveFile(string fileName)
		{
			if (IsSaveFileExists(fileName))
			{
				File.Delete(GetSaveFilePath(fileName));
			}
		}
	}
}