using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace WitchMendokusai
{
	// 일제 사격의 몸 배치. 두 손에 작은 광석 총, 두 어깨 뒤에 큰 광석 총. 몸에 붙어 조준을 따라 돌고, 날아가는 것은 빛뿐
	// - 어깨 총은 늘 인물 뒤라 얼굴과 몸을 가리지 않음. 손 총은 손 자리 (화면 기준) 를 쥠
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
		[SerializeField, Tooltip("앞의 둘은 손 (화면 옆, 위, 카메라 쪽), 뒤의 둘은 어깨 (조준 기준 옆, 위, 앞)")] private Vector3[] armSlots = { new(0.3f, 0.25f, 0.14f), new(-0.3f, 0.25f, 0.14f), new(0.7f, 1.25f, -0.1f), new(-0.7f, 1.25f, -0.1f) };
		[SerializeField, Tooltip("조준 반대쪽 손이 옮겨 가는 거리 (조준 쪽, 위). 몸 뒤에서 총구가 나옴")] private Vector2 offHandReach = new(0.3f, 0.2f);
		[SerializeField, Range(0f, 1f), Tooltip("이보다 반대쪽이면 손 총을 몸 뒤에 그림")] private float offHandBehind = 0.5f;
		[SerializeField, Tooltip("손 총 그림에서 손잡이를 쥐는 칸 (왼쪽 위에서)")] private Vector2 handGrip = new(3f, 13f);
		[SerializeField, Range(0f, 1f), Tooltip("어깨 총의 밀림에 대한 손 총의 밀림")] private float handRecoil = 0.35f;
		[SerializeField, Min(1), Tooltip("armSlots 가운데 손의 수 (나머지는 어깨)")] private int armHands = 2;
		[SerializeField, Min(1), Tooltip("몇 발을 한 묶음으로 셈")] private int shotPattern = 6;
		[SerializeField, Min(0), Tooltip("묶음 안에서 어깨의 중간 빛이 나가는 차례")] private int middleTurn = 3;
		[SerializeField, Min(0), Tooltip("묶음 안에서 어깨의 굵은 빛이 나가는 차례")] private int heavyTurn = 5;
		[SerializeField, Min(1f), Tooltip("월드 1 에 들어가는 그림 칸. 인물 그림과 같은 밀도")] private float armTexels = 32f;
		[SerializeField, Range(0f, 1f), Tooltip("그림의 뒤끝에서 자리까지")] private float armGrip = 0.3f;
		[SerializeField, Min(0.02f)] private float armPopSeconds = 0.2f;
		[SerializeField, Min(0f)] private float armRecoil = 0.3f;
		[SerializeField, Min(0f), Tooltip("총이 조준을 따라 도는 빠르기 (도/초)")] private float armTurn = 540f;
		[SerializeField, Min(0.02f), Tooltip("손의 가는 줄기 간격")] private float streamInterval = 0.05f;
		[SerializeField, Range(0f, 90f)] private float streamFan = 24f;
		[SerializeField, Min(0.1f), Tooltip("문 배치의 곁줄기에 대한 굵기")] private float streamScale = 1.7f;
		[SerializeField, Tooltip("빛의 굵기 (손, 어깨 중간, 어깨 굵은)")] private Vector3 layerScales = new(0.62f, 1f, 1.5f);
		[SerializeField, Min(0f), Tooltip("첫 발에 몸에서 터지는 빛")] private float bodyBurst = 1.5f;
		[SerializeField, Min(0f)] private float bodyBurstShake = 0.35f;
		[SerializeField, Min(0f)] private float heavyShake = 0.15f;
		[SerializeField, Min(0f), Tooltip("마지막 발 뒤 총이 남는 길이. 막타 빛줄기가 나갈 때까지 보임")] private float armLinger = 0.6f;
		[SerializeField, Tooltip("막타 빛줄기가 나가는 자리 (앞, 위)")] private Vector2 armHeroOffset = new(1.3f, 0.9f);

		private FxQuadBatch shoulderBatch;
		private FxQuadBatch handBehind;
		private FxQuadBatch handFront;
		private Vector3 armAim;
		private float armSign = 1f;
		private float streamCarry;
		private bool bodyBurstPending;

		// 몸에 붙은 총이 보이는 동안. 손에 든 무기 그림을 감춤
		public bool IsArmed { get; private set; }

		private bool InArms => formation == Formation.Arms;

		private bool IsHand(Weapon weapon) => weapon.Arm < armHands;

		private Texture2D ArmArt(Weapon weapon) => IsHand(weapon) ? art.Gun : art.BigGun;

		private float ArmLength(Weapon weapon) => ArmArt(weapon).width / armTexels;

		private float ArmGrip(Weapon weapon) => IsHand(weapon) ? handGrip.x / art.Gun.width : armGrip;

		// 손 총은 손잡이가 총신보다 아래. 그만큼 총을 올림
		private float ArmLift(Weapon weapon) => IsHand(weapon) ? (handGrip.y - art.Gun.height * 0.5f) / armTexels : 0f;

		private void PrepareArms()
		{
			shoulderBatch = Layer("ArmShoulder", art.BigGun, 2976);
			handBehind = Layer("ArmHandBehind", art.Gun, 2977);
			// 인물 그림 (큐 3000) 앞, 손 그림 뒤
			handFront = Layer("ArmHandFront", art.Gun, 3002);
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
			Transform cameraTransform = viewCamera.transform;
			Vector3 viewForward = cameraTransform.forward;
			Vector3 slot = armSlots[weapon.Arm];
			float bob = bobHeight * Mathf.Sin(Time.time * bobSpeed + weapon.Phase);
			float kick = ArmKick(weapon);
			aim = (armAim - viewForward * (Vector3.Dot(armAim, viewForward) * flattenAim)).normalized;
			if (IsHand(weapon))
			{
				float off = OffHand(weapon);
				Vector3 right = cameraTransform.right;
				Vector3 up = Vector3.Slerp(Vector3.up, cameraTransform.up, faceCamera).normalized;
				Vector3 away = Vector3.Cross(right, up).normalized;
				position = owner.position + right * slot.x + up * (slot.y + offHandReach.y * off) - away * slot.z + aim * (offHandReach.x * off - armRecoil * handRecoil * kick);
				return;
			}
			position = owner.position + Vector3.Cross(Vector3.up, armAim) * slot.x + Vector3.up * (slot.y + bob) + armAim * slot.z - aim * (armRecoil * kick);
		}

		private float ArmKick(Weapon weapon) => Time.time >= weapon.FireTime ? Mathf.Pow(0.5f, (Time.time - weapon.FireTime) / flashHalfLife) : 0f;

		// 조준의 반대쪽 손이면 1 에 가까움. 화면 위아래 조준은 0
		private float OffHand(Weapon weapon) => Mathf.Clamp01(-Mathf.Sign(armSlots[weapon.Arm].x) * Vector3.Dot(armAim, viewCamera.transform.right));

		private Vector3 ArmUp(Vector3 aim) => Vector3.Cross(viewCamera.transform.forward, aim).normalized * armSign;

		private Vector3 ArmMuzzle(Weapon weapon, Vector3 position, Vector3 aim) => position + aim * (ArmLength(weapon) * (1f - ArmGrip(weapon))) + ArmUp(aim) * ArmLift(weapon);

		// 총이 거꾸로 서면 뒤집음. 조준이 화면 위아래에 가까우면 마지막 방향을 지킴
		private void TurnOver()
		{
			if (Vector3.Dot(ArmUp(armAim), viewCamera.transform.up) < -0.15f)
				armSign = -armSign;
		}

		private void DrawArms()
		{
			armAim = Vector3.RotateTowards(armAim, Aim(), armTurn * Mathf.Deg2Rad * Time.deltaTime, 0f);
			TurnOver();
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
				DrawArm(weapon, broken);
			}
			Stream();
		}

		private void DrawArm(Weapon weapon, float broken)
		{
			PoseArm(weapon, out Vector3 position, out Vector3 aim);
			float age = Time.time - weapon.AppearTime;
			float born = Mathf.Clamp01(age / armPopSeconds);
			float kick = ArmKick(weapon);
			float heat = Mathf.Max(Mathf.Pow(0.5f, age / 0.1f), kick * 0.5f);
			Texture2D picture = ArmArt(weapon);
			float length = ArmLength(weapon) * Back(born);
			Vector3 up = ArmUp(aim);
			Vector3 center = position + aim * (length * (0.5f - ArmGrip(weapon))) + up * ArmLift(weapon);
			FxQuadLook look = FxQuadLook.Of(Color.Lerp(Color.white, hotTint, heat + broken), 1f - Mathf.Clamp01(broken * 2.5f), Mathf.Max(1f - born, broken), weapon.Phase);
			FxQuadBatch batch = IsHand(weapon) == false ? shoulderBatch : OffHand(weapon) > offHandBehind ? handBehind : handFront;
			batch.Add(center, aim * (length * 0.5f), up * (length * 0.5f * picture.height / picture.width), look);
			float breath = 0.5f + 0.15f * Mathf.Sin(Time.time * 7f + weapon.Phase);
			Billboard(flashSoft, ArmMuzzle(weapon, position, aim), 0.3f + 0.5f * kick, 0f, FxQuadLook.Of(Scale(goldTint, (breath * 0.5f + kick) * (1f - broken))));
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
					Scale = streamScale,
				});
			}
		}
	}
}
