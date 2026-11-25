using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 알리사 일제 사격의 그림. 빛 완드를 등 뒤에 잔뜩 꺼내고 (한 자루마다 금빛 고리), 한 자루가 한 발씩 쏘고 부서짐
	// - 판정 없음. 피해는 VolleyGauge 가 내는 탄이 함. 줄기는 그 탄의 머리를 따라가 그림과 피해 시점이 어긋나지 않음
	// - 결은 변신 연출과 같음 (FormSolid, FormEnergy. 캐릭터 PPU 와 같은 텍셀, 네 단 색, 끊긴 움직임)
	// - 게임 시간으로 흐름 (카드 창에서 같이 멈춤). 만든 물체는 다시 쓰고 파괴될 때 지움
	// - 이 파일: 값, 꺼냄, 무기 자세. 줄기와 총구와 맞은 자리는 .Shots.cs
	public sealed partial class VolleyArsenal : MonoBehaviour
	{
		private static readonly int CORE_COLOR_ID = Shader.PropertyToID("_CoreColor");
		private static readonly int BODY_COLOR_ID = Shader.PropertyToID("_BodyColor");
		private static readonly int TIP_COLOR_ID = Shader.PropertyToID("_TipColor");
		private static readonly int EMISSION_ID = Shader.PropertyToID("_EmissionColor");
		private static readonly int EDGE_ID = Shader.PropertyToID("_EdgeColor");
		private static readonly int DENSITY_ID = Shader.PropertyToID("_TexelDensity");
		private static readonly int DISSOLVE_ID = Shader.PropertyToID("_Dissolve");
		private static readonly int DISSOLVE_RANGE_ID = Shader.PropertyToID("_DissolveRange");
		private static readonly int OUTLINE_ID = Shader.PropertyToID("_Outline");
		private static readonly int HEAT_ID = Shader.PropertyToID("_Heat");
		private static readonly int TEXELS_ID = Shader.PropertyToID("_Texels");
		private static readonly int SHAPE_ID = Shader.PropertyToID("_Shape");
		private static readonly int FLASH_ID = Shader.PropertyToID("_Flash");
		private static readonly int INTENSITY_ID = Shader.PropertyToID("_Intensity");
		private static readonly int DISSOLVE_FROM_TIP_ID = Shader.PropertyToID("_DissolveFromTip");
		private const float GLOW_SHAPE = 2f;
		// 인물 그림 (큐 3000) 뒤와 앞
		private const int BEHIND_BODY_QUEUE = 2962;
		private const int FRONT_QUEUE = 3012;

		[Header("셰이더")]
		[SerializeField] private Shader energyShader;
		[SerializeField] private Shader solidShader;

		[Header("색")]
		[SerializeField, ColorUsage(false, true)] private Color coreColor = new(1.08f, 0.9f, 0.5f);
		[SerializeField, ColorUsage(false, true)] private Color bodyColor = new(0.98f, 0.54f, 0.1f);
		[SerializeField, ColorUsage(false, true)] private Color tipColor = new(0.6f, 0.2f, 0.04f);
		[SerializeField] private Color lightColor = new(1f, 0.7f, 0.36f, 1f);

		[Header("결")]
		[SerializeField, Min(8f), Tooltip("월드 1 당 텍셀. 캐릭터 그림의 PPU (32) 와 같게")] private float texelsPerUnit = 32f;
		[SerializeField, Min(1f), Tooltip("무기 움직임이 바뀌는 횟수 (초당)")] private float poseSteps = 15f;

		[Header("꺼냄")]
		[SerializeField, Min(0f), Tooltip("첫 자루에서 마지막 자루까지 나오는 길이")] private float raiseSeconds = 0.28f;
		[SerializeField, Min(0f), Tooltip("다 나온 뒤 첫 발까지 멈춤")] private float holdSeconds = 0.2f;
		[SerializeField, Min(0.02f), Tooltip("한 자루가 커지는 길이")] private float popSeconds = 0.12f;
		[SerializeField, Min(0.05f), Tooltip("쏜 자루가 부서지는 길이")] private float breakSeconds = 0.28f;
		[SerializeField, Min(0f), Tooltip("쏜 뒤 부서지기 전까지")] private float breakDelay = 0.16f;
		[SerializeField, Min(1), Tooltip("그리는 자루 수 상한")] private int maxWeapons = 32;

		[Header("배치 (월드)")]
		[SerializeField, Tooltip("가장 안쪽 줄과 바깥 줄의 반지름")] private Vector2 rowRadius = new(1.25f, 3f);
		[SerializeField, Min(1)] private int rows = 3;
		[SerializeField, Tooltip("부채 양 끝 각도 (도). 0 이 화면 오른쪽, 90 이 위")] private Vector2 fanAngles = new(-8f, 188f);
		[SerializeField] private float centerHeight = 0.75f;
		[SerializeField, Tooltip("인물 뒤로")] private float behind = 0.45f;
		[SerializeField, Range(0f, 1f), Tooltip("배치 판을 카메라 쪽으로 눕히는 정도")] private float faceCamera = 0.55f;
		[SerializeField] private Vector2 weaponSize = new(0.92f, 1.25f);
		[SerializeField, Min(0.05f)] private float ringSize = 0.5f;
		[SerializeField, Min(0f), Tooltip("쏠 때 뒤로 밀리는 거리")] private float recoil = 0.3f;
		[SerializeField, Range(0f, 1f), Tooltip("고리의 밝기 바닥값")] private float ringGlow = 0.4f;

		private sealed class Weapon
		{
			public Transform Body;
			public Transform Ring;
			public Material BodyMaterial;
			public Material RingMaterial;
			public Vector2 Slot;
			public float Size;
			public float Phase;
			public float AppearTime;
			public float FireTime;
			public bool Alive;
		}

		private readonly List<Weapon> weapons = new();
		private readonly List<UnityEngine.Object> owned = new();
		private Transform root;
		private Mesh wandMesh;
		private Mesh ringMesh;
		private Transform owner;
		private Func<Vector3> aimSource;
		private int raisedCount;

		public float WindupSeconds => raiseSeconds + holdSeconds;

		// 자루를 꺼내기 시작. 첫 발은 WindupSeconds 뒤 호출한 쪽이 Fire 로
		public void Raise(Transform ownerTransform, Func<Vector3> aim, int count)
		{
			Prepare();
			owner = ownerTransform;
			aimSource = aim;
			raisedCount = Mathf.Clamp(count, 1, maxWeapons);
			while (weapons.Count < raisedCount)
				weapons.Add(CreateWeapon(weapons.Count));
			for (int i = 0; i < weapons.Count; i++)
			{
				Weapon weapon = weapons[i];
				weapon.Alive = i < raisedCount;
				if (weapon.Alive == false)
				{
					Show(weapon, false);
					continue;
				}
				weapon.Slot = SlotFor(i, raisedCount);
				// 안쪽 줄부터 바깥으로, 양쪽이 번갈아 나오게 흔듦
				weapon.AppearTime = Time.time + raiseSeconds * i / Mathf.Max(1, raisedCount - 1);
				weapon.FireTime = float.PositiveInfinity;
				weapon.BodyMaterial.SetFloat(DISSOLVE_ID, 0f);
				weapon.RingMaterial.SetFloat(DISSOLVE_ID, 0f);
				Show(weapon, false);
			}
		}

		// 한 발. 쏘는 자루를 골라 총구를 터뜨리고, 줄기가 탄을 따라가게 함
		public void Fire(int shotIndex, GameObject shot)
		{
			// 옆 자루가 잇달아 쏘지 않게 서로소 걸음으로 섞음
			int stride = raisedCount % 5 == 0 ? 3 : 5;
			Weapon weapon = weapons[shotIndex * stride % raisedCount];
			weapon.FireTime = Time.time;
			Vector3 muzzle = Muzzle(weapon);
			Flash(muzzle, Aim(), muzzleSize);
			StartTracer(muzzle, shot.transform);
			lightLevel = 1f;
		}

		private void OnDisable()
		{
			foreach (Weapon weapon in weapons)
			{
				weapon.Alive = false;
				Show(weapon, false);
			}
			StopShots();
		}

		private void OnDestroy()
		{
			foreach (UnityEngine.Object target in owned)
				Destroy(target);
			if (root != null)
				Destroy(root.gameObject);
		}

		private void LateUpdate()
		{
			if (root == null || owner == null)
				return;
			UpdateWeapons();
			UpdateShots();
		}

		private void UpdateWeapons()
		{
			foreach (Weapon weapon in weapons)
			{
				if (weapon.Alive == false || Time.time < weapon.AppearTime)
					continue;
				float age = Time.time - weapon.AppearTime;
				float sinceFire = Time.time - weapon.FireTime;
				float broken = Mathf.Clamp01((sinceFire - breakDelay) / breakSeconds);
				if (broken >= 1f)
				{
					weapon.Alive = false;
					Show(weapon, false);
					continue;
				}
				Show(weapon, true);
				Pose(weapon, out Vector3 position, out Vector3 aim, out Vector3 up);
				float pop = Pop(age / popSeconds);
				float kick = sinceFire >= 0f ? Mathf.Pow(0.5f, sinceFire / 0.06f) : 0f;
				weapon.Body.SetPositionAndRotation(position - aim * (recoil * kick), WeaponRotation(aim, up));
				weapon.Body.localScale = Vector3.one * (weapon.Size * pop);
				weapon.BodyMaterial.SetFloat(HEAT_ID, Mathf.Max(Mathf.Pow(0.5f, age / 0.07f), kick * 0.8f));
				weapon.BodyMaterial.SetFloat(DISSOLVE_ID, broken);

				// 고리는 카메라 쪽으로 반쯤 돌려 옆에서도 둥글게 보임. 쏘면 퍼지며 부서짐
				Vector3 facing = (aim - CameraForward() * 1.4f).normalized;
				float spread = 1f + 0.7f * Mathf.Clamp01(sinceFire / 0.16f);
				weapon.Ring.SetPositionAndRotation(position - aim * (weapon.Size * 0.15f), Quaternion.FromToRotation(Vector3.up, facing) * Quaternion.Euler(0f, Stepped() * 140f + weapon.Phase * 60f, 0f));
				weapon.Ring.localScale = Vector3.one * (ringSize * pop * spread);
				weapon.RingMaterial.SetFloat(DISSOLVE_ID, sinceFire >= 0f ? Mathf.Clamp01(sinceFire / 0.2f) : 0f);
				weapon.RingMaterial.SetFloat(FLASH_ID, Mathf.Max(ringGlow, Mathf.Max(Mathf.Pow(0.5f, age / 0.08f), kick)));
			}
		}

		private void Pose(Weapon weapon, out Vector3 position, out Vector3 aim, out Vector3 up)
		{
			Transform cameraTransform = Camera.main.transform;
			Vector3 right = cameraTransform.right;
			up = Vector3.Slerp(Vector3.up, cameraTransform.up, faceCamera).normalized;
			Vector3 away = Vector3.Cross(right, up).normalized;
			float bob = 0.04f * Mathf.Sin(Stepped() * 5f + weapon.Phase);
			position = owner.position + Vector3.up * centerHeight + right * weapon.Slot.x + up * (weapon.Slot.y + bob) + away * behind;
			aim = Aim();
		}

		private Vector3 Muzzle(Weapon weapon)
		{
			Pose(weapon, out Vector3 position, out Vector3 aim, out Vector3 up);
			return position + aim * (weapon.Size * 0.9f) + up * (weapon.Size * 0.1f);
		}

		private Vector3 Aim() => aimSource();

		private static Vector3 CameraForward() => Camera.main.transform.forward;

		private float Stepped() => Mathf.Floor(Time.time * poseSteps) / poseSteps;

		// 총구 (+X) 가 조준 쪽, 손잡이 (-Y) 가 화면 아래쪽
		private static Quaternion WeaponRotation(Vector3 aim, Vector3 up) => Quaternion.LookRotation(Vector3.Cross(aim, up), up);

		// 처음에 넘쳤다가 제 크기로
		private static float Pop(float progress)
		{
			float clamped = Mathf.Clamp01(progress);
			return 1f + 0.35f * Mathf.Sin(clamped * Mathf.PI) - Mathf.Pow(1f - clamped, 3f);
		}

		// 줄마다 부채꼴로 고르게. 바깥 줄일수록 많이
		private Vector2 SlotFor(int index, int count)
		{
			int row = 0;
			int used = 0;
			int weight = rows * (rows + 1) / 2;
			int inRow = 0;
			for (; row < rows; row++)
			{
				inRow = row == rows - 1 ? count - used : Mathf.Max(1, Mathf.RoundToInt(count * (row + 1f) / weight));
				if (index < used + inRow)
					break;
				used += inRow;
			}
			float along = inRow <= 1 ? 0.5f : (index - used) / (inRow - 1f);
			float angle = Mathf.Lerp(fanAngles.x, fanAngles.y, along) * Mathf.Deg2Rad;
			float radius = Mathf.Lerp(rowRadius.x, rowRadius.y, rows <= 1 ? 0f : row / (rows - 1f));
			return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
		}

		private static void Show(Weapon weapon, bool visible)
		{
			weapon.Body.gameObject.SetActive(visible);
			weapon.Ring.gameObject.SetActive(visible);
		}

		private void Prepare()
		{
			if (root != null)
				return;
			if (energyShader == null || solidShader == null)
				throw new InvalidOperationException($"{name}: 일제 사격 셰이더 미연결");
			root = new GameObject(nameof(VolleyArsenal)).transform;
			wandMesh = Own(FormMeshes.Wand());
			ringMesh = Own(FormMeshes.Ring(24, 0.74f));
			PrepareShots();
		}

		private Weapon CreateWeapon(int index)
		{
			Weapon weapon = new()
			{
				Size = Mathf.Lerp(weaponSize.x, weaponSize.y, Mathf.Repeat(index * 0.618f, 1f)),
				Phase = index * 1.7f,
				BodyMaterial = Solid(),
				RingMaterial = Energy(BEHIND_BODY_QUEUE),
			};
			weapon.BodyMaterial.SetVector(DENSITY_ID, Vector3.one * (weapon.Size * texelsPerUnit));
			weapon.BodyMaterial.SetVector(DISSOLVE_RANGE_ID, new Vector4(-0.42f, 0.2f, 0f, 0f));
			weapon.BodyMaterial.SetFloat(OUTLINE_ID, 0.045f);
			weapon.RingMaterial.SetVector(TEXELS_ID, new Vector4(ringSize * 6.28f * texelsPerUnit, 4f, 0f, 0f));
			weapon.Body = UltimateFxStage.CreateMeshObject("VolleyWand", root, wandMesh, weapon.BodyMaterial).transform;
			weapon.Ring = UltimateFxStage.CreateMeshObject("VolleyRing", root, ringMesh, weapon.RingMaterial).transform;
			return weapon;
		}

		private Material Solid()
		{
			Material material = Own(new Material(solidShader) { name = "VolleyArsenal_Solid" });
			material.SetColor(EMISSION_ID, coreColor);
			material.SetColor(EDGE_ID, coreColor * 1.3f);
			return material;
		}

		private Material Energy(int queue)
		{
			Material material = Own(new Material(energyShader) { name = "VolleyArsenal_Energy", renderQueue = queue });
			material.SetColor(CORE_COLOR_ID, coreColor);
			material.SetColor(BODY_COLOR_ID, bodyColor);
			material.SetColor(TIP_COLOR_ID, tipColor);
			return material;
		}

		private T Own<T>(T target) where T : UnityEngine.Object
		{
			owned.Add(target);
			return target;
		}
	}
}
