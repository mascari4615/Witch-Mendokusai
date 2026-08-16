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
				// 깨진 저장은 지우지 않는다 — 사람이 들여다볼 수 있게 남기고, 이번 판만 새로 시작한다.
				Debug.LogWarning("[Idle] 저장을 못 읽었다. 이번 판은 처음부터 돈다: " + error.Message);
				return null;
			}
		}

		public static void Save(IdleSaveData saveData)
		{
			string path = FilePath;
			string temporary = path + ".tmp";

			try
			{
				File.WriteAllText(temporary, JsonUtility.ToJson(saveData));

				if (File.Exists(path))
				{
					File.Delete(path);
				}

				File.Move(temporary, path);
			}
			catch (Exception error)
			{
				Debug.LogError("[Idle] 저장에 실패했다: " + error.Message);
			}
		}

		/// <summary>지금을 초 단위로. 코어가 「자리를 비운 동안」을 재는 기준이다.</summary>
		public static long NowUnixSeconds()
		{
			return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}
	}
}
