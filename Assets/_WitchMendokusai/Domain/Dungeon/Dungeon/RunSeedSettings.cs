using UnityEngine;

namespace WitchMendokusai
{
	// 판 시드 디버그. 켜면 매 판 같은 바닥 장식과 물건 배치
	[CreateAssetMenu(fileName = nameof(RunSeedSettings), menuName = "WM/Variable/" + nameof(RunSeedSettings))]
	public class RunSeedSettings : ScriptableObject
	{
		[Tooltip("켜면 아래 값을 매 판 시드로 씀. 재현 확인용")] public bool UseFixedSeed;
		[Tooltip("고정 시드 값. 시작 로그의 [RunSeed] 값을 그대로 넣으면 그 판 배치 재현")] public int FixedSeed;
	}
}
