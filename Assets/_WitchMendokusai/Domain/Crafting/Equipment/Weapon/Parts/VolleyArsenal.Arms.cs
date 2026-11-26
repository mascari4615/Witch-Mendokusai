using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 몸 배치. 두 팔의 회전 총열과 두 어깨의 발사관 (3D, VolleyArmsModel) 이 몸에 붙어 조준을 따라 돌고, 날아가는 것은 빛뿐
	// - 박자: 무장이 맺힘, 멈춤, 몸에서 터지는 빛과 첫 발, 빛줄기를 잇는 포화, 막타
	// - 세 층: 팔의 빛줄기 (그림만, 쉬지 않고), 어깨의 중간 빛, 여섯 발마다 굵은 빛. 피해는 탄이 하므로 층은 그림만 나눔
	// - 문 배치 (VolleyGateObject 프리팹) 와 줄기, 맞은 자리, 바닥, 막타를 같이 씀
	public sealed partial class VolleyArsenal
	{
		private enum Formation
		{
			// 허공의 문에서 빛이 나감
			Gates = 0,
			// 몸에 붙은 무장
			Arms = 1,
		}

		[Header("몸 배치")]
		[SerializeField] private Formation formation = Formation.Gates;
		[SerializeField, Tooltip("몸에 붙은 무장 (3D). 몸 배치만")] private VolleyArmsModel armsModel;
		[SerializeField, Min(1), Tooltip("앞에서부터 몇 자루가 팔인지 (나머지는 어깨)")] private int armHands = 2;
		[SerializeField, Min(2), Tooltip("자루 수")] private int armCount = 4;
		[SerializeField, Min(1), Tooltip("몇 발을 한 묶음으로 셈")] private int shotPattern = 6;
		[SerializeField, Min(0), Tooltip("묶음 안에서 어깨의 중간 빛이 나가는 차례")] private int middleTurn = 3;
		[SerializeField, Min(0), Tooltip("묶음 안에서 어깨의 굵은 빛이 나가는 차례")] private int heavyTurn = 5;
		[SerializeField, Tooltip("빛의 굵기 (팔, 어깨 중간, 어깨 굵은)")] private Vector3 layerScales = new(0.62f, 1f, 1.5f);
		[SerializeField, Min(0f), Tooltip("총구 섬광 배율. 총열을 덮지 않게")] private float armMuzzleScale = 0.45f;
		[SerializeField, Min(0f), Tooltip("첫 발에 몸에서 터지는 빛")] private float bodyBurst = 0.9f;
		[SerializeField, Min(0f)] private float bodyBurstShake = 0.35f;
		[SerializeField, Min(0f)] private float heavyShake = 0.15f;
		[SerializeField, Min(0f), Tooltip("마지막 발 뒤 무장이 남는 길이. 막타 빛줄기가 나갈 때까지 보임")] private float armLinger = 0.6f;
		[SerializeField, Tooltip("막타 빛줄기가 나가는 자리 (앞, 위)")] private Vector2 armHeroOffset = new(1.3f, 0.9f);

		private bool bodyBurstPending;

		// 무장이 보이는 동안. 손에 든 무기 그림을 감춤
		public bool IsArmed => InArms && armsModel.IsShown;

		private bool InArms => formation == Formation.Arms;

		/// <summary> 등 뒤 시점. 무장을 인물 뒤로 밀지 않음 </summary>
		public void SetBackView(bool on)
		{
			if (InArms)
				armsModel.BackView = on;
		}

		private void RaiseArms(Func<Vector3> aimPoint)
		{
			if (armsModel == null)
				throw new InvalidOperationException($"{name}: 몸 배치인데 무장 모델 미연결");
			armsModel.Raise(owner, aimSource, aimPoint);
			bodyBurstPending = true;
			Take(raised, armCount);
			decor.Clear();
			for (int i = 0; i < raised.Count; i++)
			{
				Weapon weapon = raised[i];
				weapon.Decor = false;
				weapon.Scale = 1f;
				weapon.Arm = i;
				weapon.Fan = 0f;
				weapon.AppearTime = Time.time;
			}
		}

		private (Weapon weapon, float scale) ArmShot(int shotIndex)
		{
			int turn = shotIndex % shotPattern;
			int side = shotIndex / shotPattern % 2;
			if (turn == heavyTurn)
				return (raised[armHands + side], layerScales.z);
			if (turn == middleTurn)
				return (raised[armHands + (1 - side)], layerScales.y);
			return (raised[shotIndex % armHands], layerScales.x);
		}

		private void BodyBurst()
		{
			bodyBurstPending = false;
			Vector3 chest = owner.position + Vector3.up * shotHeight;
			bursts.Add(new Burst { Position = chest, Forward = Vector3.up, Ground = owner.position.y + groundLift, StartTime = Time.time, Seed = Random.value, Scale = bodyBurst });
			for (int i = 0; i < sparksPerImpact * 2; i++)
				UltimateFxStage.Emit(sparks, chest, (Vector3.up * 0.5f + Random.onUnitSphere).normalized * Random.Range(4f, 10f), Random.Range(0.08f, 0.2f), Random.Range(0.25f, 0.6f), Color.white);
			CameraManager.Instance.GenerateCameraImpulse(bodyBurstShake);
		}

		// 빛줄기는 첫 발부터 마지막 발까지. 끊긴 포화는 남은 무장을 거둠
		private void DriveArms()
		{
			bool running = Time.time <= Mathf.Max(raiseTime + WindupSeconds, lastShotTime) + orphanSeconds;
			armsModel.SetFiring(running && bodyBurstPending == false && shotsLeft > 0);
			if (running == false && shotsLeft > 0)
			{
				shotsLeft = 0;
				armsModel.Dismiss(0f, 0f);
			}
			foreach (Weapon weapon in weapons)
				weapon.Alive = IsArmed;
		}
	}
}
