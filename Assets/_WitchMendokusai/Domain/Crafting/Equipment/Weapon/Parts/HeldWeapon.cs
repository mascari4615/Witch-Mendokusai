using System;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 인물이 손에 든 무기 그림. 전용 무기 프리팹 (완드, 일제 사격, 빛의 검) 에 붙어 그 형태인 동안만 보임
	// - 판정 없음. 손 자리를 따라다니고 조준 쪽을 향하며, 같은 프리팹의 발사기가 쏠 때 뒤로 밀림
	// - 변신 연출이 도는 동안은 감춤 (연출이 자기 무기를 보임)
	// - 결은 변신 연출과 같음 (FormSolid)
	public sealed class HeldWeapon : MonoBehaviour
	{
		private static readonly int EMISSION_ID = Shader.PropertyToID("_EmissionColor");
		private static readonly int EDGE_ID = Shader.PropertyToID("_EdgeColor");
		private static readonly int DENSITY_ID = Shader.PropertyToID("_TexelDensity");
		private static readonly int DISSOLVE_RANGE_ID = Shader.PropertyToID("_DissolveRange");
		private static readonly int OUTLINE_ID = Shader.PropertyToID("_Outline");
		private static readonly int HEAT_ID = Shader.PropertyToID("_Heat");

		public enum Shape
		{
			Wand = 0,
			Blade = 1,
		}

		[SerializeField] private Shape shape = Shape.Wand;
		[SerializeField] private Shader solidShader;
		[SerializeField, ColorUsage(false, true)] private Color emissionColor = new(1.08f, 0.9f, 0.5f);

		[Header("크기와 자리 (월드)")]
		[SerializeField, Min(0.05f)] private float size = 0.72f;
		[SerializeField, Min(0.01f), Tooltip("칼날의 두께 배율")] private float bladeWidth = 0.16f;
		[SerializeField, Tooltip("손 자리 (조준 쪽 옆, 위)")] private Vector2 handOffset = new(0.3f, 0.3f);
		[SerializeField, Tooltip("인물 앞으로")] private float inFront = 0.14f;
		[SerializeField, Range(0f, 1f), Tooltip("판을 카메라 쪽으로 눕히는 정도")] private float faceCamera = 0.55f;
		[SerializeField, Range(0f, 90f), Tooltip("위아래로 기우는 한도 (도)")] private float maxTilt = 40f;
		[SerializeField, Tooltip("평소 기울기. 칼은 세워 듦")] private float restTilt = 8f;

		[Header("움직임")]
		[SerializeField, Min(8f)] private float texelsPerUnit = 32f;
		[SerializeField, Min(1f)] private float poseSteps = 15f;
		[SerializeField, Min(0.02f)] private float popSeconds = 0.18f;
		[SerializeField, Min(0f)] private float popOver = 0.35f;
		[SerializeField, Min(0f)] private float recoil = 0.18f;
		[SerializeField, Min(0.01f), Tooltip("밀림이 절반이 되는 길이")] private float recoilHalfLife = 0.07f;
		[SerializeField, Min(0f)] private float bobHeight = 0.03f;
		[SerializeField, Min(0f)] private float bobSpeed = 5f;

		private PlayerProvider playerProvider;
		private GameManager gameManager;
		private Spawner[] spawners;
		private Transform body;
		private Camera viewCamera;
		private Mesh mesh;
		private Material material;
		private float shownTime;
		private float shotTime = float.NegativeInfinity;
		private float side = 1f;

		[Inject]
		public void Construct(PlayerProvider playerProvider, GameManager gameManager)
		{
			this.playerProvider = playerProvider;
			this.gameManager = gameManager;
		}

		private void OnEnable()
		{
			// 같은 오브젝트의 베기만. 자식 (요정 등) 이 쏠 때는 손이 안 밀림
			spawners ??= GetComponents<Spawner>();
			foreach (Spawner spawner in spawners)
				spawner.Shot += OnShot;
		}

		private void OnDisable()
		{
			foreach (Spawner spawner in spawners)
				spawner.Shot -= OnShot;
			if (body != null)
				body.gameObject.SetActive(false);
		}

		private void OnDestroy()
		{
			Destroy(mesh);
			Destroy(material);
		}

		private void OnShot(GameObject shot, Vector3 direction) => shotTime = Time.time;

		private void LateUpdate()
		{
			viewCamera = Camera.main;
			// 씬이 바뀌는 사이에는 카메라나 인물이 없음
			bool visible = gameManager.IsFormMomentPlaying == false && viewCamera != null && playerProvider.Current != null;
			if (visible && body == null)
				Create();
			if (body == null)
				return;
			if (visible && body.gameObject.activeSelf == false)
				shownTime = Time.time;
			body.gameObject.SetActive(visible);
			if (visible)
				Place();
		}

		private void Place()
		{
			Transform cameraTransform = viewCamera.transform;
			Vector3 right = cameraTransform.right;
			Vector3 up = Vector3.Slerp(Vector3.up, cameraTransform.up, faceCamera).normalized;
			Vector3 away = Vector3.Cross(right, up).normalized;
			Vector3 aim = playerProvider.Current.AimDirection;
			Vector2 onScreen = new(Vector3.Dot(aim, right), Vector3.Dot(aim, up));
			// 조준이 화면 앞뒤 축에 가까우면 마지막 쪽을 지킴
			if (Mathf.Abs(onScreen.x) > 0.1f)
				side = Mathf.Sign(onScreen.x);
			float tilt = Mathf.Clamp(Mathf.Atan2(onScreen.y, Mathf.Abs(onScreen.x)) * Mathf.Rad2Deg, -maxTilt, maxTilt) + restTilt;
			float clock = Mathf.Floor(Time.time * poseSteps) / poseSteps;
			float kick = Mathf.Pow(0.5f, (Time.time - shotTime) / recoilHalfLife);
			Vector3 forward = Quaternion.AngleAxis(tilt * side, -away) * (right * side);
			Vector3 position = playerProvider.Current.transform.position
				+ right * (handOffset.x * side)
				+ up * (handOffset.y + bobHeight * Mathf.Sin(clock * bobSpeed))
				- away * inFront
				- forward * (recoil * kick);
			float pop = Pop((Time.time - shownTime) / popSeconds);
			Vector3 crossUp = Vector3.Cross(away, forward) * side;
			if (shape == Shape.Wand)
			{
				body.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.Cross(forward, crossUp), crossUp));
				body.localScale = Vector3.one * (size * pop);
			}
			else
			{
				body.SetPositionAndRotation(position, Quaternion.LookRotation(away, forward) * Quaternion.Euler(0f, 45f, 0f));
				body.localScale = new Vector3(bladeWidth, size * pop, bladeWidth);
			}
			material.SetFloat(HEAT_ID, Mathf.Max(Mathf.Pow(0.5f, (Time.time - shownTime) / recoilHalfLife), kick * 0.5f));
		}

		private void Create()
		{
			if (solidShader == null)
				throw new InvalidOperationException($"{name}: 손 무기 셰이더 미연결");
			mesh = shape == Shape.Wand ? FormMeshes.Wand() : FormMeshes.Bar(1f);
			material = new Material(solidShader) { name = "HeldWeapon" };
			material.SetColor(EMISSION_ID, emissionColor);
			material.SetColor(EDGE_ID, emissionColor * 1.3f);
			material.SetVector(DENSITY_ID, shape == Shape.Wand ? Vector3.one * (size * texelsPerUnit) : new Vector3(5f, size * texelsPerUnit, 5f));
			material.SetVector(DISSOLVE_RANGE_ID, FormMeshes.WAND_DISSOLVE_RANGE);
			material.SetFloat(OUTLINE_ID, shape == Shape.Wand ? FormMeshes.WAND_OUTLINE : 0f);
			body = UltimateFxStage.CreateMeshObject(nameof(HeldWeapon), transform, mesh, material).transform;
			body.gameObject.SetActive(false);
		}

		private float Pop(float progress)
		{
			float clamped = Mathf.Clamp01(progress);
			return 1f + popOver * Mathf.Sin(clamped * Mathf.PI) - Mathf.Pow(1f - clamped, 3f);
		}
	}
}
