using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>요소 하나에 그리는 효과. WaveIdleEffect 가 여러 칸에 시차를 두고 부를 수 있음</summary>
	public abstract class SingleIdleEffect : UIIdleEffect
	{
		public Color color = new(1f, 0.95f, 0.8f, 1f);

		public override int Play(VisualElement overlay, VisualElement target, List<VisualElement> targets, Action onDone)
		{
			PlayOn(overlay, target, 0f, onDone);
			return 1;
		}

		/// <summary>delay 초 뒤 target 하나에 그리고, 끝나면 onDone 한 번</summary>
		public abstract void PlayOn(VisualElement overlay, VisualElement target, float delay, Action onDone);
	}
}
