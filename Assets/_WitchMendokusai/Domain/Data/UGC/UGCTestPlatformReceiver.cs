using UnityEngine;

namespace WitchMendokusai
{
	public class UGCTestPlatformReceiver : MonoBehaviour
	{
		[SerializeField] private float amplitude = 3f;
		// UGC 명령이 speed 를 덮어쓰기 전까지의 기본 속도.
		[SerializeField] private float speed = 1f;
		// UGC 가 아무리 느린 값을 보내도 이 아래로는 안 내려간다 — 0 이면 플랫폼이 영영 안 멈춘다.
		[SerializeField] private float minSpeed = 0.1f;
		// 왕복이 아닐 때, 진폭의 몇 할까지 갔으면 「끝까지 갔다」로 볼지.
		[SerializeField] private float stopThresholdRatio = 0.95f;
		// 머티리얼이 없는 UGC 오브젝트에 입혀줄 기본 색.
		[SerializeField] private Color defaultMaterialColor = new(0.25f, 0.75f, 1f, 1f);

		private bool isMoving;
		private bool loop;
		private Vector3 startPosition;

		private void Awake()
		{
			startPosition = transform.position;
			UGCMaterialSafety.EnsureUsableMaterial(GetComponent<Renderer>(), defaultMaterialColor);
			UGCObjectRegistry.Register(gameObject.name, "Platform", gameObject);
		}

		private void OnDestroy()
		{
			UGCObjectRegistry.Unregister(gameObject.name, gameObject);
		}

		private void Update()
		{
			if (!isMoving)
				return;

			float y = Mathf.Sin(Time.time * speed) * amplitude;
			transform.position = startPosition + Vector3.up * y;

			if (loop == false && Mathf.Abs(y) >= amplitude * stopThresholdRatio)
				isMoving = false;
		}

		public void UGC_MovePlatform(UGCActionExecutor.MovePlatformCommand command)
		{
			if (command == null)
				return;

			speed = Mathf.Max(minSpeed, command.speed);
			loop = command.loop;
			isMoving = true;

			Debug.Log($"[UGC] Platform '{name}' move started. route={command.routeId}, speed={speed}, loop={loop}");
		}

	}
}
