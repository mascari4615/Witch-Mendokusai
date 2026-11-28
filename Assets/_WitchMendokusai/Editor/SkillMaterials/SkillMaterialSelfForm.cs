using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 내 몸에 거는 모양 (자기 강화, 보호막, 회복, 변신). 주기마다 내 자리에 탄 하나가 켜져 몸을 따라다니고, 수명 동안 효과가 걸림
	// 탄은 바탕 프리팹 없이 새로 짓음 (판정이 없어 장판 바탕의 콜라이더, 피해 부품이 필요 없음). 무기는 공통 Spawner (SpawnPosition Player)
	// 종류는 JSON 값 SelfKind 하나로 가름. 같은 클래스를 이름만 달리 여럿 등록 (SelfBuff, SelfShield, SelfHeal, SelfTransform)
	// - Buff: 기존 부품 StatWhileActive 로 켜져 있는 동안 BuffStat 을 BuffAmount 만큼 올림
	// - Heal: SkillMaterialSelfPulse 가 HealAmount 회복. HealEvery 가 0 이면 켜지는 순간 한 번, 아니면 그 주기마다 (CalmSeconds 동안 안 맞았을 때). HealFx 는 회복마다의 연출
	// - Shield: SkillMaterialSelfPulse 가 ShieldSeconds 동안 (0 이면 켜져 있는 내내) ShieldCharges 번 피해를 막음. BuffAmount 가 있으면 켜져 있는 동안 BuffStat 도 올림
	// 몸 둘레 표시는 이펙트 겹 (Burst). 겹 반지름을 빼면 Radius
	internal sealed class SelfForm : ISkillMaterialForm
	{
		private enum SelfKind
		{
			Buff = 0,
			Heal = 1,
			Shield = 2,
		}

		public SelfForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => true;

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			// 맞히는 판정이 없어 적중 효과를 붙일 곳이 없음. 조용히 버리지 않고 바로 실패
			if (spec.Effects.Count > 0)
				throw new InvalidOperationException($"{spec.Id}: {Name} 은 적중 효과 (Effects) 를 받지 않음");
			SelfKind kind = ParseEnum<SelfKind>(spec, "SelfKind");
			GameObject root = new(spec.Id + "_Shot");
			try
			{
				root.AddComponent<SkillObject>();
				root.AddComponent<PoolingObject>();
				root.AddComponent<FollowUser>();
				SkillMaterialAssets.SetFields(root.AddComponent<ObjectWithTime>(), ("duration", spec.Float("Lifetime")));
				AddEffect(root, spec, kind);
				SkillMaterialAssets.AddLayers(root, spec);
				return SkillMaterialAssets.SaveShot(root, spec.Id + "_Shot");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(root);
			}
		}

		private static void AddEffect(GameObject root, SkillMaterialSpec spec, SelfKind kind)
		{
			switch (kind)
			{
				case SelfKind.Buff:
					SkillMaterialAssets.SetFields(root.AddComponent<StatWhileActive>(),
						("gateStat", (int)UnitStatType.NONE),
						("targetStat", (int)ParseEnum<UnitStatType>(spec, "BuffStat")),
						("amount", spec.Int("BuffAmount")));
					break;
				case SelfKind.Heal:
					SkillMaterialSelfPulse heal = root.AddComponent<SkillMaterialSelfPulse>();
					SkillMaterialAssets.SetFields(heal,
						("healAmount", spec.Int("HealAmount")),
						("healEverySeconds", spec.Float("HealEvery")),
						("calmSeconds", spec.Float("CalmSeconds")),
						("healFxRadius", spec.Float("Radius")));
					// 회복할 때마다의 연출. 빈 문자열이면 없음
					string healFxPath = spec.Text("HealFx");
					if (string.IsNullOrEmpty(healFxPath) == false)
					{
						CombatFx healFx = UnityEditor.AssetDatabase.LoadAssetAtPath<CombatFx>(healFxPath);
						if (healFx == null)
							throw new InvalidOperationException($"{spec.Id}: HealFx 에셋 없음 {healFxPath}");
						SkillMaterialAssets.SetFields(heal, ("healFx", healFx));
					}
					break;
				case SelfKind.Shield:
					SkillMaterialAssets.SetFields(root.AddComponent<SkillMaterialSelfPulse>(),
						("shieldCharges", spec.Int("ShieldCharges")),
						("shieldSeconds", spec.Float("ShieldSeconds")),
						("breakInvincibleSeconds", spec.Float("InvincibleSeconds")));
					// 보호막과 함께 켜져 있는 동안 능력치 (원본: 받는 피해 감소). BuffAmount 0 이면 없음
					if (spec.Int("BuffAmount") > 0)
					{
						SkillMaterialAssets.SetFields(root.AddComponent<StatWhileActive>(),
							("gateStat", (int)UnitStatType.NONE),
							("targetStat", (int)ParseEnum<UnitStatType>(spec, "BuffStat")),
							("amount", spec.Int("BuffAmount")));
					}
					break;
				default:
					throw new InvalidOperationException($"{spec.Id}: 굽는 법 없는 SelfKind {kind}");
			}
		}

		private static T ParseEnum<T>(SkillMaterialSpec spec, string key) where T : struct, Enum => SkillMaterialEnums.Parse<T>(spec, key);
	}

	internal static class SkillMaterialEnums
	{
		// 이름만 받음 (숫자 금지). 상태이상 이름 검사 (SkillMaterialCatalog.ParseEffects) 와 같은 규칙
		public static T Parse<T>(SkillMaterialSpec spec, string key) where T : struct, Enum
		{
			string text = spec.Text(key);
			if (int.TryParse(text, out _) || Enum.IsDefined(typeof(T), text) == false)
				throw new InvalidOperationException($"{spec.Id}: {key} 에 없는 이름 {text} ({typeof(T).Name})");
			return (T)Enum.Parse(typeof(T), text);
		}
	}
}
