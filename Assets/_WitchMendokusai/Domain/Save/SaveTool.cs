using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace KarmoLabs
{
	/// <summary> 저장 파일을 어디서 읽었나. 알림 화면이 복구와 새 게임 시작을 가르는 데 씀 </summary>
	public enum SaveReadSource
	{
		/// <summary> 본 파일도 앞 판도 없음. 첫 실행 </summary>
		Missing = 0,
		Main = 1,
		/// <summary> 본 파일이 깨져 앞 판에서 읽음 </summary>
		Backup = 2,
		/// <summary> 파일은 있었는데 둘 다 못 읽음 </summary>
		Unreadable = 3,
	}

	/// <summary>
	/// 저장 파일 읽고 쓰기
	/// 쓰기: 임시 파일에 쓴 뒤 바꿔 끼움. 쓰는 도중 꺼져도 앞 판 유지
	/// 읽기: 저장 파일은 바깥 입력이라 여기서만 관대. 못 읽으면 앞 판 (.bak), 그것도 안 되면 없는 것으로 처리
	/// 못 읽은 파일: 지우지 않고 옆으로 옮김
	/// </summary>
	public class SaveTool
	{
		public const string TEMP_SUFFIX = ".tmp";
		public const string BACKUP_SUFFIX = ".bak";
		public const string CORRUPT_INFIX = ".corrupt-";

		private static readonly JsonSerializerSettings JsonSettings = new() { TypeNameHandling = TypeNameHandling.Auto, };
		// Application.dataPath 는 Android/iOS 에서 APK/IPA 내부(읽기 전용)를
		// 가리켜 DirectoryNotFoundException 유발. persistentDataPath 는 모든
		// 지원 플랫폼(Standalone/Android/iOS/WebGL)에서 앱 전용 read/write
		// 경로 보장 — 세이브 파일의 정본 위치.
		public static string GetSaveFilePath(string fileName) => Path.Combine(Application.persistentDataPath, fileName);

		public static void SaveFile<T>(string fileName, T data)
		{
			string json = JsonConvert.SerializeObject(data, Formatting.Indented, JsonSettings);
			WriteAtomic(GetSaveFilePath(fileName), json);
		}

		public static bool TryLoadFile<T>(string fileName, out T data) where T : class
			=> TryReadWithBackup(GetSaveFilePath(fileName), out data);

		/// <summary> 임시 파일에 다 쓴 뒤 바꿔 끼움. 앞 판은 .bak 로 남는다. </summary>
		public static void WriteAtomic(string path, string contents)
		{
			string tempPath = path + TEMP_SUFFIX;
			File.WriteAllText(tempPath, contents);

			if (File.Exists(path))
				File.Replace(tempPath, path, path + BACKUP_SUFFIX);
			else
				File.Move(tempPath, path);
		}

		public static bool TryLoadFile<T>(string fileName, out T data, out SaveReadSource source) where T : class
			=> TryReadWithBackup(GetSaveFilePath(fileName), out data, out source);

		/// <summary> 본 파일을 읽고, 못 읽으면 앞 판을 읽는다. 둘 다 안 되면 false. </summary>
		public static bool TryReadWithBackup<T>(string path, out T data) where T : class
			=> TryReadWithBackup(path, out data, out SaveReadSource _);

		/// <summary> source: 어느 파일에서 읽었는지. 못 읽었으면 파일이 아예 없었는지 (Missing), 있었는데 깨졌는지 (Unreadable) </summary>
		public static bool TryReadWithBackup<T>(string path, out T data, out SaveReadSource source) where T : class
		{
			string backupPath = path + BACKUP_SUFFIX;
			bool anyFileExisted = File.Exists(path) || File.Exists(backupPath);

			if (TryRead(path, out data))
			{
				source = SaveReadSource.Main;
				return true;
			}

			if (TryRead(backupPath, out data))
			{
				Debug.LogWarning($"[{nameof(SaveTool)}] 저장 파일을 못 읽어 앞 판으로 불러옴: {Path.GetFileName(path)}");
				source = SaveReadSource.Backup;
				return true;
			}

			source = anyFileExisted ? SaveReadSource.Unreadable : SaveReadSource.Missing;
			return false;
		}

		private static bool TryRead<T>(string path, out T data) where T : class
		{
			data = null;
			if (File.Exists(path) == false)
				return false;

			try
			{
				data = JsonConvert.DeserializeObject<T>(File.ReadAllText(path), JsonSettings);
			}
			// 내용이 깨진 경우만 잡음. 잠금이나 권한 같은 입출력 실패는 멀쩡한 세이브일 수 있어 그대로 던짐
			catch (JsonException exception)
			{
				Debug.LogError($"[{nameof(SaveTool)}] 저장 파일 읽기 실패: {Path.GetFileName(path)} ({exception.GetType().Name}: {exception.Message})");
				data = null;
			}

			// 빈 파일은 예외 없이 null 로 읽힘
			if (data != null)
				return true;

			Quarantine(path);
			return false;
		}

		// 못 읽은 파일을 옆으로 옮김. 다음 저장이 덮어쓰지 않게, 그리고 나중에 살펴볼 수 있게
		private static void Quarantine(string path)
		{
			string corruptPath = $"{path}{CORRUPT_INFIX}{DateTime.Now:yyyyMMdd-HHmmss}";
			if (File.Exists(corruptPath))
				File.Delete(corruptPath);
			File.Move(path, corruptPath);
			Debug.LogWarning($"[{nameof(SaveTool)}] 못 읽은 저장 파일을 옮김: {Path.GetFileName(corruptPath)}");
		}

		public static void DeleteSaveFile(string fileName)
		{
			// 앞 판을 남기면 지운 뒤 읽기가 앞 판을 되살림
			string path = GetSaveFilePath(fileName);
			foreach (string target in new[] { path, path + BACKUP_SUFFIX, path + TEMP_SUFFIX })
			{
				if (File.Exists(target))
					File.Delete(target);
			}
		}
	}
}
