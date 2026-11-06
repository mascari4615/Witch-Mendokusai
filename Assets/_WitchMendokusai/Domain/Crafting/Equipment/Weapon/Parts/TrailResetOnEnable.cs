using UnityEngine;

namespace WitchMendokusai
{
	// 풀 재사용 때 이전 위치에서 이어지는 궤적 제거
	[RequireComponent(typeof(TrailRenderer))]
	public class TrailResetOnEnable : MonoBehaviour
	{
		private TrailRenderer trail;

		private void Awake()
		{
			trail = GetComponent<TrailRenderer>();
		}

		private void OnEnable()
		{
			trail.Clear();
		}
	}
}
