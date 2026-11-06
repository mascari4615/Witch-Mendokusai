using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WitchMendokusai
{
	/// <summary>
	/// 생존 장비 자산 생성기. 같은 경로에 다시 저장해 GUID 유지.
	/// 호출: unity command eval "return WitchMendokusai.SurvivalEquipmentBuilder.Build(4);"
	/// 인자: 만들 최대 차수. 0 기존 장비 상점, 1 첫 묶음, 2 2차, 3 3차, 4 4차, 5 추가 3종.
	/// </summary>
	public static partial class SurvivalEquipmentBuilder
	{
		private const string WEAPON_ROOT = "Assets/_WitchMendokusai/Domain/Crafting/Equipment/Weapon";
		private const string CARD_ROOT = "Assets/_WitchMendokusai/Domain/Dungeon/Card";
		private const string STAT_ROOT = "Assets/_WitchMendokusai/Domain/Unit/UnitStat";
		private const string ICON_ROOT = "Assets/_WitchMendokusai/Domain/Item/Assets/_Common/Sprite/game-icons.net";
		private const string SHOP_BUFFER = "Assets/_WitchMendokusai/Domain/Item/Assets/_ShopItemDataBuffer.asset";
		private const string BASE_MATERIAL = WEAPON_ROOT + "/Satellite/SkillTemp.mat";
		private const string RESONANCE_EFFECT_SOURCE = CARD_ROOT + "/C_410_SwordResonance.asset";
		private const int PRICE = 50;
		private const int SALE_PRICE = 20;

		private static readonly StringBuilder log = new();
		private static readonly Dictionary<string, CardData> cardsByKey = new();

		public static string Build(int maxTier)
		{
			log.Clear();
			cardsByKey.Clear();
			CheckIsolation();

			AddLegacyEquipmentToShop();
			FixMissingSprites();
			AddLegacyTrails();
			foreach (WeaponSpec spec in CreateSpecs())
			{
				if (spec.Tier > maxTier)
					continue;
				BuildWeapon(spec);
			}
			foreach (WeaponSpec spec in CreateSpecs())
			{
				if (spec.Tier > maxTier)
					continue;
				BuildResonance(spec, maxTier);
			}

			ApplyFeelConfig(CreateSpecs());
			AssetDatabase.SaveAssets();
			SyncAddresses();
			AssetDatabase.Refresh();
			log.AppendLine("done tier " + maxTier);
			return log.ToString();
		}

		private static void CheckIsolation()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode)
				throw new InvalidOperationException("Play 중 실행 금지");
		}

		private static void AddLegacyEquipmentToShop()
		{
			foreach (string fileName in new[] { "I_20000002_정령", "I_20000005_포탑", "I_20000010_큐브", "I_20000011_폭탄", "I_20000006_커피", "I_20000007_작은망치", "I_20000008_기어" })
			{
				EquipmentData equipment = LoadRequired<EquipmentData>(WEAPON_ROOT + "/" + fileName + ".asset");
				AddToShop(equipment);
			}
		}

		private static void AddToShop(EquipmentData equipment)
		{
			ItemDataBuffer buffer = LoadRequired<ItemDataBuffer>(SHOP_BUFFER);
			SerializedObject serialized = new(buffer);
			SerializedProperty items = RequireProperty(serialized, "<InitItems>k__BackingField");
			for (int i = 0; i < items.arraySize; i++)
			{
				if (items.GetArrayElementAtIndex(i).objectReferenceValue == equipment)
					return;
			}
			items.arraySize++;
			items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = equipment;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			log.AppendLine("shop + " + equipment.name);
		}

		private static void BuildWeapon(WeaponSpec spec)
		{
			string folder = WEAPON_ROOT + "/" + spec.Folder;
			EnsureFolder(folder);

			Sprite icon = LoadIcon(spec.Icon);
			GameObject weaponObject = spec.BuildObject(spec, folder);

			List<CardData> cards = new();
			int cardIndex = 0;
			foreach (CardSpec cardSpec in spec.Cards)
			{
				bool absolute = cardSpec.StatSuffix.StartsWith("!", StringComparison.Ordinal);
				string statName = absolute ? cardSpec.StatSuffix.Substring(1) : spec.Prefix + "_" + cardSpec.StatSuffix;
				UnitStatData stat = EnsureStat(statName, icon);
				string fileSuffix = absolute ? cardSpec.StatSuffix.Substring(1) : cardSpec.StatSuffix;
				CardData card = EnsureCard(spec.CardBase + cardIndex, spec.Key + fileSuffix, spec.Name + " " + cardSpec.Label, spec.Name + " " + cardSpec.Label + " +", icon,
					new[] { new EffectSpec(stat, cardSpec.Value) }, cardSpec.MaxStack, 0, cardSpec.UIKind, null, null);
				cards.Add(card);
				cardsByKey[statName] = card;
				cardIndex++;
			}

			if (spec.Prefix != null)
			{
				EnsureStat(spec.Prefix + "_COUNT", icon);
				EnsureStat(spec.Prefix + "_DAMAGE_BONUS", icon);
				EnsureStat(spec.Prefix + "_ATTACK_SPEED_BONUS", icon);
				EnsureStat(spec.Prefix + "_SCALE_BONUS", icon);
			}

			string assetPath = WEAPON_ROOT + "/I_" + spec.Id + "_" + spec.Name.Replace(" ", "") + ".asset";
			EquipmentData equipment = LoadOrCreate<EquipmentData>(assetPath);
			SerializedObject serialized = new(equipment);
			Set(serialized, "<ID>k__BackingField", spec.Id);
			Set(serialized, "<Name>k__BackingField", spec.Name);
			Set(serialized, "<Description>k__BackingField", spec.Description);
			Set(serialized, "<Sprite>k__BackingField", icon);
			Set(serialized, "<Type>k__BackingField", 2);
			Set(serialized, "<MaxAmount>k__BackingField", 1);
			Set(serialized, "<PurchasePrice>k__BackingField", PRICE);
			Set(serialized, "<SalePrice>k__BackingField", SALE_PRICE);
			Set(serialized, "<Object>k__BackingField", weaponObject);
			Set(serialized, "<UIKind>k__BackingField", (int)SurvivalUIKind.Damage);
			SerializedProperty effectCards = RequireProperty(serialized, "<EffectCards>k__BackingField");
			effectCards.arraySize = cards.Count;
			for (int i = 0; i < cards.Count; i++)
				effectCards.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
			if (spec.ExtraEffects != null)
			{
				SerializedProperty effects = RequireProperty(serialized, "<Effects>k__BackingField");
				effects.arraySize = spec.ExtraEffects.Length;
				for (int i = 0; i < spec.ExtraEffects.Length; i++)
				{
					SerializedProperty element = effects.GetArrayElementAtIndex(i);
					element.FindPropertyRelative("Type").intValue = 0;
					element.FindPropertyRelative("Data").objectReferenceValue = EnsureStat(spec.ExtraEffects[i].StatName, icon);
					element.FindPropertyRelative("ArithmeticOperator").intValue = 1;
					element.FindPropertyRelative("Value").intValue = spec.ExtraEffects[i].Value;
				}
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(equipment);

			AddToShop(equipment);
			log.AppendLine("weapon " + spec.Id + " " + spec.Name);
		}

		private static void BuildResonance(WeaponSpec spec, int maxTier)
		{
			if (spec.Resonance == null)
				return;
			ResonanceSpec resonance = spec.Resonance;

			List<CardData.Requirement> requirements = new();
			foreach (RequirementSpec requirementSpec in resonance.Requirements)
			{
				CardData required = ResolveRequirement(requirementSpec.Key);
				if (required == null)
				{
					log.AppendLine("resonance skipped (card missing until later tier) " + spec.Name + " <- " + requirementSpec.Key);
					return;
				}
				requirements.Add(new CardData.Requirement { Card = required, Stack = requirementSpec.Stack });
			}

			List<EffectSpec> effects = new();
			Sprite icon = LoadIcon(spec.Icon);
			foreach (ResonanceEffect effect in resonance.Effects)
				effects.Add(new EffectSpec(EnsureStat(effect.StatName, icon), effect.Value));

			CardAwakeningEffect awakeningEffect = LoadRequired<CardData>(RESONANCE_EFFECT_SOURCE).AwakeningEffectPrefab;
			EnsureCard(spec.CardBase + 9, spec.Key + "Resonance", resonance.Name, resonance.Description, icon, effects.ToArray(), 1, (int)CardRarity.Epic, SurvivalUIKind.Advanced, requirements, awakeningEffect, spec);
		}

		private static CardData ResolveRequirement(string key)
		{
			if (key.StartsWith("C_", StringComparison.Ordinal))
				return LoadCardByPrefix(key);
			return cardsByKey.TryGetValue(key, out CardData card) ? card : null;
		}

		private static CardData LoadCardByPrefix(string prefix)
		{
			foreach (string guid in AssetDatabase.FindAssets(prefix + "_ t:CardData", new[] { CARD_ROOT }))
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (Path.GetFileName(path).StartsWith(prefix + "_", StringComparison.Ordinal))
					return AssetDatabase.LoadAssetAtPath<CardData>(path);
			}
			throw new InvalidOperationException("카드 없음 " + prefix);
		}

		private static UnitStatData EnsureStat(string statName, Sprite icon)
		{
			UnitStatType statType = (UnitStatType)Enum.Parse(typeof(UnitStatType), statName);
			int id = (int)statType;
			UnitStatData stat = LoadOrCreate<UnitStatData>(STAT_ROOT + "/USD_" + id + "_" + statName + ".asset");
			SerializedObject serialized = new(stat);
			Set(serialized, "<ID>k__BackingField", id);
			Set(serialized, "<Name>k__BackingField", statName);
			Set(serialized, "<Sprite>k__BackingField", icon);
			Set(serialized, "<Type>k__BackingField", id);
			serialized.ApplyModifiedPropertiesWithoutUndo();
			return stat;
		}

		private static CardData EnsureCard(int id, string fileKey, string cardName, string description, Sprite icon, EffectSpec[] effects, int maxStack, int rarity, SurvivalUIKind uiKind, List<CardData.Requirement> requirements, CardAwakeningEffect awakeningEffect, WeaponSpec owner = null)
		{
			CardData card = LoadOrCreate<CardData>(CARD_ROOT + "/C_" + id + "_" + fileKey + ".asset");
			SerializedObject serialized = new(card);
			Set(serialized, "<ID>k__BackingField", id);
			Set(serialized, "<Name>k__BackingField", cardName);
			Set(serialized, "<Description>k__BackingField", description);
			Set(serialized, "<Sprite>k__BackingField", icon);
			Set(serialized, "<MaxStack>k__BackingField", maxStack);
			Set(serialized, "<Rarity>k__BackingField", rarity);
			Set(serialized, "<UIKind>k__BackingField", (int)uiKind);
			Set(serialized, "<AwakeningEffectPrefab>k__BackingField", awakeningEffect);

			SerializedProperty effectsProperty = RequireProperty(serialized, "<Effects>k__BackingField");
			effectsProperty.arraySize = effects.Length;
			for (int i = 0; i < effects.Length; i++)
			{
				SerializedProperty element = effectsProperty.GetArrayElementAtIndex(i);
				element.FindPropertyRelative("Type").intValue = 0;
				element.FindPropertyRelative("Data").objectReferenceValue = effects[i].Stat;
				element.FindPropertyRelative("ArithmeticOperator").intValue = 1;
				element.FindPropertyRelative("Value").intValue = effects[i].Value;
			}

			SerializedProperty requirementsProperty = RequireProperty(serialized, "<Requirements>k__BackingField");
			int requirementCount = requirements != null ? requirements.Count : 0;
			requirementsProperty.arraySize = requirementCount;
			for (int i = 0; i < requirementCount; i++)
			{
				SerializedProperty element = requirementsProperty.GetArrayElementAtIndex(i);
				element.FindPropertyRelative("Card").objectReferenceValue = requirements[i].Card;
				element.FindPropertyRelative("Stack").intValue = requirements[i].Stack;
			}
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(card);

			if (owner != null)
				AttachCardToEquipment(owner, card);
			return card;
		}

		private static void AttachCardToEquipment(WeaponSpec owner, CardData card)
		{
			EquipmentData equipment = LoadRequired<EquipmentData>(WEAPON_ROOT + "/I_" + owner.Id + "_" + owner.Name.Replace(" ", "") + ".asset");
			SerializedObject serialized = new(equipment);
			SerializedProperty effectCards = RequireProperty(serialized, "<EffectCards>k__BackingField");
			for (int i = 0; i < effectCards.arraySize; i++)
			{
				if (effectCards.GetArrayElementAtIndex(i).objectReferenceValue == card)
					return;
			}
			effectCards.arraySize++;
			effectCards.GetArrayElementAtIndex(effectCards.arraySize - 1).objectReferenceValue = card;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static Sprite LoadIcon(string iconName)
		{
			string path = iconName.StartsWith("Assets/", StringComparison.Ordinal) ? iconName : ICON_ROOT + "/" + iconName + ".png";
			if (path.EndsWith(".asset", StringComparison.Ordinal))
				return LoadRequired<ItemData>(path).Sprite;
			return LoadRequired<Sprite>(path);
		}

		private static T LoadRequired<T>(string path) where T : Object
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset == null)
				throw new InvalidOperationException("자산 없음 " + path);
			return asset;
		}

		private static T LoadOrCreate<T>(string path) where T : ScriptableObject
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset != null)
				return asset;
			asset = ScriptableObject.CreateInstance<T>();
			AssetDatabase.CreateAsset(asset, path);
			return asset;
		}

		private static void EnsureFolder(string folder)
		{
			if (AssetDatabase.IsValidFolder(folder))
				return;
			AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
		}

		private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName)
		{
			SerializedProperty property = serialized.FindProperty(propertyName);
			if (property == null)
				throw new InvalidOperationException(serialized.targetObject.GetType().Name + " 속성 없음 " + propertyName);
			return property;
		}

		private static void Set(SerializedObject serialized, string propertyName, int value) => RequireProperty(serialized, propertyName).intValue = value;
		private static void Set(SerializedObject serialized, string propertyName, float value) => RequireProperty(serialized, propertyName).floatValue = value;
		private static void Set(SerializedObject serialized, string propertyName, bool value) => RequireProperty(serialized, propertyName).boolValue = value;
		private static void Set(SerializedObject serialized, string propertyName, string value) => RequireProperty(serialized, propertyName).stringValue = value;
		private static void Set(SerializedObject serialized, string propertyName, Object value) => RequireProperty(serialized, propertyName).objectReferenceValue = value;
	}
}
