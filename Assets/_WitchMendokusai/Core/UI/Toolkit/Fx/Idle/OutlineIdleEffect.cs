using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>테두리 빛. ring 이면 바깥으로 퍼지며 사라짐, 아니면 제자리에서 한 번 숨 쉼</summary>
	[CreateAssetMenu(fileName = "IdleFx_0_Outline", menuName = "WM/UI/Idle Effect/Outline")]
	public sealed class OutlineIdleEffect : SingleIdleEffect
	{
		[Tooltip("켜면 퍼지는 고리, 끄면 제자리 숨 쉬기")]
		public bool ring;
		public float seconds = 0.9f;
		[Tooltip("테두리 두께, 픽셀")]
		public float width = 2f;
		[Tooltip("숨 쉬기 때 안쪽 채움 세기")]
		[Range(0f, 1f)] public float fill = 0.08f;
		[Tooltip("고리가 퍼지는 배율")]
		public float growth = 1.25f;

		public override void PlayOn(VisualElement overlay, VisualElement target, float delay, Action onDone)
		{
			VisualElement outline = UIIdleFx.Cover(overlay, target, ring ? "idle-ring" : "idle-pulse");
			outline.style.borderTopWidth = width;
			outline.style.borderBottomWidth = width;
			outline.style.borderLeftWidth = width;
			outline.style.borderRightWidth = width;
			outline.style.borderTopColor = color;
			outline.style.borderBottomColor = color;
			outline.style.borderLeftColor = color;
			outline.style.borderRightColor = color;
			if (ring == false)
			{
				Color inner = color;
				inner.a *= fill;
				outline.style.backgroundColor = inner;
			}
			outline.style.opacity = 0f;

			UIIdleFx.Animate(outline, seconds, delay, progress =>
			{
				if (ring)
				{
					float eased = 1f - (1f - progress) * (1f - progress);
					float grown = Mathf.Lerp(1f, growth, eased);
					outline.style.scale = new Scale(new Vector2(grown, grown));
					outline.style.opacity = 1f - eased;
					return;
				}
				outline.style.opacity = Mathf.Sin(progress * Mathf.PI);
			}, onDone);
		}
	}
}
