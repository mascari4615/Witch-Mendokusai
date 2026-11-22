using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임이 정본인 업적 장부의 읽기 창구. 달성한 (업적 ID, 단계) 목록.
	/// 3단계까지 간 업적은 1, 2, 3 단계가 모두 들어 있음 (플랫폼이 중간 단계에 연결돼 있어도 받게).
	/// 플랫폼 (Steam 등) 은 이 목록을 읽어 자기 쪽에 적기만 함. 플랫폼 상태를 게임으로 되읽는 창구는 없음
	/// </summary>
	public interface IAchievementLedger
	{
		IReadOnlyCollection<AchievementStep> UnlockedSteps { get; }
	}
}
