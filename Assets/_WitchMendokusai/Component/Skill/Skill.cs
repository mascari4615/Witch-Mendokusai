using UnityEngine;

namespace WitchMendokusai
{
	public class Skill
	{
		public SkillData Data { get; private set; }
		public Cooldown Cooldown { get; private set; }
		public bool IsReady => Cooldown.IsReady;

		public Skill(SkillData data)
		{
			Data = data;
			Cooldown = new Cooldown();
		}

		public void UpdateCooltime(float coolTimeBonus = 0)
		{
			Cooldown.Set(Data.Cooltime * (1f - (coolTimeBonus / 100f)));
		}

		public void Tick() => Cooldown.Tick();

		public void Use(UnitObject unitObject)
		{
			Data.Use(unitObject);
			Cooldown.Reset();
		}
	}

	public class Cooldown
	{
		public float Base { get; private set; }
		public float Remain { get; private set; }
		public bool IsReady => Remain == 0;

		public void Set(float baseCooltime) => Base = baseCooltime;
		public void Reset() => Remain = Base;
		public void Tick() => Remain = Mathf.Clamp(Remain - TimeManager.TICK, 0, float.MaxValue);
	}
}