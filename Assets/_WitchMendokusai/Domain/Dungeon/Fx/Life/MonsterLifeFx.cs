using System.Collections;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 몬스터의 등장과 사망 연출을 잇는 컴포넌트. 켜질 때 등장 스타일, 죽을 때 사망 스타일
	/// - 스타일 칸을 비우면 그 연출만 없음
	/// - 등장 튕김은 popTarget 의 크기만 건드림. 피격 부풀림과 정예 배율은 UnitObject.MeshParent 를 쓰므로 그 아래 트랜스폼을 지정
	/// - 풀 재사용: 꺼질 때 popTarget 크기를 원래대로 돌림
	/// - 사망 모습은 체력이 0 이 되는 순간에 잡아 둠. OnDeath 는 본체가 꺼지고 정예 색과 크기가 풀린 뒤에 불리기 때문
	/// </summary>
	[RequireComponent(typeof(DamageReaction))]
	public class MonsterLifeFx : MonoBehaviour, IDeathReaction
	{
		[SerializeField, Tooltip("등장 연출. 비우면 없음")] private MonsterSpawnStyle spawnStyle;
		[SerializeField, Tooltip("사망 연출. 비우면 없음")] private MonsterDeathStyle deathStyle;
		[SerializeField, Tooltip("사망 때 잔상으로 복사할 몸 그림")] private SpriteRenderer body;
		[SerializeField, Tooltip("등장 때 크기를 튕길 트랜스폼. UnitObject.MeshParent 의 자식 (다른 코드가 크기를 안 건드리는 것)")] private Transform popTarget;
		[SerializeField, Min(0f), Tooltip("발밑 고리 크기 배율. 큰 몸은 키움")] private float puffSizeMultiplier = 1f;

		private UnitHealth health;
		private Vector3 popBaseScale;
		private MonsterDeathGhostPose deathPose;
		private bool hasDeathPose;

		private void Awake()
		{
			health = GetComponent<UnitHealth>();
			popBaseScale = popTarget.localScale;
		}

		private void OnEnable()
		{
			hasDeathPose = false;
			health.OnHealthChanged += CaptureDeathPose;

			if (spawnStyle == null)
				return;
			spawnStyle.PlayPuff(transform.position, puffSizeMultiplier);
			if (spawnStyle.PopSeconds > 0f)
				StartCoroutine(PopRoutine());
		}

		private void OnDisable()
		{
			health.OnHealthChanged -= CaptureDeathPose;
			// 코루틴은 비활성화로 멈춤. 도중에 꺼졌어도 다음 사용 때 제 크기
			popTarget.localScale = popBaseScale;
		}

		private IEnumerator PopRoutine()
		{
			float seconds = spawnStyle.PopSeconds;
			float elapsed = 0f;
			while (elapsed < seconds)
			{
				popTarget.localScale = Vector3.Scale(popBaseScale, spawnStyle.EvaluatePop(elapsed / seconds));
				yield return null;
				elapsed += Time.deltaTime;
			}
			popTarget.localScale = popBaseScale;
		}

		// 체력 변화는 사망 통지보다 먼저 옴. 0 이 된 순간의 모습이 죽는 모습
		private void CaptureDeathPose(int currentHealth, int maxHealth)
		{
			if (currentHealth > 0 || deathStyle == null)
				return;
			deathPose = MonsterDeathGhost.Capture(body);
			hasDeathPose = true;
		}

		public void OnDeath()
		{
			if (deathStyle == null)
				return;
			if (hasDeathPose == false)
				deathPose = MonsterDeathGhost.Capture(body);
			hasDeathPose = false;
			MonsterDeathGhost.Play(deathPose, deathStyle);
		}
	}
}
