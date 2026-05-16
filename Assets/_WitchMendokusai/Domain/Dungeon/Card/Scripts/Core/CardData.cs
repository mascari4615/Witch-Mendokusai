using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4a — 순수 데이터 SO. 효과 dispatch 는 CardBuffer 의 DI IEffectRunner 책임
	// (구 OnEquip()=static Effect.ApplyEffects 우회 폐기, OnRemove()=no-op 였음 — 둘 다 제거).
	[CreateAssetMenu(fileName = "C_", menuName = "WM/Variable/" + nameof(CardData))]
	public class CardData : DataSO
	{
		[field: Header("_" + nameof(CardData))]
		[field: SerializeField] public List<EffectInfo> Effects { get; private set; }
		[field: SerializeField] public int MaxStack { get; private set; } = 5;
	}
}