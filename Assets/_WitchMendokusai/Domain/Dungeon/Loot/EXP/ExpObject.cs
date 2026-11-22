using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class ExpObject : LootObject
	{
		// SurvivalFeel 의 같은 소리 프레임 제한 열쇠 (냥 줍기와 따로 셈)
		private const string EXP_SOUND_KEY = "ExpPickup";
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		private static readonly int LEGACY_COLOR_ID = Shader.PropertyToID("_Color");
		private static readonly int EMISSION_COLOR_ID = Shader.PropertyToID("_EmissionColor");
		private static readonly List<ExpObject> motionOrbs = new();
		private static Material sourceMaterial;
		private static Material[] tierMaterials = new Material[0];
		private static int spawnCounter;

		[SerializeField] private int amount;
		private int value;
		private List<ExpObject> registry;
		private MeshRenderer meshRenderer;
		private Transform meshTransform;
		private float tierScale = 1f;
		private float phase;

		public int BaseAmount => amount;
		public int Value => value;

		private UnitStat PlayerStat => playerProvider.Current.UnitStat;

		public void SetValue(int newValue, SurvivalFeelConfig config, List<ExpObject> activeGems)
		{
			value = newValue;
			if (registry == null)
				registry = activeGems;
			registry.Add(this);
			phase = ExpOrbMotion.PhaseOf(spawnCounter++);
			ApplyTier(config);
			ApplyMotion(config, Time.time);
			if (motionOrbs.Contains(this) == false)
				motionOrbs.Add(this);
		}

		public void AddValue(int addedValue, SurvivalFeelConfig config)
		{
			value += addedValue;
			ApplyTier(config);
		}

		// 오브 전체의 회전, 맥동, 떠오름을 한 번에 갱신. SurvivalFeel 이 프레임마다 한 번 호출
		public static void TickMotion(SurvivalFeelConfig config, float time)
		{
			for (int i = 0; i < motionOrbs.Count; i++)
				motionOrbs[i].ApplyMotion(config, time);
		}

		private void ApplyMotion(SurvivalFeelConfig config, float time)
		{
			if (meshTransform == null)
				return;
			meshTransform.localScale = Vector3.one * ExpOrbMotion.Size(config, tierScale, phase, time);
			meshTransform.localRotation = ExpOrbMotion.Rotation(config, phase, time);
			meshTransform.localPosition = new Vector3(0f, ExpOrbMotion.Lift(config, phase, time), 0f);
		}

		// 등급 재질은 등급마다 하나. 같은 재질이라 SRP Batcher 와 인스턴싱이 묶음
		private void ApplyTier(SurvivalFeelConfig config)
		{
			if (meshRenderer == null)
			{
				meshRenderer = GetComponentInChildren<MeshRenderer>(true);
				meshTransform = meshRenderer.transform;
				if (sourceMaterial == null)
					sourceMaterial = meshRenderer.sharedMaterial;
			}
			int tierIndex = ExpOrbMotion.TierIndex(config.ExpGemTiers, value);
			ExpGemTier tier = config.ExpGemTiers[tierIndex];
			tierScale = tier.Scale;
			meshRenderer.sharedMaterial = TierMaterial(tierIndex, tier, config.ExpOrbEmissionScale);
		}

		private static Material TierMaterial(int tierIndex, ExpGemTier tier, float emissionScale)
		{
			if (tierMaterials.Length <= tierIndex)
				System.Array.Resize(ref tierMaterials, tierIndex + 1);
			Material material = tierMaterials[tierIndex];
			if (material == null)
			{
				material = new Material(sourceMaterial) { name = "EXP Tier " + tierIndex, enableInstancing = true };
				tierMaterials[tierIndex] = material;
			}
			material.SetColor(BASE_COLOR_ID, tier.Color);
			material.SetColor(LEGACY_COLOR_ID, tier.Color);
			material.SetColor(EMISSION_COLOR_ID, tier.Color * (tier.Emission * emissionScale));
			return material;
		}

		protected override void OnEffect()
		{
			SurvivalFeel feel = SurvivalFeel.Instance;
			if (feel == null || feel.TakeSound(EXP_SOUND_KEY))
				PlayPickupSound();
			if (feel != null && DungeonManagerBridge.IsDungeon)
				feel.OnExpPicked(transform.position);
			PlayerStat[UnitStatType.EXP_CUR] += RollExpGain();
		}

		// 바 진행에 따라 음높이를 올려 재생. 제한에 걸려 건너뛴 줍기도 진행은 오르므로 다음 소리가 자연히 높음
		private void PlayPickupSound()
		{
			SurvivalFeelConfig config = resourceManager.SurvivalFeel;
			float progress = ExpPickupPitch.Progress(PlayerStat[UnitStatType.EXP_CUR], PlayerStat[UnitStatType.EXP_MAX]);
			float pitch = ExpPickupPitch.Multiplier(progress, config.ExpPitchSemitones, config.ExpPitchScale);
			float volume = ExpPickupPitch.LevelVolume(PlayerStat[UnitStatType.LEVEL_CUR], config.ExpVolumeDbPerLevel, config.ExpVolumeMinDb);
			CombatSfx.Play(CombatSfxEvent.ExpPickup, transform.position, pitch, volume);
		}

		// 보석 값이 작아 소수점을 버리면 보너스가 사라짐, 남는 소수점은 그 확률로 1 추가
		private int RollExpGain()
		{
			SurvivalRunRules rules = resourceManager.SurvivalRunRules;
			double baseGain = value * (double)PlayerStat.PercentMultiplier(UnitStatType.EXP_BONUS);
			double gain = ExpCurveRules.CalculateGain(rules.ExpCurveBands, rules.ExpBaseRequired, rules.ExpBarrierGainMultiplier,
				PlayerStat[UnitStatType.LEVEL_CUR], PlayerStat[UnitStatType.EXP_CUR], baseGain);
			int wholeGain = (int)System.Math.Floor(gain);
			return Random.value < gain - wholeGain ? wholeGain + 1 : wholeGain;
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			registry?.Remove(this);
			motionOrbs.Remove(this);
		}
	}
}
