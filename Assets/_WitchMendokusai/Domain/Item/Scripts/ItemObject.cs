using System;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;
using VContainer;

namespace WitchMendokusai
{
	public class ItemObject : LootObject
	{
		[SerializeField] private SpriteRenderer spriteRenderer;
		private ItemData itemData;

		private SOManager soManager;

		[Inject]
		public void Construct(SOManager soManager)
		{
			this.soManager = soManager;
		}

		public void Init(ItemData itemData)
		{
			this.itemData = itemData;
			spriteRenderer.sprite = itemData.Sprite;
		}

		protected override void OnEffect()
		{
			RuntimeManager.PlayOneShot("event:/SFX/Equip", transform.position);
			soManager.ItemInventory.Add(itemData);
			soManager.DungeonItemBuffer.Add(itemData);
		}
	}
}