using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using VContainer;

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

		[Inject] internal ResourceManager resourceManager;

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
			PlayerStat[UnitStatType.EXP_CUR] += RollExpGain();
		}

		// 보석 값이 작아 소수점을 버리면 보너스가 사라짐, 남는 소수점은 그 확률로 1 추가
		private int RollExpGain()
		{
			SurvivalRunRules rules = resourceManager.SurvivalRunRules;
			float barrierMultiplier = ExpCurveRules.GainMultiplier(rules.ExpCurveBands, PlayerStat[UnitStatType.LEVEL_CUR], rules.ExpBarrierGainMultiplier);
			float gain = value * PlayerStat.PercentMultiplier(UnitStatType.EXP_BONUS) * barrierMultiplier;
			int wholeGain = Mathf.FloorToInt(gain);
			return Random.value < gain - wholeGain ? wholeGain + 1 : wholeGain;
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
