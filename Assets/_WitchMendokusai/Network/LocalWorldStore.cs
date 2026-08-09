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
		public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FILE_NAME);

		/// <summary>지난 기억. 없거나 망가졌으면 null — 그때는 빈 세계로 시작한다.</summary>
		public static WorldSaveData TryLoad()
		{
			try
			{
				if (File.Exists(Path) == false)
					return null;

				return JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(Path));
			}
			catch (IOException error)
			{
				Debug.LogWarning("[world] 기억을 못 읽었다 — 빈 세계로 시작한다: " + error.Message);
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
				if (File.Exists(Path))
					File.Delete(Path);

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
