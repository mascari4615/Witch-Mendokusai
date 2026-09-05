using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace KarmoLabs
{
	public class SaveTool
	{
		private static readonly JsonSerializerSettings JsonSettings = new() { TypeNameHandling = TypeNameHandling.Auto, };
		// Application.dataPath 는 Android/iOS 에서 APK/IPA 내부(읽기 전용)를
		// 가리켜 DirectoryNotFoundException 유발. persistentDataPath 는 모든
		// 지원 플랫폼(Standalone/Android/iOS/WebGL)에서 앱 전용 read/write
		// 경로 보장 — 세이브 파일의 정본 위치.
		public static string GetSaveFilePath(string fileName) => Path.Combine(Application.persistentDataPath, fileName);
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