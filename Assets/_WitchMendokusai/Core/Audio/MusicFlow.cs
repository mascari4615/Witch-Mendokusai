namespace WitchMendokusai
{
	// 음악 전환 판단. 엔진 없이 시험할 수 있게 AudioManager 에서 뗌
	public static class MusicFlow
	{
		// 요청한 상태로 바꿔야 하나. 같은 상태를 다시 요청하면 곡을 처음부터 다시 틀지 않음
		// 승패 연출 중에는 던전과 보스 요청을 무시 (연출 지연 동안 웨이브가 돌아 보스가 나오거나 잡혀도 침묵 유지)
		// 승패 상태는 장면 전환이나 결과창의 실험실 요청으로만 빠져나감
		public static bool ShouldSwitch(bool hasCurrent, MusicState current, MusicState requested)
		{
			if (hasCurrent == false)
				return true;
			if (current == requested)
				return false;
			bool inOutcome = current == MusicState.Victory || current == MusicState.Defeat;
			bool combatRequest = requested == MusicState.Dungeon || requested == MusicState.Boss;
			return (inOutcome && combatRequest) == false;
		}

		// 지금 곡이 끝났을 때 다시 틀어야 하나. 경로가 빈 상태 (조용히 두기) 는 다시 틀 것이 없음
		public static bool ShouldRestart(bool loop, bool hasEventPath, bool stopped)
			=> loop && hasEventPath && stopped;

		// 상태는 바뀌어도 같은 곡이면 이어서 틀기 (시작 화면에서 로비처럼 같은 곡을 쓰는 상태 사이)
		public static bool IsSameTrack(string currentPath, string nextPath)
			=> string.IsNullOrEmpty(nextPath) == false && currentPath == nextPath;
	}
}
