using UnityEngine;

namespace WitchMendokusai
{
	// 공격 몸동작 (AttackPose) 을 그림에 입힘. 그림 트랜스폼의 크기와 자리만 건드림 (피격 부풀림과 정예 배율은 그 위 MeshParent)
	// 몸 하나에 하나, 첫 공격 때 붙고 공격이 끝나거나 끊기면 원래대로
	public sealed class MonsterPoseDriver : MonoBehaviour
	{
		private enum Stage
		{
			None = 0,
			Windup = 1,
			Strike = 2,
			Rest = 3,
			// 떠 있음 (Hold). 크기는 그대로
			Air = 4,
		}

		// 공격이 끝나고 이만큼 안에 다음 준비가 없으면 원래대로 (초). 행동 틱 (0.1초) 보다 김
		private const float RELEASE_SECONDS = 0.18f;

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
		private bool restsNext;
		// 그림을 땅에서 이만큼 위로 (m)
		private float lift;
		private float releaseTime = float.PositiveInfinity;

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
			restsNext = false;
			releaseTime = float.PositiveInfinity;
			Enter(Stage.Windup, newSeconds);
			SetSprite(pose.Windup);
		}

		// 공중에 뜬 몸. 그림과 높이를 매 프레임 밖에서 정함
		public void Hold(AttackPose newPose, Sprite sprite, float newLift)
		{
			Capture();
			pose = newPose;
			restsNext = false;
			releaseTime = float.PositiveInfinity;
			lift = newLift;
			if (stage != Stage.Air)
				Enter(Stage.Air, 1f);
			SetSprite(sprite);
		}

		// 단계가 바뀌어 원래 그림 자체가 바뀜
		public void SetBase(Sprite sprite)
		{
			if (stage == Stage.None)
				body.sprite = sprite;
			else
				baseSprite = sprite;
		}

		public void Strike()
		{
			if (stage == Stage.None)
				return;
			lift = 0f;
			Enter(Stage.Strike, pose.StrikeSeconds);
			SetSprite(pose.Strike);
		}

		// 때린 자세를 다 보인 뒤 처짐. 때리지 못하고 끊겼으면 바로
		public void Rest()
		{
			if (stage == Stage.Strike)
				restsNext = true;
			else if (stage is Stage.Windup or Stage.Air)
				BeginRest();
		}

		// 공격 하나가 끝남. 곧 다음 준비가 오면 이어지고, 아니면 원래대로
		public void Release()
		{
			// 떠 있다 끊기면 바로 땅으로
			if (stage == Stage.Air)
				Clear();
			else if (stage != Stage.None && float.IsPositiveInfinity(releaseTime))
				releaseTime = Time.time + RELEASE_SECONDS;
		}

		private void BeginRest()
		{
			restsNext = false;
			Enter(Stage.Rest, 1f);
			SetSprite(pose.Rest);
		}

		private void Clear()
		{
			if (stage == Stage.None)
				return;
			stage = Stage.None;
			restsNext = false;
			releaseTime = float.PositiveInfinity;
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
			if (Time.time >= releaseTime)
			{
				Clear();
				return;
			}
			if (stage == Stage.Strike && restsNext && Time.time - startTime >= pose.StrikeSeconds)
				BeginRest();
			float time = Time.time - startTime;
			float progress = Mathf.Clamp01(time / seconds);
			float stretch = 1f;
			float shake = 0f;
			switch (stage)
			{
				case Stage.Windup:
					if (pose.StrikeLeadSeconds > 0f && time >= seconds - pose.StrikeLeadSeconds)
						SetSprite(pose.Strike);
					stretch = 1f - pose.Crouch * Mathf.SmoothStep(0f, 1f, progress * pose.CrouchEase);
					shake = Mathf.Sin(Time.time * pose.TrembleSpeed) * pose.Tremble * progress * progress;
					break;
				case Stage.Strike:
					stretch = StrikeStretch(time);
					break;
				case Stage.Rest:
					stretch = 1f + pose.Breath * Mathf.Sin(time * pose.BreathSpeed);
					break;
			}
			// 단계가 바뀔 때 튀지 않게 따라감. 때리는 순간은 바로
			lastStretch = stage == Stage.Strike ? stretch : Mathf.Lerp(lastStretch, stretch, 1f - Mathf.Exp(-Time.deltaTime * pose.FollowRate));
			target.localScale = new Vector3(baseScale.x / Mathf.Sqrt(lastStretch), baseScale.y * lastStretch, baseScale.z);
			// 떨림 폭은 월드 단위. 보스 몸은 배율이 커서 나눔
			target.localPosition = basePosition + Vector3.right * (shake / target.parent.lossyScale.x);
			if (stage == Stage.Air)
				target.position += Vector3.up * lift;
		}

		private float StrikeStretch(float time) => 1f + pose.Stretch * Mathf.Exp(-time * pose.WobbleDamping) * Mathf.Cos(time * pose.WobbleSpeed);
	}
}
