using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 몸 배치. 광석 총 네 자루 (두 손, 두 어깨) 가 몸에 붙어 조준을 따라 돌고, 날아가는 것은 빛뿐
	// - 박자: 총이 맺힘, 멈춤, 몸에서 터지는 빛과 첫 발, 훑는 포화, 막타
	// - 세 층: 손의 가는 줄기 (그림만, 쉬지 않고), 어깨의 중간 빛, 여섯 발마다 굵은 빛. 피해는 탄이 하므로 층은 그림만 나눔
	// - 문 배치 (VolleyGateObject 프리팹) 와 줄기, 맞은 자리, 바닥, 막타를 같이 씀
	public sealed partial class VolleyArsenal
	{
		private enum Formation
		{
			// 허공의 문에서 빛이 나감
			Gates = 0,
			// 몸에 붙은 총
			Arms = 1,
		}

		[Header("몸 배치")]
		[SerializeField] private Formation formation = Formation.Gates;
		[SerializeField, Tooltip("조준 기준 자리 (옆, 위, 앞). 앞의 둘은 손, 뒤의 둘은 어깨")] private Vector3[] armSlots = { new(0.62f, 0.5f, 0.2f), new(-0.62f, 0.5f, 0.2f), new(0.92f, 1f, -0.1f), new(-0.92f, 1f, -0.1f) };
		[SerializeField, Min(1), Tooltip("armSlots 가운데 손의 수 (나머지는 어깨)")] private int armHands = 2;
		[SerializeField, Min(1), Tooltip("몇 발을 한 묶음으로 셈")] private int shotPattern = 6;
		[SerializeField, Min(0), Tooltip("묶음 안에서 어깨의 중간 빛이 나가는 차례")] private int middleTurn = 3;
		[SerializeField, Min(0), Tooltip("묶음 안에서 어깨의 굵은 빛이 나가는 차례")] private int heavyTurn = 5;
		[SerializeField, Tooltip("앞쪽 총의 그리기 차례. 인물 그림과 손보다 앞")] private int armFrontOrder = 5;
		[SerializeField, Min(1f), Tooltip("월드 1 에 들어가는 그림 칸. 인물 그림과 같은 밀도")] private float armTexels = 32f;
		[SerializeField, Range(0f, 1f), Tooltip("그림의 뒤끝에서 자리까지")] private float armGrip = 0.3f;
		[SerializeField, Min(0.02f)] private float armPopSeconds = 0.2f;
		[SerializeField, Min(0f)] private float armRecoil = 0.3f;
		[SerializeField, Min(0f), Tooltip("총이 조준을 따라 도는 빠르기 (도/초)")] private float armTurn = 540f;
		[SerializeField, Min(0.02f), Tooltip("손의 가는 줄기 간격")] private float streamInterval = 0.1f;
		[SerializeField, Range(0f, 90f)] private float streamFan = 16f;
		[SerializeField, Tooltip("빛의 굵기 (손, 어깨 중간, 어깨 굵은)")] private Vector3 layerScales = new(0.62f, 1f, 1.5f);
		[SerializeField, Min(0f), Tooltip("첫 발에 몸에서 터지는 빛")] private float bodyBurst = 1.5f;
		[SerializeField, Min(0f)] private float bodyBurstShake = 0.35f;
		[SerializeField, Min(0f)] private float heavyShake = 0.15f;
		[SerializeField, Tooltip("막타 빛줄기가 나가는 자리 (앞, 위)")] private Vector2 armHeroOffset = new(1.3f, 0.9f);

		private FxQuadBatch armBehind;
		private FxQuadBatch armFront;
		private Vector3 armAim;
		private float armSign = 1f;
		private float streamCarry;
		private bool bodyBurstPending;

		// 몸에 붙은 총이 보이는 동안. 손에 든 무기 그림을 감춤
		public bool IsArmed { get; private set; }

		private bool InArms => formation == Formation.Arms;

		private float ArmLength => art.BigGun.width / armTexels;

		private void PrepareArms()
		{
			armBehind = Layer("ArmBehind", art.BigGun, 2976);
			armFront = new FxQuadBatch("VolleyArmFront", root, art.Shader, art.BigGun, art.Noise, 3003, false, armFrontOrder);
			batches.Add(armFront);
		}

		private void RaiseArms()
		{
			armAim = Aim();
			bodyBurstPending = true;
			streamCarry = 0f;
			Take(raised, armSlots.Length);
			decor.Clear();
			for (int i = 0; i < raised.Count; i++)
			{
				Weapon weapon = raised[i];
				weapon.Decor = false;
				weapon.Scale = 1f;
				weapon.Arm = i;
				weapon.Fan = 0f;
				weapon.AppearTime = Time.time + raiseSeconds * i / Mathf.Max(1, raised.Count - 1);
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

		private void PoseArm(Weapon weapon, out Vector3 position, out Vector3 aim)
		{
			Vector3 viewForward = viewCamera.transform.forward;
			Vector3 slot = armSlots[weapon.Arm];
			Vector3 side = Vector3.Cross(Vector3.up, armAim);
			float bob = bobHeight * Mathf.Sin(Time.time * bobSpeed + weapon.Phase);
			aim = (armAim - viewForward * (Vector3.Dot(armAim, viewForward) * flattenAim)).normalized;
			float kick = Time.time >= weapon.FireTime ? Mathf.Pow(0.5f, (Time.time - weapon.FireTime) / flashHalfLife) : 0f;
			position = owner.position + side * slot.x + Vector3.up * (slot.y + bob) + armAim * slot.z - aim * (armRecoil * kick);
		}

		private void DrawArms()
		{
			Vector3 viewForward = viewCamera.transform.forward;
			armAim = Vector3.RotateTowards(armAim, Aim(), armTurn * Mathf.Deg2Rad * Time.deltaTime, 0f);
			IsArmed = false;
			foreach (Weapon weapon in weapons)
			{
				if (weapon.Alive == false || Time.time < weapon.AppearTime)
					continue;
				if (Time.time > Mathf.Max(raiseTime + WindupSeconds, lastShotTime) + orphanSeconds)
					weapon.BreakTime = Mathf.Min(weapon.BreakTime, Time.time);
				float broken = Mathf.Clamp01((Time.time - weapon.BreakTime) / breakSeconds);
				if (broken >= 1f)
				{
					weapon.Alive = false;
					continue;
				}
				IsArmed = true;
				DrawArm(weapon, viewForward, broken);
			}
			Stream();
		}

		private void DrawArm(Weapon weapon, Vector3 viewForward, float broken)
		{
			PoseArm(weapon, out Vector3 position, out Vector3 aim);
			float age = Time.time - weapon.AppearTime;
			float born = Mathf.Clamp01(age / armPopSeconds);
			float kick = Time.time >= weapon.FireTime ? Mathf.Pow(0.5f, (Time.time - weapon.FireTime) / flashHalfLife) : 0f;
			float heat = Mathf.Max(Mathf.Pow(0.5f, age / 0.1f), kick * 0.5f);
			float length = ArmLength * Back(born);
			Vector3 up = Vector3.Cross(viewForward, aim).normalized * armSign;
			float lift = Vector3.Dot(up, viewCamera.transform.up);
			// 조준이 화면 위아래에 가까우면 마지막 방향을 지킴
			if (lift < -0.15f)
			{
				armSign = -armSign;
				up = -up;
			}
			Vector3 half = aim * (length * 0.5f);
			Vector3 center = position + aim * (length * (0.5f - armGrip));
			bool behind = Vector3.Dot(position - owner.position, Vector3.ProjectOnPlane(viewForward, Vector3.up)) > 0f;
			FxQuadLook look = FxQuadLook.Of(Color.Lerp(Color.white, hotTint, heat + broken), 1f - Mathf.Clamp01(broken * 2.5f), Mathf.Max(1f - born, broken), weapon.Phase);
			(behind ? armBehind : armFront).Add(center, half, up * (length * 0.5f * art.BigGun.height / art.BigGun.width), look);
			Vector3 muzzle = position + aim * (length * (1f - armGrip));
			float breath = 0.5f + 0.15f * Mathf.Sin(Time.time * 7f + weapon.Phase);
			Billboard(flashSoft, muzzle, 0.3f + 0.5f * kick, 0f, FxQuadLook.Of(Scale(goldTint, (breath * 0.5f + kick) * (1f - broken))));
		}

		// 손 두 자루가 쉬지 않고 쏘는 가는 줄기. 첫 발부터 막타까지
		private void Stream()
		{
			bool firing = bodyBurstPending == false && shotsLeft > 0 && Time.time < lastShotTime + orphanSeconds;
			if (firing == false)
				return;
			streamCarry += Time.deltaTime / streamInterval;
			while (streamCarry >= 1f)
			{
				streamCarry -= 1f;
				Weapon hand = raised[Random.Range(0, armHands)];
				hand.FireTime = Time.time;
				Vector3 direction = Quaternion.Euler(0f, Random.Range(-0.5f, 0.5f) * streamFan, 0f) * armAim;
				phantoms.Add(new Phantom
				{
					From = Muzzle(hand, out _),
					To = owner.position + Vector3.up * shotHeight + direction * Random.Range(phantomReach.x, phantomReach.y),
					StartTime = Time.time,
					Seed = Random.value,
					Row = Random.value < 0.5f ? 0 : 1,
					Pale = Random.value < 0.5f,
				});
			}
		}
	}
}
