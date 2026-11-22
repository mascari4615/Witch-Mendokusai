using System.Collections.Generic;

namespace WitchMendokusai
{
	public enum AchievementWallChange
	{
		Pinned = 0,
		Unpinned = 1,
		Full = 2,
	}

	/// <summary>
	/// 메달 벽에 건 업적. 칸 수는 화면 설정 (AchievementScreenSettings.WallSlotCount) 이 정하고, 순서는 건 순서.
	/// 사용자가 한 번도 손대지 않았으면 높은 단계부터 자동으로 채우고, 손댄 뒤로는 사용자 목록을 그대로 둠. 엔진 없음
	/// </summary>
	public sealed class AchievementWall
	{
		private readonly List<int> pins;
		private readonly AchievementSaveSlice slice;

		public AchievementWall(AchievementSaveSlice slice)
		{
			this.slice = slice;
			pins = slice.WallPins;
		}

		public IReadOnlyList<int> Pins => pins;

		public bool IsPinned(int achievementId) => pins.Contains(achievementId);

		/// <summary>
		/// 화면을 열 때 부름. 못 닿은 업적과 넘치는 칸을 덜어 내고, 손대지 않은 벽이면 빈 칸을 높은 단계 순으로 채움.
		/// ordered 는 화면의 업적 순서 (분류, ID). 같은 단계는 이 순서를 따름
		/// </summary>
		public void Refresh(IReadOnlyList<int> ordered, AchievementBook book, int slotCount)
		{
			pins.RemoveAll(id => book.TierOf(id) == 0);
			if (pins.Count > slotCount)
			{
				pins.RemoveRange(slotCount, pins.Count - slotCount);
			}
			if (slice.WallEdited)
			{
				return;
			}
			// 손대지 않은 벽은 저장된 목록을 믿지 않고 매번 다시 고름 (나중에 금을 따면 동을 밀어냄)
			pins.Clear();

			List<int> candidates = new();
			foreach (int id in ordered)
			{
				if (book.TierOf(id) > 0 && pins.Contains(id) == false)
				{
					candidates.Add(id);
				}
			}
			// 단계가 높은 것 먼저, 같으면 화면 순서 (안정 정렬이 아니라서 순번을 같이 비교)
			List<int> order = new(ordered);
			candidates.Sort((left, right) =>
			{
				int byTier = book.TierOf(right).CompareTo(book.TierOf(left));
				return byTier != 0 ? byTier : order.IndexOf(left).CompareTo(order.IndexOf(right));
			});
			foreach (int id in candidates)
			{
				if (pins.Count >= slotCount)
				{
					break;
				}
				pins.Add(id);
			}
		}

		/// <summary>걸려 있으면 내리고, 아니면 빈 칸에 검. 칸이 다 찼으면 바꾸지 않고 Full. 손댄 벽으로 표시</summary>
		public AchievementWallChange Toggle(int achievementId, int slotCount)
		{
			if (pins.Remove(achievementId))
			{
				slice.WallEdited = true;
				return AchievementWallChange.Unpinned;
			}
			if (pins.Count >= slotCount)
			{
				return AchievementWallChange.Full;
			}
			pins.Add(achievementId);
			slice.WallEdited = true;
			return AchievementWallChange.Pinned;
		}
	}
}
