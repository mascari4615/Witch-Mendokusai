using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 일격을 내는 모양의 공통. 탄 (기다리는 몸) 과 그 탄이 내는 일격 (원형 판정, AreaForm.Build) 두 프리팹을 구움
	// - 탄: 판정 없음. OnBlast 가 아닌 겹 (예고, 몸 둘레 빛, 떨어지는 검)
	// - 일격: 피해, 적중 효과, 처형 (MissingHealthPercent), OnBlast 겹 (적중 터짐). 수치 Radius, Damage, Lifetime, RehitInterval, Knockback, Hitstop, Shake 는 일격의 것
	internal abstract class SkillMaterialStrikeForm : ISkillMaterialForm
	{
		protected SkillMaterialStrikeForm(string name) => Name = name;

		public string Name { get; }
		public bool AcceptsLayers => true;

		public GameObject BuildShot(SkillMaterialSpec spec)
		{
			GameObject blast = AreaForm.Build(spec, "_Shot_Blast", followUser: false, onBlast: true, extra: built => AddExecute(built, spec));
			GameObject root = new(spec.Id + "_Shot");
			try
			{
				root.AddComponent<SkillObject>();
				root.AddComponent<PoolingObject>();
				BuildWaiter(root, spec, blast);
				SkillMaterialAssets.AddLayers(root, spec);
				return SkillMaterialAssets.SaveShot(root, spec.Id + "_Shot");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(root);
			}
		}

		// 기다리는 몸의 부품. 일격 프리팹을 내는 방식이 모양마다 다름
		protected abstract void BuildWaiter(GameObject root, SkillMaterialSpec spec, GameObject blast);

		private static void AddExecute(GameObject blast, SkillMaterialSpec spec)
		{
			int percent = spec.Int("MissingHealthPercent");
			if (percent > 0)
				SkillMaterialAssets.SetFields(blast.AddComponent<MissingHealthDamage>(), ("percent", percent));
		}
	}

	// 강화 일격 (원본: 이동속도 상승 뒤 다음 평타 강화). 몸을 따라다니며 WaitSeconds 동안 이동속도를 올리고, StrikeRange 안에 적이 처음 들어오면 그 적 자리에 일격
	internal sealed class EmpoweredStrikeForm : SkillMaterialStrikeForm
	{
		public EmpoweredStrikeForm(string name) : base(name) { }

		protected override void BuildWaiter(GameObject root, SkillMaterialSpec spec, GameObject blast)
		{
			root.AddComponent<FollowUser>();
			SkillMaterialAssets.SetFields(root.AddComponent<ObjectWithTime>(), ("duration", spec.Float("WaitSeconds")));
			int buffAmount = spec.Int("BuffAmount");
			if (buffAmount > 0)
			{
				SkillMaterialAssets.SetFields(root.AddComponent<StatWhileActive>(),
					("gateStat", (int)UnitStatType.NONE),
					("targetStat", (int)SkillMaterialEnums.Parse<UnitStatType>(spec, "BuffStat")),
					("amount", buffAmount));
			}
			SkillMaterialAssets.SetFields(root.AddComponent<SkillMaterialEmpoweredStrike>(), ("strikePrefab", blast), ("range", spec.Float("StrikeRange")));
		}
	}

	// 지연 일격 (원본: 대상 위로 떨어지는 큰 검). 무기가 놓은 자리 (가까운 적) 에서 StrikeDelay 뒤 일격. 떨어지는 그림은 탄의 CombatFx 겹이 맡고 그 겹의 때리는 순간을 StrikeDelay 에 맞춤
	internal sealed class DelayedStrikeForm : SkillMaterialStrikeForm
	{
		// 터지고 나서 꺼지기까지 여유. 터짐이 먼저 오게
		private const float TAIL_SECONDS = 0.2f;

		public DelayedStrikeForm(string name) : base(name) { }

		protected override void BuildWaiter(GameObject root, SkillMaterialSpec spec, GameObject blast)
		{
			float delay = spec.Float("StrikeDelay");
			if (delay <= 0f)
				throw new InvalidOperationException($"{spec.Id}: StrikeDelay 는 0 보다 커야 함");
			SkillMaterialAssets.SetFields(root.AddComponent<ObjectWithTime>(), ("duration", delay + TAIL_SECONDS));
			SkillMaterialAssets.SetFields(root.AddComponent<ExplodeOnEnd>(), ("explosionPrefab", blast), ("fuseSeconds", delay), ("playSplashSfx", false), ("debrisPushRadius", spec.Float("Radius")));
		}
	}
}
