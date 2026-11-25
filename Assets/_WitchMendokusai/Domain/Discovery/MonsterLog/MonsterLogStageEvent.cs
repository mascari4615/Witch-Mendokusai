namespace WitchMendokusai
{
	/// <summary>몬스터 기록이 방금 한 단계 올랐음 (첫 처치 포함). 한 번에 여러 단계를 건너뛰면 마지막 단계 하나만</summary>
	public struct MonsterLogStageEvent
	{
		public Monster Monster;
		public int Stage;
		public int MaxStage;
	}
}
