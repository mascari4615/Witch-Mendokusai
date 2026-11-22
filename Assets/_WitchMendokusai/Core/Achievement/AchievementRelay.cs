using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 업적을 플랫폼 하나로 옮기는 중계. 플랫폼 무관, 엔진 호출은 경고 로그뿐.
	///
	/// - 주도권은 게임: 게임 업적 단계 (업적 ID, 단계) 를 받아 연결표에 있는 것만 플랫폼에 적음. 표에 없는 업적은 게임 전용
	/// - 한 방향: 플랫폼 상태를 읽지 않음. 적기에 성공한 것은 이 실행 동안 다시 적지 않음
	/// - 실패한 것은 기록하지 않음 (플랫폼 준비 전 실패를 영구 실패로 굳히지 않게, 다음 완료 신호나 다음 실행에서 다시 시도)
	/// </summary>
	public sealed class AchievementRelay
	{
		private readonly IReadOnlyDictionary<AchievementStep, string> platformIdByStep;
		private readonly IAchievementPlatformPort port;
		private readonly HashSet<AchievementStep> relayedSteps = new();

		public AchievementRelay(IReadOnlyDictionary<AchievementStep, string> platformIdByStep, IAchievementPlatformPort port)
		{
			this.platformIdByStep = platformIdByStep;
			this.port = port;
		}

		/// <summary>게임 장부의 달성 목록을 통째로 맞춤 (부팅, 플랫폼 연결 직후)</summary>
		public int Resync(IEnumerable<AchievementStep> unlockedSteps)
		{
			int relayed = 0;
			foreach (AchievementStep step in unlockedSteps)
			{
				if (TryRelay(step))
				{
					relayed++;
				}
			}
			if (relayed > 0)
			{
				port.Flush();
			}
			return relayed;
		}

		/// <summary>게임 업적 하나가 방금 한 단계 오름</summary>
		public bool OnAchievementUnlocked(AchievementStep step)
		{
			if (TryRelay(step) == false)
			{
				return false;
			}
			port.Flush();
			return true;
		}

		private bool TryRelay(AchievementStep step)
		{
			if (relayedSteps.Contains(step))
			{
				return false;
			}
			if (platformIdByStep.TryGetValue(step, out string platformId) == false)
			{
				return false;
			}

			if (port.Unlock(platformId) == false)
			{
				Debug.LogWarning("[Achievement] 플랫폼 업적 적기 실패: " + platformId + " (게임 업적 " + step + "). 이름이 플랫폼에 없거나 플랫폼이 아직 준비 전");
				return false;
			}
			relayedSteps.Add(step);
			return true;
		}
	}
}
