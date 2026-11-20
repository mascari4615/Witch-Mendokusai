using UnityEngine;

namespace WitchMendokusai
{
	// 카드팩 개봉 풀 연출. 처음 FullPlays 번, 그 뒤 RepeatSkin (빠른 연출)
	// 수치 기본값이 풀 연출 박자. 설계 memo wm/design/card-pack-v2-20261120
	[CreateAssetMenu(fileName = "RevealSkin_PackOpenFull", menuName = "WM/UI/" + nameof(PackOpenFullSkin))]
	public sealed class PackOpenFullSkin : PackOpenV2Skin
	{
	}
}
