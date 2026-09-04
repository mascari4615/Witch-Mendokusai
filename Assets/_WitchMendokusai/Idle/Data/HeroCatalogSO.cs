using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.DomainSDK.Idle;

namespace WitchMendokusai.Idle
{
	[CreateAssetMenu(fileName = "IdleHeroCatalog", menuName = "WM/Idle/Hero Catalog")]
	public sealed class HeroCatalogSO : ScriptableObject
	{
		[SerializeField] private List<HeroDefinitionSO> heroes = new List<HeroDefinitionSO>();

		public int Count => heroes.Count;

		public IdleHeroCatalog ToDomain()
		{
			IdleHeroKind[] definitions = new IdleHeroKind[heroes.Count];
			for (int index = 0; index < heroes.Count; index++)
			{
				HeroDefinitionSO hero = heroes[index];
				if (hero == null)
				{
					throw new InvalidOperationException("영웅 카탈로그 " + index + "번 항목이 비었다.");
				}

				definitions[index] = hero.ToDomain();
			}

			return new IdleHeroCatalog(definitions);
		}

		public Sprite SpriteOf(int id)
		{
			return id >= 0 && id < heroes.Count && heroes[id] != null ? heroes[id].Sprite : null;
		}
	}
}
