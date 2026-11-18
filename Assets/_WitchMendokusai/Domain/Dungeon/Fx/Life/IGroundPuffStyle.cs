using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 바닥에 누운 고리 (WM/GroundPuff 셰이더) 한 종류의 값. 곡선의 가로축은 진행도 0 ~ 1
	/// - 등장 먼지 고리 (MonsterSpawnStyle), 처치 고리 (MonsterDeathStyle) 가 같은 구동기 경로를 씀
	/// </summary>
	public interface IGroundPuffStyle
	{
		Material PuffMaterial { get; }
		float PuffSeconds { get; }
		/// <summary> 같은 종류 (등장, 처치) 고리의 동시 상한 </summary>
		int MaxPuffs { get; }
		float PuffRadius { get; }
		AnimationCurve PuffRadiusCurve { get; }
		AnimationCurve PuffHollowCurve { get; }
		AnimationCurve PuffAlphaCurve { get; }
		Color PuffColor { get; }
		float PuffSoftness { get; }
		float PuffGroundOffset { get; }
	}
}
