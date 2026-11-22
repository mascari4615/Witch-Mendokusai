using Steamworks;
using Steamworks.Data;

namespace WitchMendokusai
{
	/// <summary>Steam 업적 쓰기 창구. Facepunch.Steamworks 호출만, 판단은 AchievementRelay 몫</summary>
	public sealed class SteamAchievementPort : IAchievementPlatformPort
	{
		public bool Unlock(string platformAchievementId)
		{
			Achievement achievement = new Achievement(platformAchievementId);
			// apply false: 저장은 Flush 에서 한 번에
			return achievement.Trigger(false);
		}

		public void Flush() => SteamUserStats.StoreStats();
	}
}
