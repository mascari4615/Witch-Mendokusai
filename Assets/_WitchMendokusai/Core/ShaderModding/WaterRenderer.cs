using UnityEngine;

namespace WitchMendokusai
{
	// 셰이더팩 Water slot 의 식별 마커. 호수/바다 GameObject 에 붙인다.
	// WaterSlot 이 FindObjectsByType<WaterRenderer> 로 탐지 후 MeshRenderer.sharedMaterial 교체.
	[RequireComponent(typeof(MeshRenderer))]
	public class WaterRenderer : MonoBehaviour
	{
	}
}
