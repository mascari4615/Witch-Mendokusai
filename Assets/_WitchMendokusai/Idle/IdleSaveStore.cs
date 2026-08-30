using System;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai
{
	/// <summary>
	/// 방치 판을 디스크에 적고 되읽는다 (TASK-WM-406).
	///
	/// ★ 이 파일은 이제 <b>얇다</b>. 잃지 않게 넣고 꺼내는 손놀림은
	///   <see cref="IdleSaveFiles"/>(코어) 로 내렸다 — 거기엔 Unity 가 없어서
	///   엔진 밖 시험이 <b>실제로 돈다</b>. 사람의 판을 지키는 자리라 시험이 없으면 안 된다.
	///   여기 남은 것은 그릇 쪽 결정뿐이다: <b>어디에</b>(persistentDataPath)
	///   <b>어떤 꼴로</b>(JsonUtility) 적을지, 그리고 사람에게 뭐라고 말할지.
	/// </summary>
	public static class IdleSaveStore
	{
		private const string FILE_NAME = "idle.json";

		private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, FILE_NAME);

		/// <summary>없으면 <c>null</c> — 처음 켠 사람이다.</summary>
		public static IdleSaveData? Load()
		{
			string path = FilePath;

			IdleSaveFiles.ReadOutcome outcome = IdleSaveFiles.Read(path, LooksLikeJson, out string payload);

			switch (outcome)
			{
				case IdleSaveFiles.ReadOutcome.FellBackToBackup:
					Debug.LogWarning("[Idle] 본 저장이 못 쓸 것이라 <직전 판>으로 되살렸다."
						+ " 깨진 것은 " + IdleSaveFiles.BrokenPathFor(path) + " 에 있다.");
					break;

				case IdleSaveFiles.ReadOutcome.Lost:
					Debug.LogWarning("[Idle] 저장도 직전 판도 못 읽었다. 이번 판은 처음부터 돈다."
						+ " 깨진 것은 " + IdleSaveFiles.BrokenPathFor(path) + " 에 있다.");
					return null;

				case IdleSaveFiles.ReadOutcome.Nothing:
					return null;
			}

			try
			{
				return JsonUtility.FromJson<IdleSaveData>(payload);
			}
			catch (Exception error)
			{
				Debug.LogWarning("[Idle] 저장을 읽긴 했는데 꼴이 안 맞는다. 처음부터 돈다: " + error.Message);
				return null;
			}
		}

		public static void Save(IdleSaveData saveData)
		{
			try
			{
				IdleSaveFiles.Write(FilePath, JsonUtility.ToJson(saveData));
			}
			catch (Exception error)
			{
				Debug.LogError("[Idle] 저장에 실패했다: " + error.Message);
			}
		}

		/// <summary>
		/// 반쯤 적히다 만 것을 <b>읽기 전에</b> 걸러낸다 — 판정은 코어가 한다.
		///
		/// 규칙을 여기 두면 시험이 안 닿는다(실제로 안 닿고 있었다).
		/// </summary>
		private static bool LooksLikeJson(string text)
		{
			return IdleSaveFiles.LooksLikeSave(text);
		}

		/// <summary>
		/// 저장 삭제 (디버그. 데이터 초기화). 본 저장, .bak, .broken 전부.
		/// 지운 뒤 판 재구성 필요. 메모리의 세션은 이 함수 밖
		/// </summary>
		public static int Wipe()
		{
			try
			{
				int deleted = IdleSaveFiles.Delete(FilePath);
				Debug.Log("[Idle] 저장을 지웠다: " + deleted + "개 파일. " + FilePath);
				return deleted;
			}
			catch (Exception error)
			{
				Debug.LogError("[Idle] 저장을 못 지웠다: " + error.Message);
				return 0;
			}
		}

		/// <summary>지금을 초 단위로. 코어가 「자리를 비운 동안」을 재는 기준이다.</summary>
		public static long NowUnixSeconds()
		{
			return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}
	}
}
