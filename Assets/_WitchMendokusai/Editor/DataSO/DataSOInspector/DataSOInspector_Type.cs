using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	[CustomEditor(typeof(UnitStatData), true)]
	[CanEditMultipleObjects]
	public class DataSOInspector_UnitStatData : DataSOInspector
	{
		protected override List<(string, Action)> GetCustomButtons()
		{
			return new List<(string, Action)>
			{
				("Set Sprite Default", () =>
				{
					if (dataSO is UnitStatData unitStatData)
					{
						const string defaultSpritePath = "Assets/_WitchMendokusai/Component/Item/_Common/Sprite/game-icons.net/double-ringed-orb.png";
						Sprite defaultSprite = AssetDatabase.LoadAssetAtPath<Sprite>(defaultSpritePath);
						
						if (defaultSprite != null)
						{
							unitStatData.Sprite = defaultSprite;
							EditorUtility.SetDirty(unitStatData);
							Debug.Log($"Set default sprite for {unitStatData.name}");
						}
						else
						{
							Debug.LogWarning($"Default sprite not found at path: {defaultSpritePath}");
						}
					}
				})
			};
		}
	}

	[CustomEditor(typeof(Stage), true)]
	[CanEditMultipleObjects]
	public class DataSOInspector_Stage : DataSOInspector
	{
		protected override List<(string, Action)> GetCustomButtons()
		{
			return new List<(string, Action)>
			{
				("LoadStage", () =>
				{
					if (dataSO is WorldStage worldStage)
					{
						EditorManager.OpenScene(worldStage);
					}
				})
			};
		}
	}

	[CustomEditor(typeof(UpgradeData), true)]
	[CanEditMultipleObjects]
	public class DataSOInspector_UpgradeData : DataSOInspector
	{
		protected override List<(string, Action)> GetCustomButtons()
		{
			return new List<(string, Action)>
			{
				("Set UnitStat Sprite", () => { SetUnitStatSprite(dataSO as UpgradeData); }),
				("Set UnitStat Sprite All", () => { SetUnitStatSpriteAll(); }),
			};
		}

		private void SetUnitStatSprite(UpgradeData upgradeData)
		{
			UnitStatType unitStatType = upgradeData.UnitStatType;
			DataSO unitStatData = DataSOWindow.Instance.DataSOs[typeof(UnitStatData)].TryGetValue((int)unitStatType, out DataSO dataSO)
				? dataSO
				: null;

			if (unitStatData == null)
			{
				Debug.LogWarning($"UnitStatData not found for UnitStatType: {unitStatType}");
				return;
			}

			upgradeData.Sprite = unitStatData.Sprite;
			EditorUtility.SetDirty(upgradeData);

			Debug.Log($"Set Sprite of {upgradeData.name} to Sprite of {unitStatData.name}");
		}

		private void SetUnitStatSpriteAll()
		{
			var upgradeDatas = DataSOWindow.Instance.DataSOs[typeof(UpgradeData)].Values;
			foreach (var dataSO in upgradeDatas)
			{
				SetUnitStatSprite(dataSO as UpgradeData);
			}
		}
	}
}