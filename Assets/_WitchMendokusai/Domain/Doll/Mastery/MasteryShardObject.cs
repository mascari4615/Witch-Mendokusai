using UnityEngine;

namespace WitchMendokusai
{
	// 숙련 조각. 경험치 구슬처럼 줍기 범위에 닿으면 끌려와 바로 획득 (사용자 2026-11-22 22:26)
	// 획득하면 막대 스탯 (고유 효과) 과 막대 진행 (확정 변신). 처리는 DollMasteryRun.Pickup
	public class MasteryShardObject : LootObject
	{
		[SerializeField] private Transform shardTransform;
		[SerializeField, Tooltip("초당 회전 각도")] private float spinSpeed = 160f;
		[SerializeField, Tooltip("위아래 흔들림 높이")] private float bobHeight = .12f;
		[SerializeField, Tooltip("초당 위아래 흔들림 횟수")] private float bobFrequency = 1.1f;
		[SerializeField, Tooltip("기울기 (도). 회전할 때 빛을 받게")] private float tiltDegrees = 20f;
		[SerializeField] private UiSfxEvent pickupSfx = UiSfxEvent.Purchase;

		private int track;
		private float phase;

		public void Init(int masteryTrack)
		{
			track = masteryTrack;
			phase = Random.value;
		}

		private void Update()
		{
			float time = Time.time + phase;
			shardTransform.localRotation = Quaternion.Euler(tiltDegrees, time * spinSpeed, 0f);
			shardTransform.localPosition = Vector3.up * Mathf.Sin(time * bobFrequency * Mathf.PI * 2f) * bobHeight;
		}

		protected override void OnEffect()
		{
			SurvivalFeel feel = SurvivalFeel.Instance;
			if (feel == null || feel.TakeSound(nameof(MasteryShardObject)))
				UiSfx.Play(pickupSfx);
			DollMasteryRun.Pickup(track, playerProvider.CurrentObject);
		}
	}
}
