using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	/// <summary>게임 업적의 한 단계와 Steam 업적 API 이름 하나의 연결</summary>
	[Serializable]
	public struct SteamAchievementLink
	{
		[Tooltip("게임 업적 AchievementSO ID")]
		[FormerlySerializedAs("questId")]
		[SerializeField] private int achievementId;

		[Tooltip("그 업적의 몇 단계에 연결할지 (1부터)")]
		[SerializeField] private int tier;

		[Tooltip("Steamworks Stats & Achievements 의 API 이름 (대문자, 숫자, 밑줄)")]
		[SerializeField] private string apiName;

		public SteamAchievementLink(int achievementId, int tier, string apiName)
		{
			this.achievementId = achievementId;
			this.tier = tier;
			this.apiName = apiName;
		}

		public int AchievementId => achievementId;
		public int Tier => tier;
		public AchievementStep Step => new(achievementId, tier);
		public string ApiName => apiName;
	}
}
