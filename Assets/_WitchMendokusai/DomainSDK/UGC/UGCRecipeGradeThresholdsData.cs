using System;

namespace WitchMendokusai.DomainSDK.UGC
{
	/// <summary>
	/// 레시피 페이지가 정의하는 등급 경계 = BrewOutcomeRules 정합의 UGC 표면.
	/// 거리·부작용 = 목표점에서 멀어질수록 / 부작용 누적이 많을수록 등급 강등.
	/// Default 수치는 placeholder — 본격 진입 시 게임 BrewOutcomeRules 와 매핑.
	/// </summary>
	[Serializable]
	public class UGCRecipeGradeThresholdsData
	{
		public float crudeMaxDistance;
		public float fineMaxDistance;
		public float masterworkMaxDistance;
		public float masterworkMaxSideEffect;
	}
}
