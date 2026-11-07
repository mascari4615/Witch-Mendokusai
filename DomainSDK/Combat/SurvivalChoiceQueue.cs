namespace WitchMendokusai
{
	// 생존 레벨업 선택과 상자 개봉 대기열. 상자 개봉 중 레벨업은 대기, 선택 중 상자도 대기. 선택이 다 끝난 뒤 상자 개봉
	public sealed class SurvivalChoiceQueue
	{
		public int PendingLevelUps { get; private set; }
		public int PendingChests { get; private set; }
		public bool IsChestOpen { get; private set; }

		// true면 지금 선택 화면 시작. 이미 선택 중이거나 상자가 열려 있으면 대기열에만 추가
		public bool AddLevelUp()
		{
			PendingLevelUps++;
			return PendingLevelUps == 1 && IsChestOpen == false;
		}

		// 카드 선택, 스킵, 새 장비가 레벨업 하나를 소비
		public void ConsumeLevelUp()
		{
			if (PendingLevelUps > 0)
				PendingLevelUps--;
		}

		// 고를 카드가 없을 때 남은 레벨업 전부를 보상으로 바꾸는 경로. 바꿀 개수 반환
		public int TakeAllLevelUps()
		{
			int count = PendingLevelUps;
			PendingLevelUps = 0;
			return count;
		}

		public void AddChest() => PendingChests++;

		// true면 지금 상자 하나 개봉. 선택이 남았거나 이미 열려 있으면 대기
		public bool TryOpenChest()
		{
			if (PendingChests == 0 || IsChestOpen || PendingLevelUps > 0)
				return false;

			PendingChests--;
			IsChestOpen = true;
			return true;
		}

		// true면 열려 있던 상자를 닫음
		public bool CloseChest()
		{
			if (IsChestOpen == false)
				return false;

			IsChestOpen = false;
			return true;
		}

		// 선택 취소. 대기 상자는 판 안에서 유지
		public void CancelSelection()
		{
			PendingLevelUps = 0;
			IsChestOpen = false;
		}

		public void ResetRun()
		{
			CancelSelection();
			PendingChests = 0;
		}
	}
}
