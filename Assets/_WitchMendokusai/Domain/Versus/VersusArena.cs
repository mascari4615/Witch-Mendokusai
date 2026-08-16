using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 대결 축 v0 의 판 (TASK-WM-411). 벽·바닥은 보이기만 하고 판정은 이 반크기 두 개가 전부 —
	/// 콜라이더에 기대면 「왜 안 튕겼나」가 물리 설정 문제로 새어 나간다.
	/// </summary>
	public sealed class VersusArena : MonoBehaviour
	{
		[SerializeField] private float halfWidth = 13f;
		[SerializeField] private float halfDepth = 9f;

		public float HalfWidth => halfWidth;
		public float HalfDepth => halfDepth;

		public void Configure(float halfWidth, float halfDepth)
		{
			this.halfWidth = halfWidth;
			this.halfDepth = halfDepth;
		}

		/// <summary> 벽 안으로 밀어 넣는다 — 싸우는 사람이 판 밖으로 나가는 일은 없다. </summary>
		public Vector3 Clamp(Vector3 position, float radius)
		{
			position.x = Mathf.Clamp(position.x, -halfWidth + radius, halfWidth - radius);
			position.z = Mathf.Clamp(position.z, -halfDepth + radius, halfDepth - radius);
			return position;
		}

		/// <summary> 라운드 시작 위치 — 서로 반대쪽 끝. </summary>
		public Vector3 SpawnPosition(int playerIndex)
		{
			float x = playerIndex == 0 ? -halfWidth * 0.7f : halfWidth * 0.7f;
			return new Vector3(x, 0.5f, 0f);
		}
	}
}
