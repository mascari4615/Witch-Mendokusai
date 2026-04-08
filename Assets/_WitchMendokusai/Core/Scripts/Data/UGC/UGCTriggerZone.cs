using UnityEngine;

namespace WitchMendokusai
{
	[DisallowMultipleComponent]
	public class UGCTriggerZone : MonoBehaviour
	{
		[SerializeField] private string zoneId;
		[SerializeField] private float initialIgnoreSec = 0.6f;

		private float armedAt;

		private void Awake()
		{
			if (string.IsNullOrWhiteSpace(zoneId))
				zoneId = gameObject.name;

			armedAt = Time.time + Mathf.Max(0f, initialIgnoreSec);
		}

		public void Setup(string id)
		{
			zoneId = id;
			gameObject.name = id;
			armedAt = Time.time + Mathf.Max(0f, initialIgnoreSec);
			UGCObjectRegistry.Register(zoneId, "Zone", gameObject);
		}

		private void OnDestroy()
		{
			UGCObjectRegistry.Unregister(zoneId, gameObject);
		}

		private void OnTriggerEnter(Collider other)
		{
			if (string.IsNullOrWhiteSpace(zoneId))
				return;

			if (Time.time < armedAt)
				return;

			string actorId = ResolveActorId(other.gameObject);
			UGCConditionRuntime.RegisterZoneEnter(zoneId, actorId);
			UGCLog.Info($"Zone entered. zone={zoneId}, actor={actorId}");

			UGCRuntimeSession.Instance?.ExecuteTriggersForZoneEnter(zoneId, actorId);
		}

		private static string ResolveActorId(GameObject obj)
		{
			if (obj.CompareTag("Player"))
				return "player";

			return obj.name;
		}
	}
}
