using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 형태를 받는 순간 (변신). 연출을 재생하고 둘레 적을 밀어냄 (피해 없음, 넉백 저항 따름). 판정 (무기, 숙련) 은 GameManager 가 이미 끝냄
	// IsPlaying 동안 카드 창은 열리지 않음 (CardManager). 쌓인 레벨업이 연출을 끊지 않게
	public sealed class DollFormMoment
	{
		public bool IsPlaying { get; private set; }

		public IEnumerator Play(DollFormData form, Transform body)
		{
			IsPlaying = true;
			form.TransformFx.Play(new CombatFxContext { Position = body.position, Direction = body.forward, Follow = body });
			PushAway(form, body.position);
			// 화면 연출과 같은 시계 (정지 중에도 흐르고 프레임 녹화에서는 고정 간격)
			for (float left = form.TransformSeconds; left > 0f; left -= FeelTime.Delta)
				yield return null;
			IsPlaying = false;
		}

		private static void PushAway(DollFormData form, Vector3 center)
		{
			List<GameObject> monsters = ObjectBufferManager.GetObjects(ObjectType.Monster);
			foreach (GameObject monster in monsters)
			{
				Vector3 outward = monster.transform.position - center;
				outward.y = 0f;
				if (outward.magnitude > form.PushRadius || monster.TryGetComponent(out KnockbackFeedback knockback) == false)
					continue;
				knockback.OnDamaged(new DamageInfo
				{
					knockbackForce = form.PushForce,
					knockbackDuration = form.PushSeconds,
					knockbackDirectionOverride = outward.ToSim(),
				});
			}
		}
	}
}
