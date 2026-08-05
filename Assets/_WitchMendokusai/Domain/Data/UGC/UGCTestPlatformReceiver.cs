using UnityEngine;

namespace WitchMendokusai
{
	public class UGCTestPlatformReceiver : MonoBehaviour
	{
		[SerializeField] private float amplitude = 3f;

		private bool isMoving;
		private float speed = 1f;
		private bool loop;
		private Vector3 startPosition;

		private void Awake()
		{
			startPosition = transform.position;
			UGCMaterialSafety.EnsureUsableMaterial(GetComponent<Renderer>(), new Color(0.25f, 0.75f, 1f, 1f));
			UGCObjectRegistry.Register(gameObject.name, "Platform", gameObject);
		}

		private void OnDestroy()
		{
			UGCObjectRegistry.Unregister(gameObject.name, gameObject);
		}

		private void Update()
		{
			if (isMoving == false)
				return;

			float y = Mathf.Sin(Time.time * speed) * amplitude;
			transform.position = startPosition + Vector3.up * y;

			if (loop == false && Mathf.Abs(y) >= amplitude * 0.95f)
				isMoving = false;
		}

		public void UGC_MovePlatform(UGCActionExecutor.MovePlatformCommand command)
		{
			if (command == null)
				return;

			speed = Mathf.Max(0.1f, command.speed);
			loop = command.loop;
			isMoving = true;

			Debug.Log($"[UGC] Platform '{name}' move started. route={command.routeId}, speed={speed}, loop={loop}");
		}

	}
}
