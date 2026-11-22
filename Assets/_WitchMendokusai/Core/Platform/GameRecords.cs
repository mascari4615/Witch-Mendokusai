using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 최고 기록의 알림판. 기록의 정본은 각 기능의 세이브 (던전은 DungeonSaveData.Bests), 여기는 그 값을 알린 사본.
	///
	/// - 적는 쪽: 기능이 최고 기록을 알 때 (세이브 로드 뒤, 판 끝) Report. 무엇이 더 좋은 기록인지 (크게, 작게) 는 기능이 판단
	/// - 읽는 쪽: 플랫폼 순위표 어댑터. 연결 직후 Bests 를 한 번 읽고 BestReported 구독
	/// - 기록 키: 기능이 정함 (던전은 DungeonRecordRules.RecordKey)
	/// - 0 이하는 기록 없음. 같은 값은 다시 알리지 않음. 플랫폼 기록을 게임으로 되읽는 창구는 없음
	/// </summary>
	public static class GameRecords
	{
		private static readonly Dictionary<string, int> bests = new();

		public static IReadOnlyDictionary<string, int> Bests => bests;

		public static event Action<string, int> BestReported = delegate { };

		public static void Report(string recordKey, int best)
		{
			if (best <= 0)
			{
				return;
			}
			if (bests.TryGetValue(recordKey, out int known) && known == best)
			{
				return;
			}
			bests[recordKey] = best;
			BestReported(recordKey, best);
		}

		// 도메인 재로드를 끈 Play 진입에서도 이전 Play 의 값과 구독이 남지 않게
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetForPlay()
		{
			bests.Clear();
			BestReported = delegate { };
		}
	}
}
