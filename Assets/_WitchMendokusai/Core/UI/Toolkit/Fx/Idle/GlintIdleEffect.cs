using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>요소 위를 비스듬한 빛 띠가 한 번 훑음. 크기는 한 번 정하고 이동은 translate</summary>
	[CreateAssetMenu(fileName = "IdleFx_0_Glint", menuName = "WM/UI/Idle Effect/Glint")]
	public sealed class GlintIdleEffect : SingleIdleEffect
	{
		public float seconds = 0.55f;
		[Range(0f, 1f)] public float alpha = 0.55f;
		[Tooltip("빛 띠 너비, 요소의 긴 변 대비")]
		public float widthRatio = 0.35f;
		public float angle = 18f;
		[Tooltip("빛 띠가 요소 밖에서 출발하고 밖에서 끝나는 거리, 띠 너비 배수 (출발, 끝)")]
		public Vector2 travel = new(1.5f, 0.5f);
		[Tooltip("기울인 빛 띠 길이, 요소 높이 대비. 모서리까지 덮도록 1 보다 크게")]
		public float length = 1.6f;

		public override void PlayOn(VisualElement overlay, VisualElement target, float delay, Action onDone)
		{
			Rect bound = target.worldBound;
			VisualElement clip = UIIdleFx.Cover(overlay, target, "idle-glint");
			clip.style.overflow = Overflow.Hidden;

			float width = Mathf.Max(bound.width, bound.height) * widthRatio;
			float height = (bound.height + width) * length;
			VisualElement band = new() { pickingMode = PickingMode.Ignore };
			band.style.position = Position.Absolute;
			band.style.width = width;
			band.style.height = height;
			band.style.left = 0f;
			band.style.top = (bound.height - height) * 0.5f;
			band.style.backgroundImage = new StyleBackground(UIFxTextures.Band);
			Color tint = color;
			tint.a *= alpha;
			band.style.unityBackgroundImageTintColor = tint;
			band.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(angle, AngleUnit.Degree));
			band.style.opacity = 0f;
			clip.Add(band);

			float from = -width * travel.x;
			float to = bound.width + width * travel.y;
			UIIdleFx.Animate(clip, seconds, delay, progress =>
			{
				float eased = progress * progress * (3f - 2f * progress);
				band.style.translate = new Translate(Mathf.Lerp(from, to, eased), 0f);
				band.style.opacity = Mathf.Sin(progress * Mathf.PI);
			}, onDone);
		}
	}
}
