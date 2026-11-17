using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>요소 모서리 근처에 반짝임이 돌며 피었다 짐</summary>
	[CreateAssetMenu(fileName = "IdleFx_0_Twinkle", menuName = "WM/UI/Idle Effect/Twinkle")]
	public sealed class TwinkleIdleEffect : SingleIdleEffect
	{
		public ParticleShape shape = ParticleShape.Sparkle;
		public float seconds = 0.75f;
		public float size = 44f;
		[Tooltip("크기 배율 범위")]
		public Vector2 sizeJitter = new(0.8f, 1.2f);
		[Tooltip("자리, 모서리에서 안쪽으로 변 길이 비율 범위")]
		public Vector2 cornerRange = new(0.05f, 0.3f);
		[Tooltip("피는 동안 도는 각도")]
		public float turnDegrees = 90f;
		[Tooltip("피는 구간 비율. 나머지는 지는 구간")]
		[Range(0.05f, 0.95f)] public float bloomPart = 0.35f;

		public override void PlayOn(VisualElement overlay, VisualElement target, float delay, Action onDone)
		{
			Rect bound = target.worldBound;
			float starSize = size * UnityEngine.Random.Range(sizeJitter.x, sizeJitter.y);
			float CornerAxis(float edgeLength) => UnityEngine.Random.value < 0.5f
				? edgeLength * UnityEngine.Random.Range(cornerRange.x, cornerRange.y)
				: edgeLength * (1f - UnityEngine.Random.Range(cornerRange.x, cornerRange.y));

			VisualElement host = UIIdleFx.Cover(overlay, target, "idle-twinkle");
			VisualElement star = new() { pickingMode = PickingMode.Ignore };
			star.style.position = Position.Absolute;
			star.style.width = starSize;
			star.style.height = starSize;
			star.style.left = CornerAxis(bound.width) - starSize * 0.5f;
			star.style.top = CornerAxis(bound.height) - starSize * 0.5f;
			star.style.backgroundImage = new StyleBackground(UIFxTextures.Shape(shape));
			star.style.unityBackgroundImageTintColor = color;
			star.style.scale = new Scale(Vector2.zero);
			host.Add(star);

			float spin = turnDegrees * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
			UIIdleFx.Animate(host, seconds, delay, progress =>
			{
				// 빠르게 피고 천천히 짐
				float bloom = progress < bloomPart
					? progress / bloomPart
					: 1f - (progress - bloomPart) / (1f - bloomPart);
				float grown = Mathf.Sin(bloom * Mathf.PI * 0.5f);
				star.style.scale = new Scale(new Vector2(grown, grown));
				star.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(spin * progress, AngleUnit.Degree));
				star.style.opacity = grown;
			}, onDone);
		}
	}
}
