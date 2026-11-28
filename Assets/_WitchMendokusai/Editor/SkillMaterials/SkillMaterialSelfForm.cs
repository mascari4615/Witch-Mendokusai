using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 내 몸에 거는 모양 (자기 강화, 보호막, 회복, 변신). 주기마다 내 자리에 탄 하나가 켜져 몸을 따라다니고, 수명 동안 효과가 걸림
	// 탄은 바탕 프리팹 없이 새로 짓음 (판정이 없어 장판 바탕의 콜라이더, 피해 부품이 필요 없음). 무기는 공통 Spawner (SpawnPosition Player)
	// 종류는 JSON 값 SelfKind 하나로 가름. 같은 클래스를 이름만 달리 여럿 등록 (SelfBuff, SelfShield, SelfHeal, SelfTransform)
	// - Buff: 기존 부품 StatWhileActive 로 켜져 있는 동안 BuffStat 을 BuffAmount 만큼 올림
	// - Heal: SkillMaterialSelfPulse 가 켜지는 순간 HealAmount 회복
	// - Shield: SkillMaterialSelfPulse 가 켜져 있는 동안 ShieldCharges 번 피해를 막음
	// 몸 둘레 표시는 이펙트 겹 (Burst). 겹 반지름을 빼면 Radius
	internal sealed class SelfForm : ISkillMaterialForm
	{
		private enum SelfKind
		{
			Buff,
			Heal,
			Shield,
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
					SkillMaterialAssets.SetFields(root.AddComponent<SkillMaterialSelfPulse>(), ("healAmount", spec.Int("HealAmount")));
					break;
				case SelfKind.Shield:
					SkillMaterialAssets.SetFields(root.AddComponent<SkillMaterialSelfPulse>(),
						("shieldCharges", spec.Int("ShieldCharges")),
						("breakInvincibleSeconds", spec.Float("InvincibleSeconds")));
					break;
				default:
					throw new InvalidOperationException($"{spec.Id}: 굽는 법 없는 SelfKind {kind}");
			}
		}

		// 이름만 받음 (숫자 금지). 상태이상 이름 검사 (SkillMaterialCatalog.ParseEffects) 와 같은 규칙
		private static T ParseEnum<T>(SkillMaterialSpec spec, string key) where T : struct, Enum
		{
			string text = spec.Text(key);
			if (int.TryParse(text, out _) || Enum.IsDefined(typeof(T), text) == false)
				throw new InvalidOperationException($"{spec.Id}: {key} 에 없는 이름 {text} ({typeof(T).Name})");
			return (T)Enum.Parse(typeof(T), text);
		}
	}
}
