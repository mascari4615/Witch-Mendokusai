using UnityEngine;

namespace WitchMendokusai
{
	public class ChainLink : MonoBehaviour
	{
		[SerializeField] private LineRenderer lineRenderer;
		[SerializeField] private float heightOffset = 0.5f;

		public void SetPoints(Vector3 from, Vector3 to)
		{
			lineRenderer.positionCount = 2;
			lineRenderer.SetPosition(0, from + Vector3.up * heightOffset);
			lineRenderer.SetPosition(1, to + Vector3.up * heightOffset);
		}
	}
}
