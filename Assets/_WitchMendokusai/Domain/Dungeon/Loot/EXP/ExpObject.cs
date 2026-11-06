using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public class ExpObject : LootObject
	{
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		private static readonly int LEGACY_COLOR_ID = Shader.PropertyToID("_Color");
		private static readonly int EMISSION_COLOR_ID = Shader.PropertyToID("_EmissionColor");

		[SerializeField] private int amount;
		private int value;
		private List<ExpObject> registry;
		private MeshRenderer meshRenderer;
		private Vector3 baseMeshScale;
		private MaterialPropertyBlock properties;

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
			if (meshRenderer == null)
			{
				meshRenderer = GetComponentInChildren<MeshRenderer>(true);
				baseMeshScale = meshRenderer.transform.localScale;
				properties = new MaterialPropertyBlock();
			}
			ExpGemTier tier = config.ExpGemTiers[0];
			foreach (ExpGemTier candidate in config.ExpGemTiers)
				if (value >= candidate.MinValue)
					tier = candidate;
			meshRenderer.transform.localScale = baseMeshScale * tier.Scale;
			meshRenderer.GetPropertyBlock(properties);
			properties.SetColor(BASE_COLOR_ID, tier.Color);
			properties.SetColor(LEGACY_COLOR_ID, tier.Color);
			properties.SetColor(EMISSION_COLOR_ID, tier.Color * tier.Emission);
			meshRenderer.SetPropertyBlock(properties);
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
			float gain = value * PlayerStat.PercentMultiplier(UnitStatType.EXP_BONUS);
			int wholeGain = Mathf.FloorToInt(gain);
			return Random.value < gain - wholeGain ? wholeGain + 1 : wholeGain;
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			registry?.Remove(this);
		}
	}
}
