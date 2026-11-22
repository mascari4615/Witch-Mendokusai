using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>게임 업적 하나와 Steam 업적 API 이름 하나의 연결</summary>
	[Serializable]
	public struct SteamAchievementLink
	{
		[Tooltip("게임 업적 QuestSO ID (Achievement 타입)")]
		[SerializeField] private int questId;

		[Tooltip("Steamworks Stats & Achievements 의 API 이름 (대문자, 숫자, 밑줄)")]
		[SerializeField] private string apiName;

		public SteamAchievementLink(int questId, string apiName)
		{
			this.questId = questId;
			this.apiName = apiName;
		}

		public int QuestId => questId;
		public string ApiName => apiName;
	}
}
