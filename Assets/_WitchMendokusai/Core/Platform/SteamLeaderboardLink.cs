using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>게임 기록 키 하나와 Steam 순위표 이름 하나의 연결</summary>
	[Serializable]
	public struct SteamLeaderboardLink
	{
		[Tooltip("게임 기록 키 (던전은 Dungeon.{던전 ID}.{항목})")]
		[SerializeField] private string recordKey;

		[Tooltip("Steam 순위표 이름. 없으면 게임이 처음 올릴 때 이 정렬과 표시로 만듦")]
		[SerializeField] private string leaderboardName;

		[Tooltip("작은 값이 위 (최단 클리어 등). 끄면 큰 값이 위")]
		[SerializeField] private bool lowerIsBetter;

		[Tooltip("값을 초 단위 시간으로 표시. 끄면 숫자")]
		[SerializeField] private bool displayAsSeconds;

		public SteamLeaderboardLink(string recordKey, string leaderboardName, bool lowerIsBetter, bool displayAsSeconds)
		{
			this.recordKey = recordKey;
			this.leaderboardName = leaderboardName;
			this.lowerIsBetter = lowerIsBetter;
			this.displayAsSeconds = displayAsSeconds;
		}

		public string RecordKey => recordKey;
		public string LeaderboardName => leaderboardName;
		public bool LowerIsBetter => lowerIsBetter;
		public bool DisplayAsSeconds => displayAsSeconds;
	}
}
