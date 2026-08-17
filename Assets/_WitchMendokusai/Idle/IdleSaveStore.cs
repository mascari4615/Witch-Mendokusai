using System;
using System.IO;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>
	/// 방치 판을 디스크에 적고 되읽는다 (TASK-WM-406).
	///
	/// ★ 왜 코어가 아니라 여기인가 — <see cref="IdleState"/> 는 Unity 를 모른다(엔진 밖에서도 돈다).
	///   「어디에 어떤 형식으로 적을지」는 그릇을 쥔 쪽의 결정이라 엔진 편에 둔다.
	///   코어가 주는 것은 <see cref="IdleSaveData"/> 하나뿐이고, 그게 경계다.
	///
	/// ★ 임시 파일에 적고 <b>바꿔치기</b> 한다. 적는 도중에 게임이 죽으면 원본이 반쯤 덮여
	///   다음 실행에서 판이 통째로 사라진다 — 방치형에서 그건 몇 주치를 잃는 것과 같다.
	/// </summary>
	public static class IdleSaveStore
	{
		private const string FILE_NAME = "idle.json";

		/// <summary>못 읽은 저장을 옮겨 두는 이름 — 덮어쓰이기 전에 치운다.</summary>
		private const string BROKEN_NAME = "idle.broken.json";

		/// <summary>
		/// <b>직전 판</b>. 바꿔치기가 공짜로 남겨 주는 한 세대다 (실측 2026-08-17).
		///
		/// 이게 있으면 저장이 깨져도 「통째로 잃음」이 아니라 「자동 저장 한 번치 잃음」이 된다.
		/// 방치형에서 그 차이는 몇 주치와 몇 초치의 차이다.
		/// </summary>
		private const string BACKUP_NAME = "idle.json.bak";

		private static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

		/// <summary>없으면 <c>null</c> — 처음 켠 사람이다.</summary>
		public static IdleSaveData? Load()
		{
			string path = FilePath;

			if (File.Exists(path) == false)
			{
				return null;
			}

			try
			{
				string json = File.ReadAllText(path);
				return JsonUtility.FromJson<IdleSaveData>(json);
			}
			catch (Exception error)
			{
				// ★ 깨진 저장은 <b>옆으로 치운다</b>. 전에는 「사람이 들여다볼 수 있게 남긴다」고
				//   적어 놓고 그대로 뒀는데, 몇 초 뒤 자동 저장이 그 위를 덮어써서 <b>증거가 사라졌다</b>.
				//   남기려면 자리를 옮겨야 한다 — 말만으로는 안 남는다.
				MoveAside(path);

				// ★ 처음부터 시작하기 <b>전에</b> 직전 판을 본다. 바꿔치기가 남겨 둔 한 세대라
				//   대개 멀쩡하다 — 잃는 것이 몇 주치에서 자동 저장 한 번치로 줄어든다.
				IdleSaveData? older = LoadBackup();
				if (older.HasValue)
				{
					Debug.LogWarning("[Idle] 저장이 깨져서 <직전 판>으로 되살렸다 (깨진 것은 "
						+ BROKEN_NAME + " 에 있다): " + error.Message);
					return older;
				}

				Debug.LogWarning("[Idle] 저장을 못 읽었고 직전 판도 없다. 깨진 파일은 " + BROKEN_NAME
					+ " 로 옮겨 뒀고, 이번 판은 처음부터 돈다: " + error.Message);
				return null;
			}
		}

		public static void Save(IdleSaveData saveData)
		{
			string path = FilePath;
			string temporary = path + ".tmp";

			try
			{
				// ★ <b>디스크까지</b> 밀어 넣고 나서 바꿔치기한다. 운영체제 캐시에만 있는 채로
				//   전원이 나가면 새 파일이 <b>빈 껍데기</b>로 남는다 — 그 위로 바꿔치기하면
				//   멀쩡한 원본을 빈 파일로 갈아 버리는 셈이다.
				byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(saveData));

				using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write))
				{
					stream.Write(bytes, 0, bytes.Length);
					stream.Flush(true);
				}

				SwapIntoPlace(temporary, path);
			}
			catch (Exception error)
			{
				Debug.LogError("[Idle] 저장에 실패했다: " + error.Message);
			}
		}

		/// <summary>
		/// 새 파일을 <b>제자리로 바꿔치기</b>한다.
		///
		/// ⚠ 전에는 「지우고 → 옮기기」였다. 그러면 둘 사이에 게임이 죽는 순간
		///   <b>저장이 통째로 없어진다</b> — 이 파일 머리말이 막겠다고 적어 둔 바로 그 사고다.
		///   글은 바꿔치기라고 적혀 있는데 코드는 지우고 있었다.
		///   <see cref="File.Replace(string, string, string)"/> 는 그 창이 없다.
		/// </summary>
		private static void SwapIntoPlace(string temporary, string path)
		{
			if (File.Exists(path) == false)
			{
				File.Move(temporary, path);
				return;
			}

			try
			{
				// ⚠ 백업 이름을 <b>반드시 준다</b>. null 로 부르면 이 환경에서 곧바로 터진다
				//   (실측 2026-08-17: ArgumentException 「경로 형식이 잘못되었습니다」).
				//   게다가 백업은 공짜로 <b>직전 판</b>을 남겨 줘서, 저장이 깨졌을 때 되살릴 것이 생긴다.
				File.Replace(temporary, path, Path.Combine(Application.persistentDataPath, BACKUP_NAME));
			}
			catch (PlatformNotSupportedException)
			{
				// 바꿔치기를 못 하는 그릇(일부 파일 시스템)에서는 옛 방식으로 물러선다.
				// 창이 다시 생기므로 <b>조용히</b> 넘어가지 않는다.
				Debug.LogWarning("[Idle] 이 그릇은 바꿔치기를 못 한다 — 지우고 옮기는 옛 방식으로 돈다"
					+ " (적는 도중에 죽으면 저장이 사라질 수 있다).");

				File.Delete(path);
				File.Move(temporary, path);
			}
		}

		/// <summary>직전 판을 읽어 본다 — 그것마저 깨졌으면 <c>null</c>.</summary>
		private static IdleSaveData? LoadBackup()
		{
			string backup = Path.Combine(Application.persistentDataPath, BACKUP_NAME);

			if (File.Exists(backup) == false)
			{
				return null;
			}

			try
			{
				return JsonUtility.FromJson<IdleSaveData>(File.ReadAllText(backup));
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>깨진 저장을 옆으로 치운다 — 다음 저장이 덮어쓰기 전에.</summary>
		private static void MoveAside(string path)
		{
			try
			{
				string broken = Path.Combine(Application.persistentDataPath, BROKEN_NAME);

				if (File.Exists(broken))
				{
					File.Delete(broken);
				}

				File.Move(path, broken);
			}
			catch (Exception error)
			{
				Debug.LogWarning("[Idle] 깨진 저장을 옮기지 못했다: " + error.Message);
			}
		}

		/// <summary>지금을 초 단위로. 코어가 「자리를 비운 동안」을 재는 기준이다.</summary>
		public static long NowUnixSeconds()
		{
			return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}
	}
}
