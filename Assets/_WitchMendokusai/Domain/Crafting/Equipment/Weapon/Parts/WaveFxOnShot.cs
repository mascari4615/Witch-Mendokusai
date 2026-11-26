using UnityEngine;

namespace WitchMendokusai
{
	// 같은 오브젝트의 생성기 (Spawner) 발사에 검기 그림 (SwordWaveFx) 을 같이 냄. 판정 없음. 쓰는 곳: 알리사 십자 충격파
	[RequireComponent(typeof(Spawner))]
	public class WaveFxOnShot : MonoBehaviour
	{
		[SerializeField] private SwordWaveFx fx;
		[SerializeField, Min(0.1f), Tooltip("판정 길이 (월드)")] private float length = 10f;
		[SerializeField, Min(0.1f), Tooltip("그림 폭 배율")] private float width = 1.5f;

		private Spawner spawner;

		private void Awake() => spawner = GetComponent<Spawner>();

		private void OnEnable() => spawner.Shot += HandleShot;

		private void OnDisable() => spawner.Shot -= HandleShot;

		private void HandleShot(GameObject shot, Vector3 direction)
		{
			if (direction != Vector3.zero)
				fx.PlayWave(shot.transform.position, direction, length * shot.transform.localScale.z, width * shot.transform.localScale.x, true);
		}
	}
}
