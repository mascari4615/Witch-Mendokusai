using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>같은 줄의 칸들에 끼운 효과를 차례로. 빛 띠, 반짝임, 테두리 어느 것이든</summary>
	[CreateAssetMenu(fileName = "IdleFx_0_Wave", menuName = "WM/UI/Idle Effect/Wave")]
	public sealed class WaveIdleEffect : UIIdleEffect
	{
		public SingleIdleEffect effect;
		[Tooltip("훑는 칸 수 상한")]
		[Min(1)] public int maxItems = 8;
		[Tooltip("칸 사이 시작 간격, 초")]
		public float stepSeconds = 0.07f;

		public override int Play(VisualElement overlay, VisualElement target, List<VisualElement> targets, Action onDone)
		{
			if (effect == null)
				throw new InvalidOperationException($"{name}: 효과 칸이 비어 있음");
			List<VisualElement> row = UIIdleFx.SameRow(target, targets);
			int count = Mathf.Min(row.Count, maxItems);
			for (int i = 0; i < count; i++)
				effect.PlayOn(overlay, row[i], stepSeconds * i, onDone);
			return count;
		}
	}
}
