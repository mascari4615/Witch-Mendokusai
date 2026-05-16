using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(CardBuffer), menuName = "WM/DataBuffer/" + nameof(CardData))]
	// TASK-WM-107 Slice 3-4a — card 효과 dispatch 를 static Effect 파사드→DI IEffectRunner.
	// runner = DI EffectRunner 가 ctor 에서 push (SO 가 static/매니저 안 앎 = seed 정수).
	public class CardBuffer : DataBufferSO<CardData>
	{
		[SerializeField] private bool applyEffect;

		private IEffectRunner effectRunner;
		public void BindEffectRunner(IEffectRunner effectRunner) => this.effectRunner = effectRunner;

		public override void Add(CardData card)
		{
			base.Add(card);
			if (applyEffect)
				effectRunner.ApplyEffects(card.Effects);
		}

		public override bool Remove(CardData card)
		{
			// 구 CardData.OnRemove() = no-op 였으므로 applyEffect 분기 소멸 (behavior 무변경).
			return base.Remove(card);
		}
	}
}