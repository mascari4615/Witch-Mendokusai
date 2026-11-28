using System;
using UnityEngine;

namespace WitchMendokusai
{
	// 스킬 재료의 이펙트 겹 CombatFx. 탄이 켜질 때 기존 전투 연출 에셋 (도는 팔, 하늘 검, 그린 덩이) 을 탄 자리에서 재생
	// 따라다니는 연출은 탄이 꺼질 때 멈춤 (따라갈 몸이 사라짐). 고정 자리 연출 (떨어지는 검) 은 탄이 먼저 꺼져도 끝까지
	// 판정 없음. 같은 탄의 판정 부품과 나란히 붙음. 값은 굽는 도구 (SkillMaterialAssets.AddLayers) 가 레시피에서 채움
	public class SkillMaterialCombatFx : SkillComponent
	{
		[SerializeField] private CombatFx fx;
		[SerializeField, Min(0.05f)] private float seconds = 1f;
		[SerializeField, Min(0f)] private float radius = 1f;
		[SerializeField, Min(0f)] private float length = 2f;
		[SerializeField, Min(0f)] private float width = 1f;
		[SerializeField, Tooltip("탄을 따라다님. 끄면 켜진 자리에 고정")] private bool follow;
		[SerializeField, Tooltip("초당 바퀴 수. 0 이면 돌지 않음 (도는 팔 연출에만 쓰임)")] private float turnsPerSecond;

		private SkillContext context;
		private Action stop;

		public override void InitContext(SkillObject skillObject) => context = skillObject.Context;

		private void OnEnable()
		{
			// 풀이 미리 만들 때는 맥락 없이 꺼진 채라 여기 안 옴. 맥락 없이 켜졌으면 아직 쏜 적 없는 사본
			if (context == null)
				return;
			if (fx == null)
				throw new InvalidOperationException($"{name}: CombatFx 에셋이 비었음");
			Vector3 direction = transform.forward;
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.0001f)
				direction = Vector3.forward;
			float speed = turnsPerSecond * 360f;
			stop = fx.Play(new CombatFxContext
			{
				Position = transform.position,
				Origin = context.User != null ? context.User.transform.position : transform.position,
				Direction = direction.normalized,
				Follow = follow ? transform : null,
				Radius = radius,
				Length = length,
				Width = width,
				Seconds = seconds,
				Turn = Mathf.Approximately(speed, 0f) ? null : time => time * speed,
			});
		}

		private void OnDisable()
		{
			if (follow)
				stop?.Invoke();
			stop = null;
		}
	}
}
