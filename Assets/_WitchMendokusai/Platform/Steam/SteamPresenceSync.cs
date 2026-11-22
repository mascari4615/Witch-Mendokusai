using Steamworks;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 상태 (GamePresence) 를 Steam Rich Presence 로 옮기는 배선. SteamService 가 소유.
	///
	/// - steam_display 값은 Steamworks 에 올린 문구 파일의 토큰 (#Status_Town, #Status_Dungeon)
	/// - 토큰 안 %dungeon%, %level% 은 같은 이름의 키 값으로 채워짐
	/// - 한 방향: Steam 상태를 읽어 게임에 반영하지 않음
	/// </summary>
	public sealed class SteamPresenceSync
	{
		private const string DISPLAY_KEY = "steam_display";
		private const string TOKEN_PREFIX = "#Status_";
		private const string DUNGEON_KEY = "dungeon";
		private const string LEVEL_KEY = "level";

		public SteamPresenceSync()
		{
			GamePresence.Changed += Apply;
			Apply();
		}

		public void Stop()
		{
			GamePresence.Changed -= Apply;
			SteamFriends.ClearRichPresence();
		}

		private void Apply()
		{
			SteamFriends.SetRichPresence(DUNGEON_KEY, GamePresence.DungeonName);
			SteamFriends.SetRichPresence(LEVEL_KEY, GamePresence.Level.ToString());
			SteamFriends.SetRichPresence(DISPLAY_KEY, TOKEN_PREFIX + GamePresence.Status);
		}
	}
}
