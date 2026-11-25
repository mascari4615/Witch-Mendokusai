using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 알리사 일제 사격의 그림. 소용돌이 문에서 완드가 밀려 나오고, 한 자루가 한 발씩 쏘고 타서 사라짐
	// - 판정 없음. 피해는 VolleyGauge 가 내는 탄이 함. 줄기는 그 탄의 머리를 따라가 그림과 피해 시점이 어긋나지 않음
	// - 그린 텍스처 (PaintedFxSet) 를 겹겹이 쌓음. 텍스처마다 FxQuadBatch 하나라 그리기는 층 수만큼
	// - 게임 시간으로 흐름 (카드 창에서 같이 멈춤). 만든 물체는 이 오브젝트의 자식 (풀과 같이 씬을 넘어 살아남음)
	// - 그림은 LateUpdate 에서만 그림. Raise 와 Fire 는 예약만 해서 부른 쪽 (발사 코루틴) 을 끊지 않음
	// - 이 파일: 꺼냄, 문, 완드. 줄기와 총구와 맞은 자리는 .Shots.cs, 바닥과 어둠은 .Ground.cs
	public sealed partial class VolleyArsenal : MonoBehaviour
	{
		private const float TAU = Mathf.PI * 2f;
		private const float GOLDEN_STEP = 0.618034f;

		[SerializeField] private PaintedFxSet art;

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color lightTint = new(1f, 0.98f, 0.95f);
		[SerializeField, ColorUsage(false, true)] private Color hotTint = new(1.4f, 1.36f, 1.32f);
		[SerializeField, Tooltip("빛 뒤에 까는 어둠")] private Color shadeTint = new(0.03f, 0.021f, 0.008f, 1f);
		[SerializeField, Tooltip("뒤쪽에 깔리는 작은 자루")] private Color decorTint = new(0.62f, 0.54f, 0.36f, 1f);
		[SerializeField, Tooltip("줄기의 두 색")] private Color goldTint = new(1f, 0.82f, 0.28f, 1f);
		[SerializeField] private Color paleTint = new(1f, 0.97f, 0.86f, 1f);
		[SerializeField] private Color portalTint = new(0.62f, 0.58f, 0.48f, 1f);
		[SerializeField] private Color lightColor = new(1f, 0.92f, 0.78f, 1f);

		[Header("꺼냄")]
		[SerializeField, Min(0f), Tooltip("첫 자루에서 마지막 자루까지 나오는 길이")] private float raiseSeconds = 0.36f;
		[SerializeField, Min(0f), Tooltip("다 나온 뒤 첫 발까지 멈춤")] private float holdSeconds = 0.22f;
		[SerializeField, Min(0.02f), Tooltip("문이 열리는 길이")] private float openSeconds = 0.2f;
		[SerializeField, Min(0f), Tooltip("문이 열리기 시작한 뒤 완드가 나오기까지")] private float emergeDelay = 0.07f;
		[SerializeField, Min(0.02f)] private float emergeSeconds = 0.18f;
		[SerializeField, Min(0.05f), Tooltip("막타 뒤 완드와 문이 닫히는 길이")] private float breakSeconds = 0.24f;
		[SerializeField, Min(0.1f), Tooltip("막타가 안 오면 (끊긴 포화) 마지막 발 뒤 이만큼 지나 닫힘")] private float orphanSeconds = 1.5f;
		[SerializeField, Min(1), Tooltip("쏘는 자루 수 상한")] private int maxWeapons = 32;
		[SerializeField, Min(0), Tooltip("쏘지 않고 뒤에 둘러선 자루")] private int decorWeapons = 16;
		[SerializeField, Range(0.1f, 1f)] private float decorScale = 0.46f;
		[SerializeField, Tooltip("쏘는 자루의 크기. 차례로 돌아가며 씀. 가장 큰 것은 마지막 완드 그림")] private float[] sizeTiers = { 1.35f, 0.72f, 1f };

		[Header("배치 (월드)")]
		[SerializeField, Tooltip("가장 안쪽과 바깥의 반지름")] private Vector2 radiusRange = new(2.4f, 5f);
		[SerializeField, Min(0.1f), Tooltip("가로로 늘림")] private float spreadWide = 1.3f;
		[SerializeField, Min(1f), Tooltip("완드가 겨누는 자리 (인물 앞 거리)")] private float convergeDistance = 7f;
		[SerializeField, Range(10f, 180f), Tooltip("늘어서는 호의 절반 (도). 가운데는 위")] private float arcHalf = 74f;
		[SerializeField, Range(0f, 90f), Tooltip("겨누는 쪽을 비우려고 호를 반대로 기울이는 각도")] private float arcLean = 52f;
		[SerializeField] private float centerHeight = 1.2f;
		[SerializeField, Tooltip("인물 뒤로")] private float behind = 0.5f;
		[SerializeField, Range(0f, 1f), Tooltip("배치 판을 카메라 쪽으로 눕히는 정도")] private float faceCamera = 0.55f;
		[SerializeField, Tooltip("완드 길이의 범위")] private Vector2 weaponLength = new(2.6f, 3.2f);
		[SerializeField, Tooltip("완드 그림의 가로 나누기 세로 (Wands 순서)")] private float[] weaponAspects = { 2.27f, 2.47f, 2.97f, 1.88f };
		[SerializeField, Range(0.1f, 1f), Tooltip("다 나왔을 때 문 밖으로 보이는 비율")] private float emergeFraction = 0.82f;
		[SerializeField, Min(0.05f)] private float portalSize = 0.78f;
		[SerializeField, Min(0.1f), Tooltip("자루 크기에 곱하는 문 크기")] private float portalScale = 1.15f;
		[SerializeField, Min(0f), Tooltip("자루끼리 떨어지는 거리 (크기 1 기준)")] private float slotGap = 1.75f;
		[SerializeField, Tooltip("밀려난 자루의 가장 낮은 자리")] private float slotFloor = -0.6f;
		[SerializeField, Range(0f, 1f), Tooltip("완드가 화면 안쪽을 겨눌 때 눌려 보이지 않게 화면 쪽으로 눕히는 정도")] private float flattenAim = 0.75f;
		[SerializeField, Min(0f), Tooltip("문을 카메라 쪽으로 돌리는 세기")] private float portalFacing = 1.1f;
		[SerializeField, Tooltip("문이 도는 빠르기 (라디안/초)")] private float portalSpin = 3.4f;
		[SerializeField, Tooltip("가만히 있을 때와 열리고 쏠 때의 감김")] private Vector2 portalSwirl = new(1.2f, 6f);
		[SerializeField, Range(0f, 1f), Tooltip("쏠 때 문 안으로 밀려 들어가는 비율")] private float recoil = 0.3f;
		[SerializeField, Min(0.01f), Tooltip("밀림과 번쩍임이 절반이 되는 길이")] private float flashHalfLife = 0.07f;
		[SerializeField, Min(0f)] private float overshoot = 1.9f;
		[SerializeField, Min(0f)] private float bobHeight = 0.06f;
		[SerializeField, Min(0f)] private float bobSpeed = 4f;

		private sealed class Weapon
		{
			public Vector2 Slot;
			public float Length;
			public float Scale;
			// 부채 안에서 제 몫의 각도 (도). 쏘기 전부터 그쪽을 겨눔
			public float Fan;
			public bool Decor;
			// 막타 무렵 완드째 날아가 문만 남음
			public bool Launched;
			public float Phase;
			public int Kind;
			public float AppearTime;
			public float FireTime;
			// 이때부터 닫힘
			public float BreakTime;
			public bool Alive;
		}

		private readonly List<Weapon> weapons = new();
		// 지금 포화가 쓰는 자루. 앞 포화에서 타는 중인 자루는 그대로 두고 다른 자루를 씀
		private readonly List<Weapon> raised = new();
		private readonly List<Weapon> decor = new();
		private readonly List<Weapon> arranged = new();
		private readonly List<FxQuadBatch> batches = new();
		private readonly List<UnityEngine.Object> owned = new();
		private readonly List<(Weapon weapon, Transform shot)> pendingShots = new();
		private Transform root;
		private Camera viewCamera;
		private Transform owner;
		private Func<Vector3> aimSource;
		private FxQuadBatch glowBehind;
		private FxQuadBatch[] portalBatches;
		private FxQuadBatch portalFront;
		private FxQuadBatch[] wandBatches;
		private int shotsLeft;
		private float arcCenter = 90f;

		public float WindupSeconds => raiseSeconds + holdSeconds;

		// 자루를 꺼내기 시작. 첫 발은 WindupSeconds 뒤 호출한 쪽이 Fire 로
		public void Raise(Transform ownerTransform, Func<Vector3> aim, int count, float fanDegrees)
		{
			Prepare();
			owner = ownerTransform;
			aimSource = aim;
			shotsLeft = count;
			// 겨누는 쪽 앞은 비움
			arcCenter = 90f + arcLean * Mathf.Clamp(Vector3.Dot(aim(), Camera.main.transform.right), -1f, 1f);
			finaleTime = float.PositiveInfinity;
			heroTime = float.PositiveInfinity;
			Take(raised, Mathf.Clamp(count, 1, maxWeapons));
			for (int i = 0; i < raised.Count; i++)
			{
				Weapon weapon = raised[i];
				weapon.Decor = false;
				weapon.Scale = sizeTiers[i % sizeTiers.Length];
				weapon.Kind = weapon.Scale > 1f ? wandBatches.Length - 1 : i % (wandBatches.Length - 1);
				weapon.Slot = SlotFor(i, raised.Count);
				// 안쪽부터 바깥으로
				weapon.AppearTime = Time.time + raiseSeconds * i / Mathf.Max(1, raised.Count - 1);
			}
			int stride = VolleyGaugeRules.SpreadStride(raised.Count);
			for (int shot = 0; shot < raised.Count; shot++)
				raised[shot * stride % raised.Count].Fan = VolleyGaugeRules.FanAngle(shot, raised.Count, fanDegrees);
			Take(decor, decorWeapons);
			for (int i = 0; i < decor.Count; i++)
			{
				Weapon weapon = decor[i];
				weapon.Decor = true;
				weapon.Scale = decorScale;
				weapon.Kind = i % (wandBatches.Length - 1);
				weapon.Slot = DecorSlot(i);
				weapon.Fan = VolleyGaugeRules.FanAngle(decor.Count - 1 - i, decor.Count, fanDegrees);
				weapon.AppearTime = Time.time + raiseSeconds * Mathf.Repeat(i * GOLDEN_STEP, 1f);
			}
			SpreadSlots();
			RaiseGround();
		}

		// 쉬는 자루를 가져오고 모자라면 만듦
		private void Take(List<Weapon> into, int wanted)
		{
			into.Clear();
			foreach (Weapon free in weapons)
			{
				if (free.Alive == false && into.Count < wanted)
					into.Add(free);
			}
			while (into.Count < wanted)
			{
				Weapon made = new()
				{
					Length = Mathf.Lerp(weaponLength.x, weaponLength.y, Mathf.Repeat(weapons.Count * GOLDEN_STEP, 1f)),
					Phase = weapons.Count * 1.7f,
				};
				weapons.Add(made);
				into.Add(made);
			}
			foreach (Weapon taken in into)
			{
				taken.Alive = true;
				taken.FireTime = float.PositiveInfinity;
				taken.Launched = false;
				taken.BreakTime = float.PositiveInfinity;
			}
		}

		// 한 발. 쏘는 자루를 골라 총구를 터뜨리고, 줄기가 탄을 따라가게 함
		public void Fire(int shotIndex, GameObject shot)
		{
			// 옆 자루가 잇달아 쏘지 않게 서로소 걸음으로 섞음. 자루보다 발이 많으면 사라진 자루가 다시 나와 쏨
			Weapon weapon = raised[shotIndex * VolleyGaugeRules.SpreadStride(raised.Count) % raised.Count];
			weapon.FireTime = Time.time;
			weapon.Alive = true;
			pendingShots.Add((weapon, shot.transform));
		}

		private void FirePending()
		{
			foreach ((Weapon weapon, Transform shot) in pendingShots)
			{
				Vector3 muzzle = Muzzle(weapon, out Vector3 aim);
				MuzzleFlash(muzzle, aim);
				shotsLeft--;
				bool last = shotsLeft < finaleShots;
				weapon.Launched = last;
				if (shot != null)
					StartTracer(muzzle, shot, last ? finaleScale : 1f, last ? weapon.Length * weapon.Scale : 0f);
				SpawnPhantoms(Mathf.RoundToInt(Mathf.Lerp(phantomsPerShot.x, phantomsPerShot.y, 1f - shotsLeft / (float)raised.Count)));
				lightLevel = 1f;
				lastShotTime = Time.time;
				if (shotsLeft == finaleShots - 1)
					RaiseHero();
				if (shotsLeft == 0)
					StartFinale();
			}
			pendingShots.Clear();
		}

		private void OnDisable()
		{
			foreach (Weapon weapon in weapons)
				weapon.Alive = false;
			pendingShots.Clear();
			StopShots();
			StopGround();
			foreach (FxQuadBatch batch in batches)
				batch.Clear();
		}

		private void OnDestroy()
		{
			foreach (FxQuadBatch batch in batches)
				batch.Destroy();
			foreach (UnityEngine.Object target in owned)
				Destroy(target);
		}

		private void LateUpdate()
		{
			if (root == null)
				return;
			viewCamera = Camera.main;
			// 무기 오브젝트는 베기마다 자리를 옮김. 그림은 월드 자리라 뿌리를 원점에 되돌림
			root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			// 씬이 바뀌는 사이에는 카메라나 인물이 없음. 그동안 감추고 예약을 버림
			bool visible = owner != null && viewCamera != null;
			root.gameObject.SetActive(visible);
			if (visible == false)
			{
				pendingShots.Clear();
				return;
			}
			foreach (FxQuadBatch batch in batches)
				batch.Begin();
			FirePending();
			DrawGround();
			DrawWeapons();
			DrawShots();
			foreach (FxQuadBatch batch in batches)
				batch.Commit();
		}

		private void DrawWeapons()
		{
			Vector3 viewForward = viewCamera.transform.forward;
			foreach (Weapon weapon in weapons)
			{
				if (weapon.Alive == false || Time.time < weapon.AppearTime)
					continue;
				float age = Time.time - weapon.AppearTime;
				float sinceFire = Time.time - weapon.FireTime;
				if (Time.time > Mathf.Max(raiseTime + WindupSeconds, lastShotTime) + orphanSeconds)
					weapon.BreakTime = Mathf.Min(weapon.BreakTime, Time.time);
				float broken = Mathf.Clamp01((Time.time - weapon.BreakTime) / breakSeconds);
				if (broken >= 1f)
				{
					weapon.Alive = false;
					continue;
				}
				Pose(weapon, out Vector3 position, out Vector3 aim);
				float kick = sinceFire >= 0f ? Mathf.Pow(0.5f, sinceFire / flashHalfLife) : 0f;
				float birth = Mathf.Pow(0.5f, age / flashHalfLife);
				DrawPortal(weapon, position, aim, viewForward, age, kick, broken);
				DrawWand(weapon, position, aim, viewForward, age, kick, broken);
				// 나오는 순간과 쏘는 순간의 섬광. 완드 앞에
				float glint = Mathf.Max(birth, kick);
				if (glint > 0.03f && weapon.Decor == false)
					Billboard(flashSoft, position + aim * 0.2f, portalSize * weapon.Scale * (0.4f + 0.6f * glint), 0f, FxQuadLook.Of(Scale(lightTint, glint * 0.45f)));
			}
		}

		private void DrawPortal(Weapon weapon, Vector3 position, Vector3 aim, Vector3 viewForward, float age, float kick, float broken)
		{
			// 카메라 쪽으로 반쯤 돌려 옆에서도 둥글게 보임
			// 겨눔이 카메라 쪽이든 반대쪽이든 원판의 앞면이 보이게
			Vector3 toward = Vector3.Dot(aim, viewForward) > 0f ? -aim : aim;
			Vector3 facing = (toward - viewForward * portalFacing).normalized;
			Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
			Vector3 up = Vector3.Cross(facing, right);
			float open = Back(age / openSeconds);
			// 다 쓴 문은 안으로 닫힘
			float size = portalSize * weapon.Scale * portalScale * open * (1f + 0.25f * kick) * (1f - 0.85f * broken);
			float busy = Mathf.Max(Mathf.Pow(0.5f, age / 0.12f), kick);
			glowBehind.Add(position - facing * 0.05f, right * (size * 1.7f), up * (size * 1.7f), FxQuadLook.Of(WithAlpha(shadeTint, 0.85f * (1f - broken) * Mathf.Clamp01(open)), 1f));
			bool swirls = weapon.Kind % portalBatches.Length == 0;
			FxQuadBatch batch = portalBatches[weapon.Kind % portalBatches.Length];
			FxQuadLook look = FxQuadLook.Of(weapon.Decor ? decorTint : Color.Lerp(portalTint, lightTint, busy * 0.5f), 0.9f, Mathf.Max(1f - age / openSeconds, broken), weapon.Phase);
			look.Turn = Time.time * portalSpin * (swirls ? 1f : 0.4f) + weapon.Phase;
			look.Swirl = (swirls ? 1f : 0.25f) * (Mathf.Lerp(portalSwirl.x, portalSwirl.y, busy) + portalSwirl.y * broken);
			look.Distort = 0.02f;
			batch.Add(position, right * size, up * size, look);
			// 바깥으로 번지는 물결 한 겹
			if (weapon.Decor == false)
			{
				float ripple = Mathf.Repeat(Time.time * 1.3f + weapon.Phase * 0.31f, 1f);
				look.Color = WithAlpha(paleTint, 0.35f * Mathf.Sin(ripple * Mathf.PI) * (1f - broken));
				look.Cover = 0f;
				look.Dissolve = 0.35f + 0.5f * ripple;
				batch.Add(position, right * (size * (1f + 0.5f * ripple)), up * (size * (1f + 0.5f * ripple)), look);
			}
			// 완드가 잘린 자리를 덮는 빛
			portalFront.Add(position + aim * 0.12f, right * (size * 0.42f), up * (size * 0.42f), FxQuadLook.Of(Scale(weapon.Decor ? decorTint : goldTint, (0.5f + 0.5f * busy) * (1f - broken))));
		}

		private void DrawWand(Weapon weapon, Vector3 position, Vector3 aim, Vector3 viewForward, float age, float kick, float broken)
		{
			float shown = Mathf.Clamp01(Back((age - emergeDelay) / emergeSeconds) * emergeFraction * (1f - recoil * kick));
			if (age < emergeDelay || shown <= 0.02f || weapon.Launched)
				return;
			float full = weapon.Length * weapon.Scale;
			float length = full * shown;
			float width = full / weaponAspects[weapon.Kind];
			Vector3 tip = position + aim * length;
			float heat = Mathf.Max(Mathf.Pow(0.5f, (age - emergeDelay) / 0.1f), kick);
			// 사라질 때는 덮기를 거두고 빛으로 풀림 (찢긴 종이처럼 보이지 않게)
			Color tint = weapon.Decor ? decorTint : Color.Lerp(Color.white * 0.86f, hotTint, heat * 0.75f + broken * 1.2f);
			FxQuadLook look = FxQuadLook.Of(tint, 1f - Mathf.Clamp01(broken * 2.5f), broken, weapon.Phase);
			wandBatches[weapon.Kind].AddStrip(position, tip, width, viewForward, new Rect(1f - shown, 0f, shown, 1f), look);
			// 촉의 빛. 숨 쉬듯
			float breath = 0.5f + 0.25f * Mathf.Sin(Time.time * 7f + weapon.Phase);
			Billboard(flashSoft, position + aim * (length * 0.82f), 0.42f + 0.3f * heat, 0f, FxQuadLook.Of(Scale(lightTint, (breath + heat) * (weapon.Decor ? 0.15f : 0.5f) * (1f - broken))));
		}

		private void Pose(Weapon weapon, out Vector3 position, out Vector3 aim)
		{
			Transform cameraTransform = viewCamera.transform;
			Vector3 right = cameraTransform.right;
			Vector3 up = Vector3.Slerp(Vector3.up, cameraTransform.up, faceCamera).normalized;
			Vector3 away = Vector3.Cross(right, up).normalized;
			float bob = bobHeight * Mathf.Sin(Time.time * bobSpeed + weapon.Phase);
			position = owner.position + Vector3.up * centerHeight + right * weapon.Slot.x + up * (weapon.Slot.y + bob) + away * behind;
			Vector3 mark = Quaternion.Euler(0f, weapon.Fan, 0f) * Aim() * convergeDistance;
			aim = owner.position + Vector3.up * shotHeight + mark - position;
			Vector3 viewForward = cameraTransform.forward;
			aim = (aim - viewForward * (Vector3.Dot(aim, viewForward) * flattenAim)).normalized;
		}

		private Vector3 Muzzle(Weapon weapon, out Vector3 aim)
		{
			Pose(weapon, out Vector3 position, out aim);
			return position + aim * (weapon.Length * weapon.Scale * emergeFraction);
		}

		private Vector3 Aim() => aimSource();

		// 넘쳤다가 제자리로
		private float Back(float progress)
		{
			float left = Mathf.Clamp01(progress) - 1f;
			return 1f + (overshoot + 1f) * left * left * left + overshoot * left * left;
		}

		private void Billboard(FxQuadBatch batch, Vector3 position, float halfSize, float degrees, in FxQuadLook look) => Billboard(batch, position, halfSize, degrees, new Rect(0f, 0f, 1f, 1f), look);

		private void Billboard(FxQuadBatch batch, Vector3 position, float halfSize, float degrees, Rect uv, in FxQuadLook look)
		{
			Quaternion turn = viewCamera.transform.rotation * Quaternion.Euler(0f, 0f, degrees);
			batch.Add(position, turn * Vector3.right * halfSize, turn * Vector3.up * halfSize, uv, look);
		}

		private static Color Scale(Color color, float amount) => new(color.r * amount, color.g * amount, color.b * amount, color.a);

		private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);

		// 황금비 걸음으로 부채 안에 고르게 흩음. 같은 높이에 줄 서지 않음
		private Vector2 SlotFor(int index, int count)
		{
			float angle = (arcCenter + arcHalf * (Mathf.Repeat((index + 0.5f) * GOLDEN_STEP, 1f) * 2f - 1f)) * Mathf.Deg2Rad;
			float radius = Mathf.Lerp(radiusRange.x, radiusRange.y, Mathf.Sqrt((index + 0.5f) / count));
			return new Vector2(Mathf.Cos(angle) * spreadWide, Mathf.Sin(angle)) * radius;
		}

		// 겹친 자루를 조금씩 밀어냄
		private void SpreadSlots()
		{
			const int ROUNDS = 40;
			arranged.Clear();
			arranged.AddRange(raised);
			arranged.AddRange(decor);
			for (int round = 0; round < ROUNDS; round++)
			{
				for (int i = 0; i < arranged.Count; i++)
				{
					for (int j = i + 1; j < arranged.Count; j++)
						PushApart(arranged[i], arranged[j]);
				}
			}
		}

		private void PushApart(Weapon one, Weapon other)
		{
			Vector2 apart = one.Slot - other.Slot;
			float distance = Mathf.Max(0.01f, apart.magnitude);
			float wanted = slotGap * (one.Scale + other.Scale) * 0.5f;
			if (distance >= wanted)
				return;
			Vector2 push = apart * ((wanted - distance) / distance * 0.25f);
			one.Slot = new Vector2(one.Slot.x + push.x, Mathf.Max(slotFloor, one.Slot.y + push.y));
			other.Slot = new Vector2(other.Slot.x - push.x, Mathf.Max(slotFloor, other.Slot.y - push.y));
		}

		// 둘러선 자루는 바깥 테두리에 한 줄
		private Vector2 DecorSlot(int index)
		{
			float angle = (arcCenter + (arcHalf + 10f) * ((index + 0.5f) / decorWeapons * 2f - 1f)) * Mathf.Deg2Rad;
			return new Vector2(Mathf.Cos(angle) * spreadWide, Mathf.Sin(angle)) * (radiusRange.y * 1.12f);
		}

		private void Prepare()
		{
			if (root != null)
				return;
			if (art == null)
				throw new InvalidOperationException($"{name}: 일제 사격 그림 묶음 미연결");
			root = new GameObject(nameof(VolleyArsenal)).transform;
			root.SetParent(transform, false);
			PrepareGround();
			glowBehind = Layer("PortalShade", art.Soft, 2956);
			portalBatches = new FxQuadBatch[art.Portals.Length];
			for (int i = 0; i < portalBatches.Length; i++)
				portalBatches[i] = Layer("Portal" + i, art.Portals[i], 2958 + i);
			wandBatches = new FxQuadBatch[art.Wands.Length];
			for (int i = 0; i < wandBatches.Length; i++)
				wandBatches[i] = Layer("Wand" + i, art.Wands[i], 2962 + i);
			portalFront = Layer("PortalFront", art.Soft, 2970);
			PrepareShots();
		}

		// 인물 그림은 큐 3000. 그보다 작으면 뒤
		private FxQuadBatch Layer(string layerName, Texture texture, int queue, bool onTop = false)
		{
			FxQuadBatch batch = new("Volley" + layerName, root, art.Shader, texture, art.Noise, queue, onTop);
			batches.Add(batch);
			return batch;
		}

		private T Own<T>(T target) where T : UnityEngine.Object
		{
			owned.Add(target);
			return target;
		}
	}
}
