using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 카메라를 위로 비켜 세우는 땅 표시. 언덕 지형처럼 카메라 자리를 덮을 수 있는 충돌체에 붙임.
	/// CinemachineComponentDeoccluder 가 카메라 아래 이 땅 높이를 재서 카메라를 그 위로 올림.
	/// 지붕과 천장에는 붙이지 않음 (실내 카메라가 지붕 위로 튐).
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class CameraLiftGround : MonoBehaviour
	{
	}
}
