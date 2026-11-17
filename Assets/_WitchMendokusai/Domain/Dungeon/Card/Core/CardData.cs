using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum CardRarity
	{
		Common = 0,
		Rare = 1,
		Epic = 2,
	}

	// TASK-WM-107 Slice 3-4a — 순수 데이터 SO. 효과 dispatch 는 CardBuffer 의 DI IEffectRunner 책임
	// (구 OnEquip()=static Effect.ApplyEffects 우회 폐기, OnRemove()=no-op 였음 — 둘 다 제거).
	[CreateAssetMenu(fileName = "C_", menuName = "WM/Variable/" + nameof(CardData))]
	public class CardData : DataSO
	{
		[System.Serializable]
		public struct Requirement
		{
			public CardData Card;
			[Min(1)] public int Stack;
		}

		[field: Header("_" + nameof(CardData))]
		[field: SerializeField] public List<EffectInfo> Effects { get; private set; } = new();
		[field: SerializeField] public int MaxStack { get; private set; } = 5;
		[field: SerializeField] public CardRarity Rarity { get; private set; } = CardRarity.Common;
		[field: SerializeField] public List<Requirement> Requirements { get; private set; } = new();
		[field: SerializeField] public CardAwakeningEffect AwakeningEffectPrefab { get; private set; }
		[field: SerializeField] public SurvivalUIKind UIKind { get; private set; } = SurvivalUIKind.Count;

		// 트리 파일이 정의한 카드. 효과 적용 경로는 자산 카드와 같음
		public static CardData CreateRuntime(int id, string name, string description, Sprite sprite, List<EffectInfo> effects, int maxStack, CardRarity rarity, SurvivalUIKind kind)
		{
			CardData card = CreateInstance<CardData>();
			card.hideFlags = HideFlags.DontUnloadUnusedAsset;
			card.name = $"C_{id}_{name}";
			card.ID = id;
			card.Name = name;
			card.Description = description;
			card.Sprite = sprite;
			card.Effects = effects;
			card.MaxStack = maxStack;
			card.Rarity = rarity;
			card.UIKind = kind;
			return card;
		}

		// 화면에 보이는 설명. 설명 안의 {0}, {1} 자리를 같은 순서의 효과 값으로 채움
		public string DescriptionText
		{
			get
			{
				int[] values = new int[Effects.Count];
				for (int i = 0; i < Effects.Count; i++)
					values[i] = Effects[i].Value;
				return CardText.Format(Description, values);
			}
		}

		public bool MeetsRequirements(IReadOnlyList<CardData> selectedCards)
		{
			foreach (Requirement requirement in Requirements)
			{
				if (requirement.Card == null || requirement.Stack < 1)
					return false;
				int count = 0;
				foreach (CardData selected in selectedCards)
					if (selected.ID == requirement.Card.ID)
						count++;
				if (count < requirement.Stack)
					return false;
			}
			return true;
		}
	}
}
