using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 전투 카메라를 잠깐 당겼다 푸는 연출 (CameraManager.PunchZoom 을 에셋 값으로 부름)
	/// - 짧게 치고 빠지기: 들어오는 시간을 짧게, 머무는 시간 0
	/// - 천천히 당기기: 들어오는 시간을 길게, 멈출 때까지 머묾
	/// </summary>
	[CreateAssetMenu(fileName = "Zoom_0_New", menuName = "WM/CombatFx/" + nameof(CameraZoomFx))]
	public class CameraZoomFx : CombatFx
	{
		[SerializeField, Min(0.1f), Tooltip("가장 당겼을 때 카메라 거리 배율. 1 보다 작으면 가까이, 크면 멀리")] private float distanceScale = 0.9f;
		[SerializeField, Tooltip("가장 당겼을 때 시야각에 더하는 값 (도). 음수면 좁아져 당겨 보임")] private float fieldOfViewOffset = -3f;
		[SerializeField, Tooltip("들어오는 시간, 머무는 시간, 나가는 시간, 곡선")] private FlowEnvelope envelope = new();

		protected override Action Play(CombatFxRunner runner, CombatFxContext context) =>
			CameraManager.Instance.PunchZoom(distanceScale, fieldOfViewOffset, envelope.InSeconds, envelope.HoldSeconds, envelope.OutSeconds, envelope.Curve);
	}
}
