using UnityEngine;

namespace WitchMendokusai
{
	// 카드팩 개봉 빠른 연출. 반복 레벨업용, 연속 레벨업이면 일괄 공개 길
	// 박자는 에셋 값 (RevealSkin_PackOpenQuick). 설계 memo wm/design/card-pack-v2-20261120
	[CreateAssetMenu(fileName = "RevealSkin_PackOpenQuick", menuName = "WM/UI/" + nameof(PackOpenQuickSkin))]
	public sealed class PackOpenQuickSkin : PackOpenV2Skin
	{
	}
}
