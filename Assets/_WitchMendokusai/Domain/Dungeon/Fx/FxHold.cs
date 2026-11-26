namespace WitchMendokusai
{
	// 때린 뒤 남는 물체 하나의 수명. 판정 (LingerField) 과 그림 (SproutFx, TetherFx) 이 같은 값을 봄. 시각은 게임 시간
	public sealed class FxHold
	{
		public float StrikeTime;
		// 닿아도 안 아프게 식는 시각. 안 아픈 물체면 StrikeTime 과 같음
		public float HarmlessTime;
		// 그림이 사라지기 시작하는 시각
		public float EndTime;
		// 금이 가서 곧 터짐
		public bool IsCracked;
		// 터져서 바로 치움
		public bool IsGone;

		// 때리기 전에 끊겼거나 주인이 사라짐. 지금부터 식어 들어감
		public void Cancel(float time)
		{
			HarmlessTime = time;
			EndTime = time;
		}

		public bool IsHot(float time) => IsGone == false && time >= StrikeTime && time < HarmlessTime;
	}
}
