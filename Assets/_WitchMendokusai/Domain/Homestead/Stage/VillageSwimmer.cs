using UnityEngine;

namespace WitchMendokusai
{
	// 수조 안을 오가는 고기 그림. 가는 쪽을 봄
	public sealed class VillageSwimmer : MonoBehaviour
	{
		[SerializeField] private float reach = 0.42f;
		[SerializeField] private Vector2 seconds = new(5f, 9f);
		[SerializeField] private float rise = 0.1f;

		private HomesteadSprite sprite;
		private float period;
		private float phase;

		public static VillageSwimmer Create(Transform parent, Vector3 localPosition, Sprite icon, float height)
		{
			GameObject root = new("Swimmer");
			root.transform.SetParent(parent, false);
			root.transform.localPosition = localPosition;
			VillageSwimmer swimmer = root.AddComponent<VillageSwimmer>();
			swimmer.sprite = HomesteadSprite.Create(root.transform, Vector3.zero, icon, height);
			swimmer.phase = Mathf.Repeat(parent.position.x * 0.37f + parent.position.z * 0.61f, 1f);
			swimmer.period = Mathf.Lerp(swimmer.seconds.x, swimmer.seconds.y, swimmer.phase);
			return swimmer;
		}

		private void Update()
		{
			float turn = (Time.time / period + phase) * Mathf.PI * 2f;
			sprite.transform.localPosition = new Vector3(Mathf.Sin(turn) * reach, Mathf.Sin(turn * 1.7f) * rise, 0f);
			Camera view = Camera.main;
			if (view == null)
				return;
			// 아이콘의 고기는 왼쪽을 봄
			Vector3 heading = transform.right * Mathf.Cos(turn);
			sprite.Renderer.flipX = Vector3.Dot(heading, view.transform.right) > 0f;
		}
	}
}
