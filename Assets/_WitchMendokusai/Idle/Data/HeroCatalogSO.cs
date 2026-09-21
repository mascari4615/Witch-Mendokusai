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

		[Tooltip("새 판이 처음부터 가진 인형. 첫 번째가 대표. 비우면 0번 하나 (사용자 결정 2026-09-21: 욘, 링, 알리사)")]
		[SerializeField] private List<HeroDefinitionSO> starters = new List<HeroDefinitionSO>();

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

			List<int> starterIds = new List<int>();
			for (int index = 0; index < starters.Count; index++)
			{
				if (starters[index] != null)
				{
					starterIds.Add(starters[index].ID);
				}
			}

			return new IdleHeroCatalog(definitions, starterIds.Count > 0 ? starterIds : null);
		}

		public Sprite SpriteOf(int id)
		{
			return id >= 0 && id < heroes.Count && heroes[id] != null ? heroes[id].Sprite : null;
		}

		/// <summary>카드와 자리 아이콘용 얼굴</summary>
		public Sprite FaceOf(int id)
		{
			return id >= 0 && id < heroes.Count && heroes[id] != null ? heroes[id].Face : null;
		}

		public bool TryValidate(out string error)
		{
			if (heroes.Count == 0)
			{
				error = "heroes must not be empty";
				return false;
			}

			for (int index = 0; index < heroes.Count; index++)
			{
				HeroDefinitionSO hero = heroes[index];
				if (hero == null)
				{
					error = "heroes contains an empty entry at " + index;
					return false;
				}

				if (hero.ID != index)
				{
					error = "hero ID must match its catalog index at " + index;
					return false;
				}

				if (string.IsNullOrWhiteSpace(hero.Name))
				{
					error = "hero name must not be empty at " + index;
					return false;
				}

				if (hero.Sprite == null)
				{
					error = "hero portrait must not be empty at " + index;
					return false;
				}
			}

			error = string.Empty;
			return true;
		}
	}
}
