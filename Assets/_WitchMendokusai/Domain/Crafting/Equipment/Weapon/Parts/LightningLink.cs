using UnityEngine;

namespace WitchMendokusai
{
	// 두 점을 잇는 꺾인 번개. 켜져 있는 동안 몇 번 다시 꺾임. 풀에서 다시 쓰므로 SetPoints 마다 새로
	public class LightningLink : MonoBehaviour
	{
		[SerializeField] private LineRenderer lineRenderer;
		[SerializeField] private float heightOffset = 0.7f;
		[SerializeField, Min(1)] private int segments = 7;
		[SerializeField, Min(0f), Tooltip("길이에 비례해 옆으로 꺾이는 정도")] private float jitter = 0.14f;
		[SerializeField, Min(0.01f)] private float rejitterSeconds = 0.05f;

		private Vector3 from;
		private Vector3 to;
		private float nextTime;

		public void SetPoints(Vector3 start, Vector3 end)
		{
			from = start + Vector3.up * heightOffset;
			to = end + Vector3.up * heightOffset;
			Rebuild();
		}

		private void Update()
		{
			if (Time.time >= nextTime)
				Rebuild();
		}

		private void Rebuild()
		{
			nextTime = Time.time + rejitterSeconds;
			float spread = (to - from).magnitude * jitter;
			lineRenderer.positionCount = segments + 1;
			for (int i = 0; i <= segments; i++)
			{
				Vector3 point = Vector3.Lerp(from, to, i / (float)segments);
				if (i > 0 && i < segments)
					point += Random.insideUnitSphere * spread;
				lineRenderer.SetPosition(i, point);
			}
		}
	}
}
