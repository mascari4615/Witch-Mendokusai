using System;
using UnityEngine;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 고른 모자와 물들임을 조작 인형에 입힘. 인형이 새로 묶일 때와 옷장이 바뀔 때
	// 겉모습만. 판 중 형태 (변신) 는 자기 색을 덮어쓰고, 다음 묶임에서 되돌아옴
	public sealed class HomesteadCostumeDresser : IDisposable
	{
		private const string HAT_NAME = "HomesteadHat";
		private const float HAT_LIFT = 0.18f;
		private const int HAT_ORDER_STEP = 1;

		private readonly HomesteadSim sim;
		private PlayerObject player;
		private SpriteRenderer hat;
		private string shownHat = string.Empty;
		private string shownOutfit = string.Empty;

		public HomesteadCostumeDresser(HomesteadSim sim)
		{
			this.sim = sim;
			sim.Changed += Refresh;
			EventBusBridge.Subscribe<PlayerObjectBoundEvent>(OnBound);
			PlayerObject current = PlayerProvider.Instance != null ? PlayerProvider.Instance.CurrentObject : null;
			if (current != null)
				Dress(current);
		}

		public void Dispose()
		{
			sim.Changed -= Refresh;
			EventBusBridge.Unsubscribe<PlayerObjectBoundEvent>(OnBound);
			if (hat != null)
				UnityEngine.Object.Destroy(hat.gameObject);
		}

		private void OnBound(PlayerObjectBoundEvent evt) => Dress((PlayerObject)evt.Object);

		private void Dress(PlayerObject target)
		{
			player = target;
			shownHat = string.Empty;
			shownOutfit = string.Empty;
			Refresh();
		}

		private void Refresh()
		{
			if (player == null)
				return;
			ApplyHat(sim.WornIn(CostumeSlot.Hat));
			ApplyOutfit(sim.WornIn(CostumeSlot.Outfit));
		}

		private void ApplyHat(string costumeId)
		{
			if (hat != null && hat.transform.parent != player.HeadSprite.transform)
			{
				UnityEngine.Object.Destroy(hat.gameObject);
				hat = null;
			}
			if (costumeId.Length == 0)
			{
				if (hat != null)
					hat.enabled = false;
				shownHat = costumeId;
				return;
			}
			if (hat == null)
				hat = MakeHat();
			if (shownHat == costumeId && hat.enabled)
				return;
			hat.sprite = HomesteadArt.Instance.Icon(costumeId);
			hat.enabled = true;
			shownHat = costumeId;
		}

		private SpriteRenderer MakeHat()
		{
			SpriteRenderer head = player.HeadSprite;
			GameObject hatObject = new(HAT_NAME);
			hatObject.layer = head.gameObject.layer;
			hatObject.transform.SetParent(head.transform, false);
			hatObject.transform.localPosition = new Vector3(0f, head.sprite.bounds.max.y - HAT_LIFT, 0f);
			hatObject.transform.localScale = Vector3.one * (head.sprite.bounds.size.x * 0.9f);
			SpriteRenderer made = hatObject.AddComponent<SpriteRenderer>();
			made.sharedMaterial = head.sharedMaterial;
			made.sortingLayerID = head.sortingLayerID;
			made.sortingOrder = head.sortingOrder + HAT_ORDER_STEP;
			hatObject.AddComponent<HomesteadHatFollower>().Follow(head, made);
			return made;
		}

		private void ApplyOutfit(string costumeId)
		{
			if (shownOutfit == costumeId)
				return;
			Color tint = Color.white;
			if (costumeId.Length > 0)
				ColorUtility.TryParseHtmlString(sim.Catalog.Get<CostumeDef>(costumeId).Look, out tint);
			player.SetOutfitTint(tint);
			shownOutfit = costumeId;
		}
	}

	// 머리 그림의 좌우 뒤집힘과 보임을 따라감
	public sealed class HomesteadHatFollower : MonoBehaviour
	{
		private SpriteRenderer head;
		private SpriteRenderer hat;

		public void Follow(SpriteRenderer headRenderer, SpriteRenderer hatRenderer)
		{
			head = headRenderer;
			hat = hatRenderer;
		}

		private void LateUpdate()
		{
			hat.flipX = head.flipX;
			hat.forceRenderingOff = head.enabled == false;
		}
	}
}
