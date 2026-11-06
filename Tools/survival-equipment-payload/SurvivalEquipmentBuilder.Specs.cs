using System;
using System.Collections.Generic;
using UnityEngine;
using PrefabUtility = UnityEditor.PrefabUtility;
using PrefabUnpackMode = UnityEditor.PrefabUnpackMode;
using SerializedObject = UnityEditor.SerializedObject;
using Object = UnityEngine.Object;

namespace WitchMendokusai
{
	public static partial class SurvivalEquipmentBuilder
	{
		private sealed class WeaponSpec
		{
			public int Tier;
			public string Key;
			public string Folder;
			public string Prefix;
			public Color HitColor;
			public ResonanceEffect[] ExtraEffects;
			public int Id;
			public int CardBase;
			public string Name;
			public string Description;
			public string Icon;
			public Func<WeaponSpec, string, GameObject> BuildObject;
			public CardSpec[] Cards;
			public ResonanceSpec Resonance;
		}

		private readonly struct CardSpec
		{
			public readonly string StatSuffix;
			public readonly string Label;
			public readonly int Value;
			public readonly int MaxStack;
			public readonly SurvivalUIKind UIKind;

			public CardSpec(string statSuffix, string label, int value, int maxStack, SurvivalUIKind uiKind)
			{
				StatSuffix = statSuffix;
				Label = label;
				Value = value;
				MaxStack = maxStack;
				UIKind = uiKind;
			}
		}

		private readonly struct EffectSpec
		{
			public readonly UnitStatData Stat;
			public readonly int Value;

			public EffectSpec(UnitStatData stat, int value)
			{
				Stat = stat;
				Value = value;
			}
		}

		private readonly struct RequirementSpec
		{
			public readonly string Key;
			public readonly int Stack;

			public RequirementSpec(string key, int stack)
			{
				Key = key;
				Stack = stack;
			}
		}

		private readonly struct ResonanceEffect
		{
			public readonly string StatName;
			public readonly int Value;

			public ResonanceEffect(string statName, int value)
			{
				StatName = statName;
				Value = value;
			}
		}

		private sealed class ResonanceSpec
		{
			public string Name;
			public string Description;
			public RequirementSpec[] Requirements;
			public ResonanceEffect[] Effects;
		}

		private readonly struct DamageSpec
		{
			public readonly int Damage;
			public readonly int HitCount;
			public readonly float RehitInterval;
			public readonly float Knockback;
			public readonly float Hitstop;

			public DamageSpec(int damage, int hitCount = 0, float rehitInterval = 0f, float knockback = 3f, float hitstop = 0f)
			{
				Damage = damage;
				HitCount = hitCount;
				RehitInterval = rehitInterval;
				Knockback = knockback;
				Hitstop = hitstop;
			}
		}

		private static CardSpec[] StandardCards(int damageValue, bool hasCount = true, bool hasSpeed = true, bool hasScale = false, string countLabel = "수")
		{
			List<CardSpec> cards = new();
			if (hasCount)
				cards.Add(new CardSpec("COUNT", countLabel + " 증가", 1, 10, SurvivalUIKind.Count));
			cards.Add(new CardSpec("DAMAGE_BONUS", "데미지 증가", damageValue, 100, SurvivalUIKind.Damage));
			if (hasSpeed)
				cards.Add(new CardSpec("ATTACK_SPEED_BONUS", "공격속도 증가", 1, 5, SurvivalUIKind.Speed));
			if (hasScale)
				cards.Add(new CardSpec("SCALE_BONUS", "크기 증가", 1, 5, SurvivalUIKind.Size));
			return cards.ToArray();
		}

		private static RequirementSpec Req(string key, int stack) => new(key, stack);
		private static ResonanceEffect Fx(string statName, int value) => new(statName, value);

		private static ResonanceSpec Resonance(string resonanceName, string description, RequirementSpec[] requirements, params ResonanceEffect[] effects)
		{
			return new ResonanceSpec { Name = resonanceName, Description = description, Requirements = requirements, Effects = effects };
		}

		private static List<WeaponSpec> CreateSpecs()
		{
			return new List<WeaponSpec>
			{
				new WeaponSpec
				{
					Tier = 1, Key = "Needle", Folder = "Needle", Prefix = "NEEDLE", Id = 20000013, CardBase = 700, HitColor = Muted(0.74f, 0.76f, 0.82f),
					Name = "바늘 쌈지", Description = "조준 방향으로 바늘을 연달아 찌른다.", Icon = "diving-dagger",
					Cards = StandardCards(2, countLabel: "바늘 수"),
					Resonance = Resonance("꿰매는 바늘", "바늘이 맞힌 뒤 주변 적 2명에게 튄다.\n바늘 수 강화 3회와 실타래 튕김 강화 2회 후 등장. 한 판에 1회.", new[] { Req("NEEDLE_COUNT", 3), Req("THREAD_CHAIN", 2) }, Fx("NEEDLE_CHAIN", 2)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject link = MakeChainLink(folder, "NeedleChainLink", color);
						SpriteSpec sprite = new(IconSprite("diving-dagger"), color, 0.6f, flat: true, height: 0.6f, roll: 135f);
						GameObject bullet = MakeBullet(folder, "NeedleBullet", sprite, 20f, false, 1.2f, 0.3f, new DamageSpec(5, hitCount: 1, knockback: 2f), new Color(color.r, color.g, color.b, 0.5f), root => AddChain(root, "NEEDLE_CHAIN", 0, 6f, link));
						return MakeSpawnerObject(spec, folder, bullet, SpawnPositionStrategy.Player, SpawnAimStrategy.Aim, 1.2f, 0.08f, 0f, 3);
					},
				},
				new WeaponSpec
				{
					Tier = 1, Key = "Bolt", Folder = "Bolt", Prefix = "BOLT", Id = 20000014, CardBase = 710, HitColor = Muted(0.82f, 0.74f, 0.36f),
					Name = "귀찮은 벼락", Description = "주변 적 머리 위로 벼락이 떨어진다.", Icon = "electric",
					Cards = StandardCards(4, countLabel: "벼락 수"),
					Resonance = Resonance("귀찮은 뇌우", "벼락 수 +2, 벼락 데미지 +12\n벼락 수 강화 3회와 책갈피 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("BOLT_COUNT", 3), Req("BEAM_DAMAGE_BONUS", 2) }, Fx("BOLT_COUNT", 2), Fx("BOLT_DAMAGE_BONUS", 12)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject strike = MakeBlast(folder, "BoltStrike", color, 1.6f, 0.22f, new DamageSpec(15, knockback: 4f, hitstop: 0.03f), Burst(color, 8, 6f, 0.25f, 0.14f, 0.04f), root => CreateSprite(root, new SpriteSpec(IconSprite("electric"), color, 2.2f, height: 1.2f)));
						return MakeSpawnerObject(spec, folder, strike, SpawnPositionStrategy.RandomEnemy, SpawnAimStrategy.None, 2.5f, 0.05f, 0f, 2);
					},
				},
				new WeaponSpec
				{
					Tier = 1, Key = "Beam", Folder = "Beam", Prefix = "BEAM", Id = 20000015, CardBase = 720, HitColor = Muted(0.42f, 0.6f, 0.76f),
					Name = "책갈피 빔", Description = "가장 가까운 적을 향해 관통 빛줄기를 쏜다.", Icon = "ringed-beam",
					Cards = StandardCards(3, hasCount: true, hasScale: true, countLabel: "빛줄기 수"),
					Resonance = Resonance("펼친 책갈피", "빔 굵기 +2, 빔 데미지 +10\n빔 크기 강화 3회와 페이지 수 강화 2회 후 등장. 한 판에 1회.", new[] { Req("BEAM_SCALE_BONUS", 3), Req("PAGE_COUNT", 2) }, Fx("BEAM_SCALE_BONUS", 2), Fx("BEAM_DAMAGE_BONUS", 10)),
					BuildObject = (spec, folder) =>
					{
						GameObject beam = MakeBeam(folder, "BeamShot", spec.HitColor, 0.9f, 12f, 0.25f, new DamageSpec(12, knockback: 2f, hitstop: 0.02f));
						return MakeSpawnerObject(spec, folder, beam, SpawnPositionStrategy.Player, SpawnAimStrategy.NearestEnemy, 3f, 0.15f, 0f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 1, Key = "Flask", Folder = "Flask", Prefix = "FLASK", Id = 20000016, CardBase = 730, HitColor = Muted(0.42f, 0.64f, 0.44f),
					Name = "빈 플라스크", Description = "주위에 병을 던져 고인 약물이 적을 태운다.", Icon = "Assets/_WitchMendokusai/Domain/Crafting/Equipment/Potion/I_10000000_빈플라스크(소).asset",
					Cards = StandardCards(2, hasScale: true, countLabel: "병 수"),
					Resonance = Resonance("넘치는 플라스크", "병 수 +2, 약물 데미지 +6\n병 수 강화 3회와 사탕 수 강화 2회 후 등장. 한 판에 1회.", new[] { Req("FLASK_COUNT", 3), Req("CANDY_COUNT", 2) }, Fx("FLASK_COUNT", 2), Fx("FLASK_DAMAGE_BONUS", 6)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject field = MakeField(folder, "FlaskField", color, 2f, 2.5f, new DamageSpec(5, rehitInterval: 0.5f, knockback: 0f), root =>
						{
							AddBurst(root, Burst(color, 10, 3f, 0.35f, 0.14f));
							AddAmbient(root, new AmbientSpec(new Color(color.r, color.g, color.b, 0.7f), 6f, 1.8f, 0.8f, 0.14f, 1f));
						});
						SpriteSpec sprite = new(ItemSprite(POTION_ROOT + "/I_10000000_빈플라스크(소).asset"), Color.white, 0.7f);
						GameObject bottle = MakeFlyer(folder, "FlaskBottle", sprite, 0.4f, 2f, 0f, field, new Color(color.r, color.g, color.b, 0.4f));
						return MakeSpawnerObject(spec, folder, bottle, SpawnPositionStrategy.Ring, SpawnAimStrategy.None, 4.5f, 0f, 4f, 3);
					},
				},
				new WeaponSpec
				{
					Tier = 1, Key = "Broom", Folder = "Broom", Prefix = "BROOM", Id = 20000017, CardBase = 740, HitColor = Muted(0.6f, 0.45f, 0.28f),
					Name = "되돌아오는 빗자루", Description = "던진 빗자루가 적을 쓸고 돌아온다.", Icon = "whirlwind",
					Cards = StandardCards(3, hasScale: true, countLabel: "빗자루 수"),
					Resonance = Resonance("돌아오기 귀찮아", "빗자루 크기 +2, 빗자루 데미지 +8\n빗자루 크기 강화 3회와 베개 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("BROOM_SCALE_BONUS", 3), Req("PILLOW_DAMAGE_BONUS", 2) }, Fx("BROOM_SCALE_BONUS", 2), Fx("BROOM_DAMAGE_BONUS", 8)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject root = new("BroomThrown");
						AddBase(root, 4f);
						AddSphereTrigger(root, 0.9f);
						AddDamage(root, new DamageSpec(9, rehitInterval: 0.4f, knockback: 4f));
						GameObject visual = CreateSprite(root, new SpriteSpec(SheetTile("crafting_materials/resources_basic.png", 1, 0, 24), Color.white, 1.5f, flat: true, height: 0.5f));
						AddTrail(visual, folder, new Color(color.r, color.g, color.b, 0.35f), 0.2f, 0.8f);
						SerializedObjectSet(root.AddComponent<ReturningMovement>(), "spinTransform", visual.transform);
						GameObject thrown = SavePrefab(root, folder, "BroomThrown");
						return MakeSpawnerObject(spec, folder, thrown, SpawnPositionStrategy.Player, SpawnAimStrategy.NearestEnemy, 2.5f, 0.2f, 0f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 1, Key = "Pillow", Folder = "Pillow", Prefix = "PILLOW", Id = 20000018, CardBase = 750, HitColor = Muted(0.5f, 0.44f, 0.68f),
					Name = "낮잠 베개", Description = "멈춰 있을수록 주위 졸음 구역이 넓고 강해진다.", Icon = "rear-aura",
					Cards = StandardCards(1, hasCount: false, hasSpeed: false, hasScale: true),
					Resonance = Resonance("이불 밖은 위험해", "베개 크기 +2, 베개 데미지 +5\n베개 크기 강화 3회와 솥 김 크기 강화 2회 후 등장. 한 판에 1회.", new[] { Req("PILLOW_SCALE_BONUS", 3), Req("STEAM_SCALE_BONUS", 2) }, Fx("PILLOW_SCALE_BONUS", 2), Fx("PILLOW_DAMAGE_BONUS", 5)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						return MakeAura(spec, folder, color, 3.5f, new DamageSpec(3, rehitInterval: 0.5f, knockback: 0f), true, 0.06f, 0.35f, new AmbientSpec(new Color(color.r, color.g, color.b, 0.55f), 5f, 3f, 1.4f, 0.16f, 0.5f));
					},
				},

				new WeaponSpec
				{
					Tier = 2, Key = "Pin", Folder = "Pin", Prefix = "PIN", Id = 20000019, CardBase = 760, HitColor = Muted(0.74f, 0.42f, 0.5f),
					Name = "시침핀", Description = "주변에 핀을 꽂아 밟은 적을 터뜨린다.", Icon = "tripwire",
					Cards = StandardCards(4, countLabel: "핀 수"),
					Resonance = Resonance("빽빽한 시침", "핀 수 +2, 핀 데미지 +12\n핀 수 강화 3회와 폭탄 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("PIN_COUNT", 3), Req("C_601", 2) }, Fx("PIN_COUNT", 2), Fx("PIN_DAMAGE_BONUS", 12)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject blast = MakeBlast(folder, "PinBlast", color, 2.2f, 0.22f, new DamageSpec(18, knockback: 8f, hitstop: 0.02f), Burst(color, 10, 6f, 0.28f, 0.14f, 0.05f));
						SpriteSpec sprite = new(IconSprite("tripwire"), color, 0.7f, flat: true, height: 0.1f);
						GameObject mine = MakeMine(folder, "PinMine", sprite, color, 8f, 0.7f, blast);
						return MakeSpawnerObject(spec, folder, mine, SpawnPositionStrategy.Random, SpawnAimStrategy.None, 2.5f, 0.1f, 7f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 2, Key = "Potion", Folder = "Potion", Prefix = "POTION", Id = 20000020, CardBase = 770, HitColor = Muted(0.58f, 0.34f, 0.7f),
					Name = "터지는 물약", Description = "적에게 물약을 던져 터뜨린다.", Icon = "round-potion",
					Cards = StandardCards(4, hasScale: true, countLabel: "물약 수"),
					Resonance = Resonance("불안정한 배합", "물약 수 +2, 물약 데미지 +12\n물약 수 강화 3회와 폭탄 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("POTION_COUNT", 3), Req("C_601", 2) }, Fx("POTION_COUNT", 2), Fx("POTION_DAMAGE_BONUS", 12)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject blast = MakeBlast(folder, "PotionBlast", color, 2.5f, 0.25f, new DamageSpec(16, knockback: 6f, hitstop: 0.02f), Burst(color, 12, 7f, 0.3f, 0.16f, 0.06f));
						SpriteSpec sprite = new(ItemSprite(POTION_ROOT + "/I_10003000_보라빛포션(소).asset"), Color.white, 0.8f);
						GameObject bottle = MakeFlyer(folder, "PotionBottle", sprite, 0.7f, 3f, 0f, blast, new Color(color.r, color.g, color.b, 0.45f));
						return MakeSpawnerObject(spec, folder, bottle, SpawnPositionStrategy.RandomEnemy, SpawnAimStrategy.None, 3.5f, 0.1f, 0f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 2, Key = "Candy", Folder = "Candy", Prefix = "CANDY", Id = 20000021, CardBase = 780, HitColor = Muted(0.8f, 0.48f, 0.6f),
					Name = "사탕 병", Description = "병에서 쏟아진 사탕이 아무 적에게나 날아간다.", Icon = "wrapping-star",
					Cards = StandardCards(1, countLabel: "사탕 수"),
					Resonance = Resonance("쏟아진 사탕", "사탕 수 +3, 사탕 데미지 +4\n사탕 수 강화 3회와 병 수 강화 2회 후 등장. 한 판에 1회.", new[] { Req("CANDY_COUNT", 3), Req("FLASK_COUNT", 2) }, Fx("CANDY_COUNT", 3), Fx("CANDY_DAMAGE_BONUS", 4)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						SpriteSpec sprite = new(IconSprite("wrapping-star"), color, 0.55f);
						GameObject bullet = MakeBullet(folder, "CandyBullet", sprite, 14f, false, 2f, 0.35f, new DamageSpec(4, hitCount: 1, knockback: 1f), new Color(color.r, color.g, color.b, 0.3f));
						return MakeSpawnerObject(spec, folder, bullet, SpawnPositionStrategy.Player, SpawnAimStrategy.RandomEnemy, 0.8f, 0.05f, 0f, 2, spread: 12f);
					},
				},
				new WeaponSpec
				{
					Tier = 2, Key = "Rune", Folder = "Rune", Prefix = "RUNE", Id = 20000022, CardBase = 790, HitColor = Muted(0.34f, 0.62f, 0.58f),
					Name = "룬 쪽지", Description = "쪽지가 화면 끝에서 튕기며 적을 벤다.", Icon = "ricochet",
					Cards = StandardCards(2, countLabel: "쪽지 수"),
					Resonance = Resonance("쉬지 않는 쪽지", "쪽지 수 +2, 쪽지 데미지 +8\n쪽지 수 강화 3회와 빔 크기 강화 2회 후 등장. 한 판에 1회.", new[] { Req("RUNE_COUNT", 3), Req("BEAM_SCALE_BONUS", 2) }, Fx("RUNE_COUNT", 2), Fx("RUNE_DAMAGE_BONUS", 8)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject root = new("RuneNote");
						AddBase(root, 3f);
						AddSphereTrigger(root, 0.6f);
						AddDamage(root, new DamageSpec(6, rehitInterval: 0.5f, knockback: 1f));
						root.AddComponent<BouncingMovement>();
						GameObject visual = CreateSprite(root, new SpriteSpec(IconSprite("abstract-067"), color, 0.9f, flat: true, height: 0.6f));
						AddTrail(visual, folder, new Color(color.r, color.g, color.b, 0.4f), 0.3f, 0.3f);
						GameObject note = SavePrefab(root, folder, "RuneNote");
						return MakeSpawnerObject(spec, folder, note, SpawnPositionStrategy.Player, SpawnAimStrategy.RandomDirection, 3f, 0.15f, 0f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 2, Key = "Steam", Folder = "Steam", Prefix = "STEAM", Id = 20000023, CardBase = 800, HitColor = Muted(0.68f, 0.68f, 0.66f),
					Name = "솥 김", Description = "주위에 뜨거운 김을 둘러 적을 데운다.", Icon = "camp-cooking-pot",
					Cards = StandardCards(1, hasCount: false, hasSpeed: false, hasScale: true),
					Resonance = Resonance("끓어넘친 솥", "김 크기 +2, 김 데미지 +4\n김 크기 강화 3회와 병 공격속도 강화 2회 후 등장. 한 판에 1회.", new[] { Req("STEAM_SCALE_BONUS", 3), Req("FLASK_ATTACK_SPEED_BONUS", 2) }, Fx("STEAM_SCALE_BONUS", 2), Fx("STEAM_DAMAGE_BONUS", 4)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						return MakeAura(spec, folder, color, 3f, new DamageSpec(2, rehitInterval: 0.4f, knockback: 1.5f), false, 0.03f, 0.18f, new AmbientSpec(new Color(color.r, color.g, color.b, 0.5f), 14f, 2.6f, 1.1f, 0.26f, 1.4f));
					},
				},

				new WeaponSpec
				{
					Tier = 3, Key = "Thread", Folder = "Thread", Prefix = "THREAD", Id = 20000024, CardBase = 810, HitColor = Muted(0.78f, 0.52f, 0.58f),
					Name = "실타래", Description = "맞은 적에서 주변 적으로 실이 번진다.", Icon = "wool",
					Cards = new[]
					{
						new CardSpec("CHAIN", "튕김 증가", 1, 8, SurvivalUIKind.Count),
						new CardSpec("DAMAGE_BONUS", "데미지 증가", 2, 100, SurvivalUIKind.Damage),
						new CardSpec("ATTACK_SPEED_BONUS", "공격속도 증가", 1, 5, SurvivalUIKind.Speed),
					},
					Resonance = Resonance("엉킨 실타래", "튕김 +3, 실타래 데미지 +8\n튕김 강화 3회와 공격력 강화 2회 후 등장. 한 판에 1회.", new[] { Req("THREAD_CHAIN", 3), Req("C_4", 2) }, Fx("THREAD_CHAIN", 3), Fx("THREAD_DAMAGE_BONUS", 8)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject link = MakeChainLink(folder, "ThreadChainLink", color);
						SpriteSpec sprite = new(IconSprite("wool"), color, 0.7f, flat: true, height: 0.6f);
						GameObject bullet = MakeBullet(folder, "ThreadBullet", sprite, 16f, false, 1.5f, 0.35f, new DamageSpec(6, hitCount: 1, knockback: 1f), new Color(color.r, color.g, color.b, 0.35f), root => AddChain(root, "THREAD_CHAIN", 2, 6f, link));
						return MakeSpawnerObject(spec, folder, bullet, SpawnPositionStrategy.Player, SpawnAimStrategy.NearestEnemy, 1.6f, 0.1f, 0f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 3, Key = "Ink", Folder = "Ink", Prefix = "INK", Id = 20000025, CardBase = 820, HitColor = Muted(0.26f, 0.22f, 0.46f),
					Name = "잉크 웅덩이", Description = "적 발밑에 잉크를 쏟아 느리게 만든다.", Icon = "water-splash",
					Cards = StandardCards(2, hasScale: true, countLabel: "웅덩이 수"),
					Resonance = Resonance("번진 잉크", "웅덩이 크기 +2, 잉크 데미지 +6\n잉크 크기 강화 3회와 빔 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("INK_SCALE_BONUS", 3), Req("BEAM_DAMAGE_BONUS", 2) }, Fx("INK_SCALE_BONUS", 2), Fx("INK_DAMAGE_BONUS", 6)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject field = MakeField(folder, "InkPool", color, 3f, 3f, new DamageSpec(4, rehitInterval: 0.5f, knockback: 0f), root =>
						{
							AddStatus(root, 0.4f, 0.8f);
							AddBurst(root, Burst(color, 10, 3f, 0.35f, 0.16f));
							AddAmbient(root, new AmbientSpec(new Color(color.r, color.g, color.b, 0.8f), 5f, 2.7f, 0.9f, 0.16f, 0.5f));
						});
						SpriteSpec sprite = new(IconSprite("water-splash"), color, 0.7f);
						GameObject drop = MakeFlyer(folder, "InkDrop", sprite, 0.45f, 2.5f, 0f, field, new Color(color.r, color.g, color.b, 0.5f));
						return MakeSpawnerObject(spec, folder, drop, SpawnPositionStrategy.NearestEnemy, SpawnAimStrategy.None, 5f, 0.2f, 0f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 3, Key = "Page", Folder = "Page", Prefix = "PAGE", Id = 20000026, CardBase = 830, HitColor = Muted(0.78f, 0.72f, 0.54f),
					Name = "떨어진 페이지", Description = "페이지가 주위를 돌고 이따금 낱장이 떨어진다.", Icon = "white-book",
					Cards = StandardCards(2, countLabel: "페이지 수", hasScale: true),
					Resonance = Resonance("흩어진 원고", "페이지 수 +2, 페이지 데미지 +8\n페이지 수 강화 3회와 도깨비불 크기 강화 2회 후 등장. 한 판에 1회.", new[] { Req("PAGE_COUNT", 3), Req("C_103", 2) }, Fx("PAGE_COUNT", 2), Fx("PAGE_DAMAGE_BONUS", 8)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject satelliteRoot = new("PageOrbit");
						satelliteRoot.AddComponent<PoolingObject>();
						GameObject collider = new("Collider");
						collider.transform.SetParent(satelliteRoot.transform, false);
						collider.transform.localPosition = new Vector3(0f, 0f, 3f);
						AddSphereTrigger(collider, 0.8f);
						AddDamage(collider, new DamageSpec(8, rehitInterval: 0.6f, knockback: 3f));
						SpriteSpec orbitSprite = new(IconSprite("white-book"), color, 0.9f);
						CreateSprite(collider, orbitSprite);
						GameObject satellite = SavePrefab(satelliteRoot, folder, "PageOrbit");

						GameObject blast = MakeBlast(folder, "PageBlast", color, 1.5f, 0.18f, new DamageSpec(8, knockback: 2f), Burst(color, 6, 3f, 0.4f, 0.14f));
						GameObject drop = MakeFlyer(folder, "PageDrop", orbitSprite, 0.5f, 0f, 5f, blast, new Color(color.r, color.g, color.b, 0.3f));

						return MakeSpawnerObject(spec, folder, drop, SpawnPositionStrategy.Random, SpawnAimStrategy.None, 3f, 0.1f, 7f, 1, root =>
						{
							SerializedObjectSetHulahoop(root.AddComponent<EquipmentHulahoop>(), satellite, spec);
						});
					},
				},
				new WeaponSpec
				{
					Tier = 3, Key = "Paper", Folder = "Paper", Prefix = "PAPER", Id = 20000027, CardBase = 840, HitColor = Muted(0.74f, 0.7f, 0.58f),
					Name = "종이 인형 편대", Description = "종이 인형이 따라다니며 적에게 작은 폭탄을 던진다.", Icon = "voodoo-doll",
					Cards = StandardCards(2, countLabel: "인형 수"),
					Resonance = Resonance("접고 또 접고", "인형 수 +2, 폭격 데미지 +6\n인형 수 강화 3회와 정령 수 강화 2회 후 등장. 한 판에 1회.", new[] { Req("PAPER_COUNT", 3), Req("C_200", 2) }, Fx("PAPER_COUNT", 2), Fx("PAPER_DAMAGE_BONUS", 6)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject blast = MakeBlast(folder, "PaperBlast", color, 1.5f, 0.18f, new DamageSpec(6, knockback: 2f), Burst(color, 6, 4f, 0.25f, 0.12f));

						GameObject bulletRoot = new("PaperBomb");
						AddBase(bulletRoot, 2.5f);
						AddSphereTrigger(bulletRoot, 0.4f);
						SerializedObject movement = new(bulletRoot.AddComponent<BulletMovement>());
						Set(movement, "moveSpeed", 13f);
						Set(movement, "useAutoAim", true);
						movement.ApplyModifiedPropertiesWithoutUndo();
						SerializedObject explode = new(bulletRoot.AddComponent<ExplodeOnEnd>());
						Set(explode, "explosionPrefab", blast);
						Set(explode, "explodeOnContact", true);
						explode.ApplyModifiedPropertiesWithoutUndo();
						GameObject bulletVisual = CreateSprite(bulletRoot, new SpriteSpec(IconSprite("squib"), color, 0.5f));
						AddTrail(bulletVisual, folder, new Color(color.r, color.g, color.b, 0.35f), 0.15f, 0.2f);
						GameObject bullet = SavePrefab(bulletRoot, folder, "PaperBomb");

						GameObject doll = MakePaperDoll(folder, color);

						GameObject root = new("PaperObject");
						AddBase(root, 0f);
						SerializedObject fairy = new(root.AddComponent<EquipmentFairy>());
						Set(fairy, "bulletPrefab", bullet);
						Set(fairy, "fairyPrefab", doll);
						Set(fairy, "originCoolTime", 1.6f);
						Set(fairy, "countStat", (int)Stat(spec, "COUNT"));
						Set(fairy, "damageBonusStat", (int)Stat(spec, "DAMAGE_BONUS"));
						Set(fairy, "attackSpeedBonusStat", (int)Stat(spec, "ATTACK_SPEED_BONUS"));
						fairy.ApplyModifiedPropertiesWithoutUndo();
						return SavePrefab(root, folder, "PaperObject");
					},
				},

				new WeaponSpec
				{
					Tier = 4, Key = "Scissors", Folder = "Scissors", Prefix = "SCISSORS", Id = 20000028, CardBase = 850, HitColor = Muted(0.62f, 0.68f, 0.78f),
					Name = "재단 가위", Description = "앞뒤를 동시에 벤다. 임시 판정과 임시 연출.", Icon = "fangs",
					Cards = StandardCards(4, hasCount: false, hasScale: true),
					Resonance = Resonance("마름질 끝", "가위 크기 +2, 가위 데미지 +12\n가위 데미지 강화 3회와 검 수 강화 2회 후 등장. 한 판에 1회.", new[] { Req("SCISSORS_DAMAGE_BONUS", 3), Req("C_400", 2) }, Fx("SCISSORS_SCALE_BONUS", 2), Fx("SCISSORS_DAMAGE_BONUS", 12)),
					BuildObject = (spec, folder) =>
					{
						GameObject slash = MakeBeam(folder, "ScissorsSlash", spec.HitColor, 1.6f, 4.5f, 0.15f, new DamageSpec(12, knockback: 6f, hitstop: 0.03f), root => CreateSprite(root, new SpriteSpec(IconSprite("fangs"), spec.HitColor, 1.2f, flat: true, height: 0.4f)).transform.localPosition = new Vector3(0f, 0.4f, 1.5f));
						return MakeSpawnerObject(spec, folder, slash, SpawnPositionStrategy.Player, SpawnAimStrategy.Aim, 1.6f, 0f, 0f, 1, opposite: true);
					},
				},
				new WeaponSpec
				{
					Tier = 4, Key = "Duster", Folder = "Duster", Prefix = "DUSTER", Id = 20000029, CardBase = 860, HitColor = Muted(0.64f, 0.58f, 0.44f),
					Name = "먼지떨이", Description = "앞쪽 적을 멀리 털어낸다. 임시 판정과 임시 연출.", Icon = "sonic-screech",
					Cards = StandardCards(1, hasCount: false, hasScale: true),
					Resonance = Resonance("봄맞이 대청소", "먼지떨이 크기 +2, 먼지떨이 데미지 +5\n먼지떨이 크기 강화 3회와 가위 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("DUSTER_SCALE_BONUS", 3), Req("SCISSORS_DAMAGE_BONUS", 2) }, Fx("DUSTER_SCALE_BONUS", 2), Fx("DUSTER_DAMAGE_BONUS", 5)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject sweep = MakeBeam(folder, "DusterSweep", color, 5f, 3f, 0.18f, new DamageSpec(3, knockback: 30f, hitstop: 0f), root => AddBurst(root, Burst(color, 10, 5f, 0.35f, 0.18f)));
						return MakeSpawnerObject(spec, folder, sweep, SpawnPositionStrategy.Player, SpawnAimStrategy.Aim, 2f, 0f, 0f, 1);
					},
				},
				new WeaponSpec
				{
					Tier = 4, Key = "Curse", Folder = "Curse", Prefix = "CURSE", Id = 20000030, CardBase = 870, HitColor = Muted(0.44f, 0.16f, 0.44f),
					Name = "저주 페이지", Description = "주기적으로 화면의 적과 떨어진 보석을 함께 지운다. 보스는 피해만.", Icon = "spell-book",
					Cards = new[]
					{
						new CardSpec("ATTACK_SPEED_BONUS", "재사용 감소", 1, 6, SurvivalUIKind.Speed),
						new CardSpec("DAMAGE_BONUS", "보스 피해 증가", 50, 20, SurvivalUIKind.Damage),
					},
					Resonance = Resonance("찢어진 금서", "재사용 대기 추가 감소, 보스 피해 +200\n재사용 감소 강화 2회와 페이지 수 강화 3회 후 등장. 한 판에 1회.", new[] { Req("CURSE_ATTACK_SPEED_BONUS", 2), Req("PAGE_COUNT", 3) }, Fx("CURSE_ATTACK_SPEED_BONUS", 1), Fx("CURSE_DAMAGE_BONUS", 200)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						Material material = EnsureFieldMaterial(folder, "CurseWave", color, 0.04f, 0.5f, 0.03f);
						GameObject effectRoot = new("CurseWave");
						effectRoot.AddComponent<PoolingObject>();
						SerializedObject life = new(effectRoot.AddComponent<ObjectWithTime>());
						Set(life, "duration", 0.5f);
						life.ApplyModifiedPropertiesWithoutUndo();
						CreateExpandingRing(effectRoot, material, 1f, 32f, 0.5f);
						CreateSprite(effectRoot, new SpriteSpec(IconSprite("spell-book"), color, 1.6f, height: 1.6f));
						GameObject effect = SavePrefab(effectRoot, folder, "CurseWave");

						GameObject root = new("CurseObject");
						AddBase(root, 0f);
						SerializedObject wipe = new(root.AddComponent<EquipmentScreenWipe>());
						Set(wipe, "baseCooldown", 60f);
						Set(wipe, "cooldownStat", (int)Stat(spec, "ATTACK_SPEED_BONUS"));
						Set(wipe, "bossDamageStat", (int)Stat(spec, "DAMAGE_BONUS"));
						Set(wipe, "effectPrefab", effect);
						wipe.ApplyModifiedPropertiesWithoutUndo();
						return SavePrefab(root, folder, "CurseObject");
					},
				},
				new WeaponSpec
				{
					Tier = 4, Key = "ClockHand", Folder = "ClockHand", Prefix = "CLOCK", Id = 20000031, CardBase = 880, HitColor = Muted(0.36f, 0.6f, 0.7f),
					Name = "시계 바늘", Description = "바늘이 시계 방향으로 돌며 맞은 적을 거의 멈춘다.", Icon = "tension-snowflake",
					Cards = StandardCards(2, countLabel: "바늘 수"),
					Resonance = Resonance("멈춘 시계", "바늘 수 +1, 바늘 데미지 +6\n바늘 공격속도 강화 3회와 빔 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("CLOCK_ATTACK_SPEED_BONUS", 3), Req("BEAM_DAMAGE_BONUS", 2) }, Fx("CLOCK_COUNT", 1), Fx("CLOCK_DAMAGE_BONUS", 6)),
					BuildObject = (spec, folder) =>
					{
						GameObject beam = MakeBeam(folder, "ClockHandBeam", spec.HitColor, 0.6f, 9f, 0.2f, new DamageSpec(6, knockback: 0f), root => AddStatus(root, 0.9f, 1.5f));
						return MakeSpawnerObject(spec, folder, beam, SpawnPositionStrategy.Player, SpawnAimStrategy.Clock, 1.2f, 0.05f, 0f, 1, clockStep: 30f);
					},
				},

				new WeaponSpec
				{
					Tier = 5, Key = "Thimble", Folder = "Thimble", Prefix = "THIMBLE", Id = 20000032, CardBase = 890, HitColor = Muted(0.68f, 0.6f, 0.36f),
					Name = "골무 방패", Description = "맞으면 주위 적을 밀쳐 내며 되받아친다.", Icon = "double-ringed-orb",
					Cards = new[]
					{
						new CardSpec("DAMAGE_BONUS", "데미지 증가", 6, 100, SurvivalUIKind.Damage),
						new CardSpec("ATTACK_SPEED_BONUS", "재사용 감소", 1, 5, SurvivalUIKind.Speed),
						new CardSpec("SCALE_BONUS", "크기 증가", 1, 5, SurvivalUIKind.Size),
					},
					Resonance = Resonance("두꺼운 골무", "골무 크기 +2, 골무 데미지 +15\n골무 데미지 강화 2회와 최대 체력 강화 3회 후 등장. 한 판에 1회.", new[] { Req("THIMBLE_DAMAGE_BONUS", 2), Req("C_3", 3) }, Fx("THIMBLE_SCALE_BONUS", 2), Fx("THIMBLE_DAMAGE_BONUS", 15)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject counter = MakeBlast(folder, "ThimbleCounter", color, 3.5f, 0.3f, new DamageSpec(20, knockback: 14f, hitstop: 0.03f), Burst(color, 14, 7f, 0.35f, 0.16f, 0.08f));
						return MakeCounterShield(spec, folder, color, 1.2f, counter);
					},
				},
				new WeaponSpec
				{
					Tier = 5, Key = "Chore", Folder = "Chore", Prefix = "CHORE", Id = 20000033, CardBase = 900, HitColor = Muted(0.54f, 0.64f, 0.38f),
					Name = "집안일 수첩", Description = "움직인 만큼 쌓였다가 멈추는 순간 주위를 쓸어 낸다.", Icon = "archive-research",
					Cards = new[]
					{
						new CardSpec("DAMAGE_BONUS", "데미지 증가", 5, 100, SurvivalUIKind.Damage),
						new CardSpec("ATTACK_SPEED_BONUS", "충전 속도 증가", 1, 5, SurvivalUIKind.Speed),
						new CardSpec("SCALE_BONUS", "크기 증가", 1, 5, SurvivalUIKind.Size),
					},
					Resonance = Resonance("밀린 집안일", "수첩 크기 +2, 수첩 데미지 +20\n수첩 데미지 강화 3회와 베개 데미지 강화 2회 후 등장. 한 판에 1회.", new[] { Req("CHORE_DAMAGE_BONUS", 3), Req("PILLOW_DAMAGE_BONUS", 2) }, Fx("CHORE_SCALE_BONUS", 2), Fx("CHORE_DAMAGE_BONUS", 20)),
					BuildObject = (spec, folder) =>
					{
						Color color = spec.HitColor;
						GameObject burst = MakeBlast(folder, "ChoreBurst", color, 4f, 0.3f, new DamageSpec(10, knockback: 5f, hitstop: 0.02f), Burst(color, 16, 6f, 0.4f, 0.16f, 0.06f), root => CreateSprite(root, new SpriteSpec(IconSprite("archive-research"), color, 1.2f, height: 1.5f)));
						return MakeChoreNote(spec, folder, color, 2.5f, burst);
					},
				},
				new WeaponSpec
				{
					Tier = 5, Key = "Pull", Folder = "Magnet", Prefix = null, Id = 20000034, CardBase = 910,
					Name = "끌리는 실", Description = "떨어진 보석을 더 멀리서 끌어온다.", Icon = "thorny-tentacle",
					ExtraEffects = new[] { Fx("EXP_COLLIDER_SCALE", 2) },
					Cards = new[]
					{
						new CardSpec("!EXP_COLLIDER_SCALE", "흡수 범위 증가", 1, 10, SurvivalUIKind.Size),
					},
					Resonance = Resonance("한 올도 안 놓쳐", "흡수 범위 +4\n자석 강화 3회와 실타래 튕김 강화 2회 후 등장. 한 판에 1회.", new[] { Req("C_5", 3), Req("THREAD_CHAIN", 2) }, Fx("EXP_COLLIDER_SCALE", 4)),
					BuildObject = (spec, folder) => LoadRequired<EquipmentData>(WEAPON_ROOT + "/I_20000000_자석.asset").Object,
				},
			};
		}

		private const float PAPER_DOLL_SIZE = 0.9f;
		private const string POTION_ROOT = "Assets/_WitchMendokusai/Domain/Crafting/Equipment/Potion";

		private static GameObject MakePaperDoll(string folder, Color tint)
		{
			GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(LoadRequired<GameObject>(WEAPON_ROOT + "/Fairy/Fairy.prefab"));
			PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
			foreach (SpriteRenderer spriteRenderer in instance.GetComponentsInChildren<SpriteRenderer>(true))
			{
				if (spriteRenderer.name.Contains("Unit"))
				{
					Sprite sprite = IconSprite("voodoo-doll");
					spriteRenderer.sprite = sprite;
					spriteRenderer.color = tint;
					spriteRenderer.transform.localScale = Vector3.one * PAPER_DOLL_SIZE / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
				}
			}
			return SavePrefab(instance, folder, "PaperDoll");
		}

		private static void SerializedObjectSet(Object target, string propertyName, Object value)
		{
			UnityEditor.SerializedObject serialized = new(target);
			Set(serialized, propertyName, value);
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static void SerializedObjectSetHulahoop(EquipmentHulahoop hulahoop, GameObject satellite, WeaponSpec spec)
		{
			UnityEditor.SerializedObject serialized = new(hulahoop);
			Set(serialized, "satellitePrefab", satellite);
			Set(serialized, "originRotateSpeed", 90f);
			Set(serialized, "countStat", (int)Stat(spec, "COUNT"));
			Set(serialized, "rotateSpeedBonusStat", (int)Stat(spec, "ATTACK_SPEED_BONUS"));
			Set(serialized, "damageBonusStat", (int)Stat(spec, "DAMAGE_BONUS"));
			Set(serialized, "scaleBonusStat", (int)Stat(spec, "SCALE_BONUS"));
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
