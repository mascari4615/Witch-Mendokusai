using UnityEngine;

namespace WitchMendokusai
{
	// 거리 두기. 좋아하는 거리 띠 안에서 옆걸음, 안쪽이면 뒷걸음, 바깥이면 다가옴. 옆걸음 쪽은 몇 초마다 바뀜
	// 원거리 공격과 같이 끼우면 쏘고 빠지는 몬스터. 시키는 행동: 몰아붙여 벽에 가두기
	[CreateAssetMenu(fileName = "MT_Kite", menuName = "WM/Monster/Temperament/Kite")]
	public sealed class KiteTemperament : MonsterTemperament
	{
		[SerializeField, Tooltip("이보다 가까우면 뒷걸음 (m)"), Min(0f)] private float nearDistance = 4f;
		[SerializeField, Tooltip("이보다 멀면 다가옴 (m)"), Min(0f)] private float farDistance = 7f;
		[SerializeField, Tooltip("옆걸음 쪽을 바꾸는 간격 (초)"), Min(0.1f)] private float strafeSeconds = 2f;
		[SerializeField, Tooltip("옆걸음 간격을 유닛마다 이 비율만큼 달리 함"), Range(0f, 1f)] private float strafeJitter = 0.5f;
		[SerializeField, Tooltip("뒷걸음 세기 0~1"), Range(0f, 1f)] private float backSpeed = 0.7f;
		[SerializeField, Tooltip("옆걸음 세기 0~1"), Range(0f, 1f)] private float strafeSpeed = 0.5f;

		// 뒷걸음 거리와 다가올 거리 사이는 이만큼 벌림 (옆걸음 띠)
		private const float MIN_BAND = 0.5f;

		public override string KindName => "거리 두기";

		private void OnValidate() => farDistance = Mathf.Max(farDistance, nearDistance + MIN_BAND);

		// 기억의 Side 는 옆걸음 쪽, NextTime 은 쪽을 바꿀 시각
		public override TemperamentStep Steer(in TemperamentContext context, TemperamentMemory memory)
		{
			Vector3 toPlayer = TemperamentContext.Toward(context.Self, context.Player);
			float distance = context.Distance;
			if (distance > farDistance)
				return TemperamentStep.Walk(toPlayer);
			if (distance < nearDistance)
				return new TemperamentStep { Direction = -toPlayer, Speed = backSpeed, FacesPlayer = true };
			if (context.Now >= memory.NextTime)
			{
				memory.Side = -memory.Side;
				memory.NextTime = context.Now + strafeSeconds * (1f - strafeJitter * 0.5f + memory.Seed * strafeJitter);
			}
			Vector3 side = Vector3.Cross(Vector3.up, toPlayer) * memory.Side;
			return new TemperamentStep { Direction = side, Speed = strafeSpeed, FacesPlayer = true };
		}
	}
}
