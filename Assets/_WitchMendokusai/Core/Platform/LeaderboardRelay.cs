using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 최고 기록을 플랫폼 순위표 하나로 옮기는 중계. 업적 중계 (AchievementRelay) 와 같은 원칙.
	///
	/// - 주도권은 게임: 연결표에 있는 기록 키만 올림. 표에 없는 기록은 게임 전용
	/// - 한 방향: 플랫폼 순위를 읽지 않음. 어느 쪽이 좋은 기록인지는 게임과 순위표 정렬이 판단, 중계는 비교하지 않음
	/// - 한 번에 하나만 올림 (Steam 은 진행 중 올리기를 1건만 받음). 기다리는 동안 같은 키에 새 값이 오면 마지막 값 하나로 합침
	/// - 올리기에 성공한 값은 이 실행 동안 다시 올리지 않음. 실패한 것은 기록하지 않음 (다음 알림에서 다시 시도)
	/// </summary>
	public sealed class LeaderboardRelay
	{
		private readonly IReadOnlyDictionary<string, string> leaderboardByRecordKey;
		private readonly ILeaderboardPlatformPort port;
		private readonly Dictionary<string, int> submitted = new();
		private readonly Dictionary<string, int> pending = new();
		private readonly Queue<string> pendingOrder = new();
		private bool isPumping;

		public LeaderboardRelay(IReadOnlyDictionary<string, string> leaderboardByRecordKey, ILeaderboardPlatformPort port)
		{
			this.leaderboardByRecordKey = leaderboardByRecordKey;
			this.port = port;
		}

		/// <summary>알려진 최고 기록 전체를 맞춤 (플랫폼 연결 직후, 새 알림마다)</summary>
		public void Resync(IReadOnlyDictionary<string, int> bests)
		{
			foreach (KeyValuePair<string, int> record in bests)
			{
				OnBestReported(record.Key, record.Value);
			}
		}

		public void OnBestReported(string recordKey, int best)
		{
			if (leaderboardByRecordKey.ContainsKey(recordKey) == false)
			{
				return;
			}
			if (submitted.TryGetValue(recordKey, out int sent) && sent == best)
			{
				return;
			}
			if (pending.ContainsKey(recordKey) == false)
			{
				pendingOrder.Enqueue(recordKey);
			}
			pending[recordKey] = best;
			if (isPumping)
			{
				return;
			}
			_ = Pump();
		}

		private async Task Pump()
		{
			isPumping = true;
			try
			{
				while (pendingOrder.Count > 0)
				{
					string recordKey = pendingOrder.Dequeue();
					int score = pending[recordKey];
					pending.Remove(recordKey);
					if (submitted.TryGetValue(recordKey, out int sent) && sent == score)
					{
						continue;
					}
					string leaderboardName = leaderboardByRecordKey[recordKey];
					if (await TrySubmit(leaderboardName, score))
					{
						submitted[recordKey] = score;
					}
					else
					{
						Debug.LogWarning("[Leaderboard] 순위표 올리기 실패: " + leaderboardName + " (기록 " + recordKey + ", " + score + "). 순위표가 플랫폼에 없거나 플랫폼이 준비 전");
					}
				}
			}
			finally
			{
				isPumping = false;
			}
		}

		private async Task<bool> TrySubmit(string leaderboardName, int score)
		{
			try
			{
				return await port.SubmitBest(leaderboardName, score);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Leaderboard] 순위표 호출 예외: " + exception.Message);
				return false;
			}
		}
	}
}
