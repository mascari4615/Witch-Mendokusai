using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>끄기. 기본 칸에 효과가 있어도 이 에셋을 끼운 패널, 창은 쉼 (빈 칸은 기본을 따름)</summary>
	[CreateAssetMenu(fileName = "IdleFx_0_Off", menuName = "WM/UI/Idle Effect/Off")]
	public sealed class OffIdleEffect : UIIdleEffect
	{
		public override UIIdleEffect Pick() => null;

		public override int Play(VisualElement overlay, VisualElement target, List<VisualElement> targets, Action onDone) =>
			throw new InvalidOperationException($"{name}: 끄기 에셋은 그리지 않음");
	}
}
