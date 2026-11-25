using UnityEngine;

namespace WitchMendokusai
{
	// 공격 몸동작 (AttackPose) 을 그림에 입힘. 그림 트랜스폼의 크기와 자리만 건드림 (피격 부풀림과 정예 배율은 그 위 MeshParent)
	// 몸 하나에 하나, 첫 공격 때 붙고 공격이 끝나거나 끊기면 원래대로
	public sealed class MonsterPoseDriver : MonoBehaviour
	{
		private enum Stage { None, Windup, Strike, Rest }

		private SpriteRenderer body;
		private Transform target;
		private Vector3 baseScale;
		private Vector3 basePosition;
		private Sprite baseSprite;
		private AttackPose pose;
		private Stage stage;
		private float startTime;
		private float seconds;
		private float lastStretch = 1f;

		public static MonsterPoseDriver Of(UnitObject unitObject)
		{
			if (unitObject.TryGetComponent(out MonsterPoseDriver driver) == false)
				driver = unitObject.gameObject.AddComponent<MonsterPoseDriver>();
			driver.body = unitObject.SpriteRenderer;
			return driver;
		}

		public void Windup(AttackPose newPose, float newSeconds)
		{
			if (newPose.IsEmpty)
				return;
			Capture();
			pose = newPose;
			Enter(Stage.Windup, newSeconds);
			SetSprite(pose.Windup);
		}

		public void Strike()
		{
			if (stage == Stage.None)
				return;
			Enter(Stage.Strike, pose.StrikeSeconds);
			SetSprite(pose.Strike);
		}

		// 때린 자세를 다 보인 뒤 처짐
		public void Rest(float newSeconds)
		{
			if (stage != Stage.None)
				Enter(Stage.Rest, newSeconds);
		}

		public void Clear()
		{
			if (stage == Stage.None)
				return;
			stage = Stage.None;
			target.localScale = baseScale;
			target.localPosition = basePosition;
			body.sprite = baseSprite;
			lastStretch = 1f;
		}

		private void OnDisable() => Clear();

		private void Capture()
		{
			if (stage != Stage.None)
				return;
			target = body.transform;
			baseScale = target.localScale;
			basePosition = target.localPosition;
			baseSprite = body.sprite;
		}

		private void Enter(Stage newStage, float newSeconds)
		{
			stage = newStage;
			startTime = Time.time;
			seconds = Mathf.Max(0.01f, newSeconds);
		}

		private void SetSprite(Sprite sprite) => body.sprite = sprite != null ? sprite : baseSprite;

		private void LateUpdate()
		{
			if (stage == Stage.None)
				return;
			float time = Time.time - startTime;
			float progress = Mathf.Clamp01(time / seconds);
			float stretch = 1f;
			float shake = 0f;
			switch (stage)
			{
				case Stage.Windup:
					stretch = 1f - pose.Crouch * Mathf.SmoothStep(0f, 1f, progress * 1.6f);
					shake = Mathf.Sin(Time.time * 60f) * pose.Tremble * progress * progress;
					break;
				case Stage.Strike:
					stretch = StrikeStretch(time);
					break;
				case Stage.Rest when time < pose.StrikeSeconds:
					stretch = StrikeStretch(time);
					break;
				case Stage.Rest:
					SetSprite(pose.Rest);
					stretch = 1f + pose.Breath * Mathf.Sin((time - pose.StrikeSeconds) * 3.2f);
					break;
			}
			// 단계가 바뀔 때 튀지 않게 따라감. 때리는 순간은 바로
			lastStretch = stage == Stage.Strike ? stretch : Mathf.Lerp(lastStretch, stretch, 1f - Mathf.Exp(-Time.deltaTime * 18f));
			target.localScale = new Vector3(baseScale.x / Mathf.Sqrt(lastStretch), baseScale.y * lastStretch, baseScale.z);
			target.localPosition = basePosition + Vector3.right * shake;
		}

		private float StrikeStretch(float time) => 1f + pose.Stretch * Mathf.Exp(-time * 7f) * Mathf.Cos(time * 22f);
	}
}
