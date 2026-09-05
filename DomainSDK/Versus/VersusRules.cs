using System;

namespace WitchMendokusai
{
	/// <summary> 대결 한 판의 진행 규칙. 전부 인스펙터로 돌리는 노브(하드코딩 0). </summary>
	[Serializable]
	public struct VersusRules
	{
		public int RoundsToWin;           // 이 점수에 먼저 닿으면 매치 승.
		public int CardsOfferedToLoser;   // 진 쪽에게 내미는 후보 장수.
		public float RoundTimeLimitSeconds; // 0 이하 = 무제한. 서로 못 맞히는 교착 방지.

		/// <summary> v0 기본값 — ROUNDS 의 5선승을 그대로 가져온다(한 판 3~5분). </summary>
		public static VersusRules Default()
		{
			return new VersusRules
			{
				RoundsToWin = 5,
				CardsOfferedToLoser = 3,
				RoundTimeLimitSeconds = 30f,
			};
		}
	}
}
