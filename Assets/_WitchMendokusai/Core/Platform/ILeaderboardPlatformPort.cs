using System.Threading.Tasks;

namespace WitchMendokusai
{
	/// <summary>플랫폼 순위표 쓰기 창구. 플랫폼마다 하나 (지금은 Steam). 쓰기만 있고 읽기는 없음</summary>
	public interface ILeaderboardPlatformPort
	{
		/// <summary>순위표에 점수를 올림. 어느 쪽이 좋은 점수인지는 순위표 정렬 설정을 따름. 순위표가 없거나 실패하면 false</summary>
		Task<bool> SubmitBest(string leaderboardName, int score);
	}
}
