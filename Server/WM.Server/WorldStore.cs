using System;
using System.IO;
using System.Text.Json;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 세계의 기억을 <b>디스크에 둔다</b> (TASK-WM-217 단계 5).
	///
	/// 무엇을 기억할지는 판정 층(<see cref="WorldSaveData"/>)이 정하고, 여기는 <b>어디에·어떻게 쓰는지</b>만 안다.
	/// 그래서 나중에 파일이 아니라 DB 로 바뀌어도 세계 규칙은 안 바뀐다.
	///
	/// ★ <b>덮어쓰다 죽어도 세계가 안 사라져야 한다.</b> 파일에 직접 쓰다 전원이 나가면 반쯤 쓴 파일이 남고,
	///   그건 「세계가 통째로 사라진 것」과 같다. 그래서 <b>임시 파일에 다 쓴 뒤 갈아끼운다.</b>
	/// </summary>
	public sealed class WorldStore
	{
		// ★ IncludeFields 필수: 저장 모양(WorldSaveData)은 **필드**다(유니티 JsonUtility 가 그것만 읽어서).
		//   System.Text.Json 은 기본으로 필드를 무시한다 — 빼먹으면 조용히 `{}` 만 쓰고 세계가 사라진다.
		private static readonly JsonSerializerOptions options = new JsonSerializerOptions
		{
			WriteIndented = true,
			IncludeFields = true,
		};

		private readonly object gate = new object();

		public WorldStore(string path)
		{
			Path = path;
		}

		/// <summary>세계가 적히는 자리. 환경변수 <c>WM_WORLD_FILE</c> 로 바꾼다.</summary>
		public string Path { get; }

		/// <summary>기본 자리 — 서버 옆 <c>world.json</c>.</summary>
		public static WorldStore Default()
		{
			string path = Environment.GetEnvironmentVariable("WM_WORLD_FILE");
			if (string.IsNullOrWhiteSpace(path))
				path = System.IO.Path.Combine(AppContext.BaseDirectory, "world.json");

			return new WorldStore(path);
		}

		/// <summary>바로 앞 판 — 지금 것이 깨졌을 때 돌아갈 자리.</summary>
		public string BackupPath => Path + ".bak";

		/// <summary>
		/// 기억을 읽는다. 지금 것이 깨졌으면 <b>바로 앞 판</b>으로 되살린다 (TASK-WM-218).
		///
		/// ★ 왜: 이 한 파일에 건물·시각뿐 아니라 <b>사람들의 신원 장부</b>가 같이 들어 있다.
		///   파일 하나가 깨지면 모두가 「처음 온 사람」이 된다 — 세계에서 가장 잃으면 안 되는 것이다.
		///   둘 다 못 읽으면 빈 세계로 뜬다(안 뜨는 것보다 낫다).
		/// </summary>
		public WorldSaveData TryLoad()
		{
			lock (gate)
			{
				WorldSaveData current = TryReadFile(Path);
				if (current != null)
					return current;

				WorldSaveData backup = TryReadFile(BackupPath);
				if (backup != null)
				{
					Console.WriteLine("[world] 지금 기억이 깨졌다 — 바로 앞 판으로 되살린다: " + BackupPath);
					return backup;
				}

				return null;
			}
		}

		private static WorldSaveData TryReadFile(string path)
		{
			try
			{
				if (File.Exists(path) == false)
					return null;

				string json = File.ReadAllText(path);
				return JsonSerializer.Deserialize<WorldSaveData>(json, options);
			}
			catch (Exception error) when (error is IOException || error is JsonException || error is UnauthorizedAccessException)
			{
				Console.WriteLine("[world] 못 읽었다(" + path + "): " + error.Message);
				return null;
			}
		}

		/// <summary>기억을 쓴다. 다 쓴 뒤에 갈아끼우므로 도중에 죽어도 이전 기억이 남는다.</summary>
		public bool TrySave(WorldSaveData data)
		{
			if (data == null)
				return false;

			lock (gate)
			{
				try
				{
					string directory = System.IO.Path.GetDirectoryName(Path);
					if (string.IsNullOrEmpty(directory) == false)
						Directory.CreateDirectory(directory);

					string temporary = Path + ".tmp";
					File.WriteAllText(temporary, JsonSerializer.Serialize(data, options));

					// 갈아끼우기 전에 지금 판을 앞 판으로 넘긴다 — 새 판이 깨져도 돌아갈 자리가 남는다.
					if (File.Exists(Path))
						File.Copy(Path, BackupPath, overwrite: true);

					File.Move(temporary, Path, overwrite: true);
					return true;
				}
				catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
				{
					Console.WriteLine("[world] 기억을 못 썼다: " + error.Message);
					return false;
				}
			}
		}
	}
}
