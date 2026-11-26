namespace WitchMendokusai.DomainSDK.Homestead
{
	// 손가락 톡 하나가 건설에서 뜻하는 것
	public enum BuildTap
	{
		Take = 0,
		Preview = 1,
		Put = 2,
	}

	// 건설 화면의 손가락 톡 판정. 든 것이 없으면 집기, 들고 있으면 첫 톡은 미리보기, 같은 칸 다시 톡은 놓기
	// 손가락에는 마우스의 "올려 두고 보기" 가 없어서 첫 톡이 그 몫. 던전 타워 배치와 같은 규칙
	public sealed class BuildTapRule
	{
		private bool hasPending;
		private int pendingX;
		private int pendingZ;

		public BuildTap Tap(int x, int z, bool isHolding)
		{
			if (isHolding == false)
			{
				hasPending = false;
				return BuildTap.Take;
			}
			if (hasPending && pendingX == x && pendingZ == z)
			{
				hasPending = false;
				return BuildTap.Put;
			}
			hasPending = true;
			pendingX = x;
			pendingZ = z;
			return BuildTap.Preview;
		}

		// 든 것이 바뀌면 앞서 고른 칸은 잊음. 새 것을 들자마자 한 번 톡에 놓이면 안 됨
		public void Forget() => hasPending = false;
	}

	public static class FishingTouch
	{
		// 낚시 손가락 버튼의 글자. 단계마다 지금 할 일
		public static string ButtonText(FishingPhase phase)
		{
			switch (phase)
			{
				case FishingPhase.Waiting:
					return "기다림";
				case FishingPhase.Bite:
					return "채기!";
				case FishingPhase.Duel:
					return "감기";
				default:
					return string.Empty;
			}
		}
	}
}
