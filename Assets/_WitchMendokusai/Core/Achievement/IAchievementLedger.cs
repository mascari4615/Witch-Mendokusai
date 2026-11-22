using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임이 정본인 업적 장부의 읽기 창구. 완료된 업적 ID (QuestSO ID) 목록.
	/// 플랫폼 (Steam 등) 은 이 목록을 읽어 자기 쪽에 적기만 함. 플랫폼 상태를 게임으로 되읽는 창구는 없음
	/// </summary>
	public interface IAchievementLedger
	{
		IReadOnlyCollection<int> CompletedAchievementIds { get; }
	}
}
