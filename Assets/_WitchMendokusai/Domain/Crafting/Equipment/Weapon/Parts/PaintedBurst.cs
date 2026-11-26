using UnityEngine;

namespace WitchMendokusai
{
	// 켜지는 순간 그린 텍스처 폭발 하나. 판정 없음. 크기는 오브젝트 배율을 따름. 쓰는 곳: 알리사 그림자 표식과 분신 폭발
	public class PaintedBurst : MonoBehaviour
	{
		[SerializeField] private PaintedFxSet art;
		[SerializeField, Min(0.1f)] private float radius = 2f;
		[SerializeField, Min(0.05f)] private float seconds = 0.4f;
		[SerializeField, Min(0.05f)] private float ringSeconds = 0.28f;
		[SerializeField] private Color tint = Color.white;
		[SerializeField] private Color ringTint = Color.white;
		[SerializeField, Tooltip("바닥에 깔리는 어둠")] private Color shade = new(0f, 0f, 0f, 0.6f);
		[SerializeField, Range(0f, 1f), Tooltip("0 더하기, 1 덮기")] private float cover = 0.8f;

		private void OnEnable() => PaintedBurstLayer.Play(art, new PaintedBurstLayer.Burst
		{
			Position = transform.position,
			Radius = radius * transform.lossyScale.x,
			Seconds = seconds,
			RingSeconds = ringSeconds,
			Tint = tint,
			RingTint = ringTint,
			Shade = shade,
			Cover = cover,
		});
	}
}
