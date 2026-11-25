using UnityEngine;
using UnityEngine.Rendering;

namespace WitchMendokusai
{
	// 공격 물체 (뿌리, 버섯, 흙덩이) 를 세우는 그림 한 장. CombatFxRunner 가 재사용
	public static class FxSpritePool
	{
		private const string KEY = "FxSprite";

		public static SpriteRenderer Rent(CombatFxRunner runner, Sprite sprite, Material material)
		{
			GameObject rented = runner.Rent(KEY, Create);
			SpriteRenderer body = rented.GetComponent<SpriteRenderer>();
			body.sprite = sprite;
			body.sharedMaterial = material;
			body.color = Color.white;
			body.flipX = false;
			body.enabled = true;
			rented.transform.localScale = Vector3.one;
			rented.transform.rotation = Quaternion.identity;
			return body;
		}

		public static void Return(CombatFxRunner runner, SpriteRenderer body)
		{
			if (body != null && runner != null)
				runner.Return(KEY, body.gameObject);
		}

		private static GameObject Create()
		{
			GameObject created = new(KEY);
			SpriteRenderer body = created.AddComponent<SpriteRenderer>();
			body.shadowCastingMode = ShadowCastingMode.Off;
			body.receiveShadows = false;
			return created;
		}
	}
}
