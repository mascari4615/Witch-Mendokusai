namespace WitchMendokusai
{
	// 검기가 맞힌 적을 빛의 검 (SwordWaveEmitter.Current) 에 알림. 축복 카드 (가속, 수선) 가 셈
	public class WaveHitReport : SkillComponent, IHitEffect
	{
		public override void InitContext(SkillObject skillObject)
		{
		}

		public void OnHit(DamagingObject source, UnitObject victim)
		{
			if (SwordWaveEmitter.Current != null)
				SwordWaveEmitter.Current.OnWaveHit(victim);
		}
	}
}
