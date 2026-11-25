using System;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	// 인물이 손에 든 무기 그림. 전용 무기 프리팹 (완드, 일제 사격, 빛의 검) 에 붙어 그 형태인 동안만 보임
	// - 판정 없음. 손 자리를 따라다니고 조준 쪽을 향하며, 같은 프리팹의 발사기가 쏠 때 뒤로 밀림
	// - 변신 연출이 도는 동안은 감춤 (연출이 자기 무기를 보임)
	// - 그린 텍스처 한 장 (PaintedFxSet) 과 뒤의 빛
	public sealed class HeldWeapon : MonoBehaviour
	{
		public enum Shape
		{
			Wand = 0,
			Blade = 1,
		}

		[SerializeField] private Shape shape = Shape.Wand;
		[SerializeField, Tooltip("쏠 때 손이 밀리는 발사기 (베기, 빛탄). 요정은 빼 둠")] private Spawner[] kickSpawners = Array.Empty<Spawner>();
		[SerializeField] private PaintedFxSet art;
		[SerializeField, ColorUsage(false, true)] private Color hotTint = new(1.1f, 1.06f, 1.02f);
		[SerializeField] private Color glowTint = new(1f, 0.82f, 0.28f, 1f);

		[Header("크기와 자리 (월드)")]
		[SerializeField, Min(0.05f)] private float size = 1.35f;
		[SerializeField, Min(0.1f), Tooltip("그림의 가로 나누기 세로 (완드, 칼)")] private Vector2 aspects = new(2.27f, 1.91f);
		[SerializeField, Range(0f, 1f), Tooltip("손이 쥐는 자리 (그림의 뒤끝에서)")] private float grip = 0.2f;
		[SerializeField, Min(0f)] private float glowSize = 0.4f;
		[SerializeField, Tooltip("손 자리 (조준 쪽 옆, 위)")] private Vector2 handOffset = new(0.3f, 0.3f);
		[SerializeField, Tooltip("인물 앞으로")] private float inFront = 0.14f;
		[SerializeField, Range(0f, 1f), Tooltip("판을 카메라 쪽으로 눕히는 정도")] private float faceCamera = 0.55f;
		[SerializeField, Range(0f, 90f), Tooltip("위아래로 기우는 한도 (도)")] private float maxTilt = 40f;
		[SerializeField, Tooltip("평소 기울기. 칼은 세워 듦")] private float restTilt = 8f;

		[Header("움직임")]
		[SerializeField, Min(0.02f)] private float popSeconds = 0.18f;
		[SerializeField, Min(0f)] private float popOver = 0.35f;
		[SerializeField, Min(0f)] private float recoil = 0.18f;
		[SerializeField, Min(0.01f), Tooltip("밀림이 절반이 되는 길이")] private float recoilHalfLife = 0.07f;
		[SerializeField, Min(0f)] private float bobHeight = 0.03f;
		[SerializeField, Min(0f)] private float bobSpeed = 5f;

		private PlayerProvider playerProvider;
		private GameManager gameManager;
		private FxQuadBatch body;
		private FxQuadBatch glow;
		private Transform root;
		private bool shown;
		private Camera viewCamera;
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
			foreach (Spawner spawner in kickSpawners)
				spawner.Shot += OnShot;
		}

		private void OnDisable()
		{
			foreach (Spawner spawner in kickSpawners)
				spawner.Shot -= OnShot;
			shown = false;
			body?.Clear();
			glow?.Clear();
		}

		private void OnDestroy()
		{
			body?.Destroy();
			glow?.Destroy();
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
			if (visible && shown == false)
				shownTime = Time.time;
			shown = visible;
			root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
			body.Begin();
			glow.Begin();
			if (visible)
				Place();
			body.Commit();
			glow.Commit();
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
			float clock = Time.time;
			float kick = Mathf.Pow(0.5f, (Time.time - shotTime) / recoilHalfLife);
			Vector3 forward = Quaternion.AngleAxis(tilt * side, away) * (right * side);
			Vector3 position = playerProvider.Current.transform.position
				+ right * (handOffset.x * side)
				+ up * (handOffset.y + bobHeight * Mathf.Sin(clock * bobSpeed))
				- away * inFront
				- forward * (recoil * kick);
			float pop = Pop((Time.time - shownTime) / popSeconds);
			Vector3 crossUp = Vector3.Cross(away, forward) * side;
			float length = size * pop;
			float heat = Mathf.Max(Mathf.Pow(0.5f, (Time.time - shownTime) / recoilHalfLife), kick * 0.5f);
			float aspect = shape == Shape.Wand ? aspects.x : aspects.y;
			Vector3 center = position + forward * (length * (0.5f - grip));
			body.Add(center, forward * (length * 0.5f), crossUp * (length * 0.5f / aspect), FxQuadLook.Of(Color.Lerp(Color.white * 0.8f, hotTint, heat), 1f));
			float breath = 0.16f + 0.06f * Mathf.Sin(clock * bobSpeed) + heat * 0.6f;
			Transform view = cameraTransform;
			glow.Add(position + forward * (length * (0.82f - grip)), view.right * glowSize, view.up * glowSize, FxQuadLook.Of(glowTint * breath));
		}

		private void Create()
		{
			if (art == null)
				throw new InvalidOperationException($"{name}: 손 무기 그림 묶음 미연결");
			root = new GameObject(nameof(HeldWeapon)).transform;
			root.SetParent(transform, false);
			// 인물 그림 (큐 3000) 앞
			body = new FxQuadBatch("HeldWeapon", root, art.Shader, shape == Shape.Wand ? art.Wands[0] : art.Blade, art.Noise, 3002);
			glow = new FxQuadBatch("HeldWeaponGlow", root, art.Shader, art.Soft, art.Noise, 3003);
		}

		private float Pop(float progress)
		{
			float clamped = Mathf.Clamp01(progress);
			return 1f + popOver * Mathf.Sin(clamped * Mathf.PI) - Mathf.Pow(1f - clamped, 3f);
		}
	}
}
