namespace WitchMendokusai
{
	/// <summary>
	/// 싸우는 한 명을 움직이는 「손」 (TASK-WM-411). 사람 손·봇 손·<b>네트워크 너머의 손</b>이 같은 구멍에 꽂힌다 —
	/// 판정(<see cref="VersusRoundState"/>)은 이 프레임 하나만 받으므로 누가 보냈는지 모른다.
	/// 2컴 대전에서 네트워크로 흐르는 것도 정확히 이 <see cref="VersusInputFrame"/> 이다(위치가 아니라 의도).
	/// </summary>
	public interface IVersusInput
	{
		/// <summary> 이번 틱의 의도. 봇은 판을 보고 만들고, 사람은 장치에서 읽는다. </summary>
		VersusInputFrame Read(VersusRoundState state, int selfIndex, float deltaTime);
	}
}
