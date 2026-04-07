using UnityEngine;

namespace WitchMendokusai
{
	[DisallowMultipleComponent]
	public class UGCTriggerZone : MonoBehaviour
	{
		[SerializeField] private string zoneId;

		public void Setup(string id)
		{
			zoneId = id;
			gameObject.name = id;
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

			string actorId = ResolveActorId(other.gameObject);
			UGCConditionRuntime.RegisterZoneEnter(zoneId, actorId);
			UGCLog.Info($"Zone entered. zone={zoneId}, actor={actorId}");
		}

		private static string ResolveActorId(GameObject obj)
		{
			if (obj.CompareTag("Player"))
				return "player";

			return obj.name;
		}
	}
}
