namespace WitchMendokusai
{
	/// <summary>
	/// 플랫폼 업적 쓰기 창구. 플랫폼마다 하나 (지금은 Steam). 쓰기만 있고 읽기는 없음
	/// </summary>
	public interface IAchievementPlatformPort
	{
		/// <summary>플랫폼 업적 하나를 달성 처리. 플랫폼이 그 이름을 모르면 false</summary>
		bool Unlock(string platformAchievementId);

		/// <summary>모은 변경을 플랫폼에 저장 (Steam 은 StoreStats)</summary>
		void Flush();
	}
}
