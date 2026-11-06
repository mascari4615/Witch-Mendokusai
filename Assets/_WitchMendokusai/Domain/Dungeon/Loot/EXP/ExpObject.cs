using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public class ExpObject : LootObject
	{
		[SerializeField] private int amount;
		[SerializeField] private SpriteRenderer spriteRenderer;
		private int value;
		private List<ExpObject> registry;
		private Vector3 baseSpriteScale;
		private bool hasBaseSpriteScale;

		public int BaseAmount => amount;
		public int Value => value;

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public void SetValue(int newValue, SurvivalFeelConfig config, List<ExpObject> activeGems)
		{
			value = newValue;
			if (registry == null)
				registry = activeGems;
			registry.Add(this);
			ApplyTier(config);
		}

		public void AddValue(int addedValue, SurvivalFeelConfig config)
		{
			value += addedValue;
			ApplyTier(config);
		}

		private void ApplyTier(SurvivalFeelConfig config)
		{
			if (hasBaseSpriteScale == false)
			{
				baseSpriteScale = spriteRenderer.transform.localScale;
				hasBaseSpriteScale = true;
			}
			ExpGemTier tier = config.ExpGemTiers[0];
			foreach (ExpGemTier candidate in config.ExpGemTiers)
				if (value >= candidate.MinValue)
					tier = candidate;
			spriteRenderer.transform.localScale = baseSpriteScale * tier.Scale;
			spriteRenderer.color = tier.Color;
		}

		protected override void OnEffect()
		{
			SurvivalFeel feel = SurvivalFeel.Instance;
			if (feel == null || feel.TakeExpSound())
				RuntimeManager.PlayOneShot("event:/SFX/EXP", transform.position);
			if (feel != null && DungeonManagerBridge.IsDungeon)
				feel.OnExpPicked(transform.position);
			PlayerStat[UnitStatType.EXP_CUR] += value;
		}

		private void LateUpdate()
		{
			spriteRenderer.transform.rotation = Camera.main.transform.rotation;
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			registry?.Remove(this);
		}
	}
}
