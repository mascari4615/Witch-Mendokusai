using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 판 밖에 남는 것(TASK-WM-194) — 유물과 뽑기.
	///
	/// ★ 왜 필요한가: 지금까지 한 판이 끝나면 최고 기록 숫자 하나만 남았다. 그러면 다음 판이 지난 판과
	///   똑같아서 「또 해야 할 이유」가 기록 경신뿐이다. 버틴 만큼 유물이 남고, 그 유물로 새 포탑 인형을
	///   뽑아 *다음 판의 선택지 자체가 늘어나야* 개척이 이어지는 이야기가 된다(사용자 선택: 판 안 + 판 밖).
	///
	/// 규칙만 순수하게 둔다 — 저장은 호출자(GameData)가, 무작위는 인자로 받는다(테스트 가능).
	/// </summary>
	public static class TowerDefenseMeta
	{
		/// <summary> 한 판에서 얻는 유물 = 버틴 웨이브 × 계수 + 참가 보상. 0파에서 끝나도 빈손은 아니다. </summary>
		public static int RelicsFor(int wavesCleared, int perWave, int baseReward)
		{
			int waves = wavesCleared < 0 ? 0 : wavesCleared;
			return baseReward + waves * perWave;
		}

		/// <summary> 아직 안 뽑은 포탑이 남았는가. </summary>
		public static bool HasLockedTower(int towerCount, int defaultUnlockedCount, IReadOnlyList<int> unlocked)
		{
			return LockedIndices(towerCount, defaultUnlockedCount, unlocked).Count > 0;
		}

		/// <summary>
		/// 유물을 써서 하나 뽑는다. 잠긴 것 중에서만 나오므로 *중복이 없다* —
		/// 뽑았는데 이미 가진 게 또 나오는 건 「늘어나는 선택지」라는 목적에 어긋난다.
		/// </summary>
		public static bool TryPull(
			int towerCount,
			int defaultUnlockedCount,
			List<int> unlocked,
			ref int relics,
			int cost,
			float roll,
			out int pulledIndex)
		{
			pulledIndex = -1;
			if (unlocked == null || relics < cost || cost < 0)
				return false;

			List<int> locked = LockedIndices(towerCount, defaultUnlockedCount, unlocked);
			if (locked.Count == 0)
				return false;

			int pick = Mathf.Clamp(Mathf.FloorToInt(roll * locked.Count), 0, locked.Count - 1);
			pulledIndex = locked[pick];

			relics -= cost;
			unlocked.Add(pulledIndex);
			return true;
		}

		/// <summary> 지금 쓸 수 있는 포탑인가 — 처음부터 주는 것 + 뽑아서 얻은 것. </summary>
		public static bool IsUnlocked(int towerIndex, int defaultUnlockedCount, IReadOnlyList<int> unlocked)
		{
			if (towerIndex < defaultUnlockedCount)
				return true;
			if (unlocked == null)
				return false;

			foreach (int index in unlocked)
			{
				if (index == towerIndex)
					return true;
			}
			return false;
		}

		private static List<int> LockedIndices(int towerCount, int defaultUnlockedCount, IReadOnlyList<int> unlocked)
		{
			List<int> locked = new();
			for (int index = defaultUnlockedCount; index < towerCount; index++)
			{
				if (IsUnlocked(index, defaultUnlockedCount, unlocked) == false)
					locked.Add(index);
			}
			return locked;
		}
	}
}
