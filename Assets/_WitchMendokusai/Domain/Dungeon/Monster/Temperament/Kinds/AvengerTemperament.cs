using UnityEngine;

namespace WitchMendokusai
{
	// 복수. 평소엔 느릿느릿 다가옴. 근처에서 몬스터가 쓰러지면 멈춰 부풀고 (알아챔), 몇 초 동안 주인공에게 연달아 덤벼듦 (튀어 나감)
	// 감각: 근처 처치 (Fallen). 시키는 행동: 이놈 곁에서 잡지 않기, 이놈부터 잡기
	[CreateAssetMenu(fileName = "MT_Avenger", menuName = "WM/Monster/Temperament/Avenger")]
	public sealed class AvengerTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("평소 걷는 세기 (1 이 제 속도)"), Range(0f, 1f)] private float calmSpeed = 0.45f;
		[SerializeField, Tooltip("쓰러진 뒤 이 초 안에 알아채면 화남"), Min(0f)] private float noticeSeconds = 0.5f;
		[SerializeField, Tooltip("화나 있는 초"), Min(0f)] private float rageSeconds = 5f;
		[SerializeField, Tooltip("화났을 때 덤비는 사이 초"), Min(0.05f)] private float lungeInterval = 0.9f;
		[SerializeField, Tooltip("덤비는 속도 (m/s)"), Min(0f)] private float lungeSpeed = 8f;
		[SerializeField, Tooltip("덤비는 초"), Min(0f)] private float lungeSeconds = 0.25f;
		[SerializeField, Tooltip("화나는 순간 몸 부풀림 배율"), Min(1f)] private float ragePunch = 1.4f;

		public override string KindName => "복수";
		public override TemperamentSense Senses => TemperamentSense.Fallen;

		public static bool IsRaging(TemperamentMemory memory, float now) => memory.IsOn && now < memory.UntilTime;

		// 기억의 IsOn, UntilTime 은 화남과 풀리는 시각, NextTime 은 다음 덤빔 시각
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			if (context.SinceFallen <= noticeSeconds && IsRaging(memory, context.Now) == false)
			{
				memory.IsOn = true;
				memory.UntilTime = context.Now + rageSeconds;
				memory.NextTime = context.Now + lungeInterval;
				// 멈춰 부풂 (알아챔). 첫 덤빔은 다음 차례
				TemperamentStep notice = TemperamentStep.Stay;
				notice.Punch = ragePunch;
				return notice;
			}
			if (IsRaging(memory, context.Now) == false)
			{
				memory.IsOn = false;
				return TemperamentStep.Walk(toPlayer, calmSpeed);
			}
			if (context.Now >= memory.NextTime)
			{
				memory.NextTime = context.Now + lungeInterval;
				return TemperamentStep.Burst(toPlayer * lungeSpeed, lungeSeconds);
			}
			return TemperamentStep.Walk(toPlayer);
		}
	}
}
