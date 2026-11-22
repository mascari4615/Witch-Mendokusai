using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>지금 하는 일의 분류. 플랫폼 상태 표시 (Steam Rich Presence 등) 의 원천</summary>
	public enum PresenceStatus
	{
		Town = 0,
		Dungeon = 1,
	}

	/// <summary>
	/// 게임이 정본인 "지금 무엇을 하는 중" 상태. 게임이 적고 플랫폼 어댑터가 읽음.
	///
	/// - 적는 쪽: DungeonManager (판 시작, 레벨, 마을 복귀)
	/// - 읽는 쪽: 플랫폼 어댑터가 Changed 구독. 플랫폼 상태를 게임으로 되읽는 창구는 없음
	/// - 값이 같으면 Changed 를 내지 않음 (매 프레임 적어도 안전)
	/// </summary>
	public static class GamePresence
	{
		public static PresenceStatus Status { get; private set; }

		/// <summary>판 안에서만 값. 마을에서는 빈 글자</summary>
		public static string DungeonName { get; private set; } = string.Empty;

		/// <summary>판 안 플레이어 레벨. 마을에서는 0</summary>
		public static int Level { get; private set; }

		public static event Action Changed = delegate { };

		public static void EnterDungeon(string dungeonName)
		{
			Status = PresenceStatus.Dungeon;
			DungeonName = dungeonName;
			Level = 1;
			Changed();
		}

		public static void SetLevel(int level)
		{
			if (Status != PresenceStatus.Dungeon || Level == level)
			{
				return;
			}
			Level = level;
			Changed();
		}

		public static void ReturnToTown()
		{
			if (Status == PresenceStatus.Town)
			{
				return;
			}
			Status = PresenceStatus.Town;
			DungeonName = string.Empty;
			Level = 0;
			Changed();
		}

		// 도메인 재로드를 끈 Play 진입에서도 이전 Play 의 값과 구독이 남지 않게
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetForPlay()
		{
			Status = PresenceStatus.Town;
			DungeonName = string.Empty;
			Level = 0;
			Changed = delegate { };
		}
	}
}
