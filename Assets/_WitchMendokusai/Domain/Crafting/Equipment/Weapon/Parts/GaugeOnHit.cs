using UnityEngine;

namespace WitchMendokusai
{
	// 이 탄이 처음 맞힌 몬스터 하나를 일제 사격 게이지 (VolleyGauge.Current) 에 알림. 탄 하나는 한 번만 셈
	// 게이지가 없는 형태 (완드) 에서는 아무것도 안 함
	public class GaugeOnHit : SkillComponent, IHitEffect
	{
		private bool counted;

		public override void InitContext(SkillObject skillObject) => counted = false;

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (counted || VolleyGauge.Current == null || victim is MonsterObject == false)
				return;
			counted = true;
			VolleyGauge.Current.OnWeaponHit(victim);
		}
	}
}
