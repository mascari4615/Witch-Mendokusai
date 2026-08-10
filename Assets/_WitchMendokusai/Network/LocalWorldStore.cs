using System.IO;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 내 안의 세계도 <b>껐다 켜면 그대로 있다</b> (TASK-WM-217 단계 5).
	///
	/// 서버가 <c>world.json</c> 에 적는 것과 <b>같은 모양</b>(<see cref="WorldSaveData"/>)을 적는다 —
	/// 그래서 혼자 지은 세계를 나중에 서버로 올릴 수 있다(반대도 된다). 모양이 갈라지면 그 길이 막힌다.
	/// </summary>
	public static class LocalWorldStore
	{
		private const string FILE_NAME = "world.json";

		/// <summary>기기마다 다른 자리 — 유니티가 정해 주는 저장 폴더.</summary>
		/// <summary>
		/// 내 안의 세계가 적히는 자리.
		///
		/// ★ 환경변수 <c>WM_LOCAL_WORLD_FILE</c> 로 바꿀 수 있다 (TASK-WM-217, 실측 2026-08-10):
		///   관문이 <b>사람이 놀던 그 세계</b>에 그대로 들어가고 있었다. 판을 돌릴수록 들판이 고갈돼
		///   (뽑아 간 자리 46곳) 결국 「아무것도 못 줍는다」로 빨개졌다 — 코드는 멀쩡한데
		///   <b>검사가 자기가 남긴 흔적에 걸려 넘어지는</b> 구조였다. 관문은 자기 세계에서 재야 한다.
		/// </summary>
		public static string Path
		{
			get
			{
				string chosen = System.Environment.GetEnvironmentVariable("WM_LOCAL_WORLD_FILE");
				return string.IsNullOrWhiteSpace(chosen)
					? System.IO.Path.Combine(Application.persistentDataPath, FILE_NAME)
					: chosen;
			}
		}

		/// <summary>바로 앞 판 — 지금 것이 깨졌을 때 돌아갈 자리 (TASK-WM-218).</summary>
		public static string BackupPath => Path + ".bak";

		/// <summary>지난 기억. 지금 것이 깨졌으면 <b>바로 앞 판</b>으로 되살린다. 둘 다 안 되면 빈 세계.</summary>
		public static WorldSaveData TryLoad()
		{
			WorldSaveData current = TryRead(Path);
			if (current != null)
				return current;

			WorldSaveData backup = TryRead(BackupPath);
			if (backup != null)
				Debug.LogWarning("[world] 지금 기억이 깨졌다 — 바로 앞 판으로 되살린다.");

			return backup;
		}

		private static WorldSaveData TryRead(string path)
		{
			try
			{
				if (File.Exists(path) == false)
					return null;

				return JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(path));
			}
			catch (IOException error)
			{
				Debug.LogWarning("[world] 못 읽었다(" + path + "): " + error.Message);
				return null;
			}
		}

		/// <summary>기억을 쓴다. 서버와 같은 이유로 <b>다 쓴 뒤 갈아끼운다</b>(도중에 죽어도 이전 기억이 남는다).</summary>
		public static bool TrySave(WorldSaveData data)
		{
			if (data == null)
				return false;

			try
			{
				string temporary = Path + ".tmp";
				File.WriteAllText(temporary, JsonUtility.ToJson(data, true));

				// 갈아끼우기 전에 지금 판을 앞 판으로 — 새 판이 깨져도 돌아갈 자리가 남는다.
				if (File.Exists(Path))
				{
					File.Copy(Path, BackupPath, true);
					File.Delete(Path);
				}

				File.Move(temporary, Path);
				return true;
			}
			catch (IOException error)
			{
				Debug.LogWarning("[world] 기억을 못 썼다: " + error.Message);
				return false;
			}
		}
	}
}
