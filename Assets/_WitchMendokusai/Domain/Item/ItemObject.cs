using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai
{
	public class ItemObject : LootObject
	{
		[SerializeField] private SpriteRenderer spriteRenderer;
		private ItemData itemData;
		private TraceType trace;

		private SOManager soManager;
		private DataManager dataManager;

		[Inject]
		public void Construct(SOManager soManager, DataManager dataManager)
		{
			this.soManager = soManager;
			this.dataManager = dataManager;
		}

		// 풀 재사용 몸이라 자취는 매번 덮음
		public void Init(ItemData itemData, TraceType trace = TraceType.None)
		{
			this.itemData = itemData;
			this.trace = trace;
			spriteRenderer.sprite = itemData.Sprite;
		}

		protected override void OnEffect()
		{
			CombatSfx.Play(CombatSfxEvent.Equip, transform.position);
			soManager.ItemInventory.Add(itemData);
			soManager.DungeonItemBuffer.Add(itemData);
			if (trace != TraceType.None)
				dataManager.FeatureSave<TraceSaveSlice>().Notebook.Record(itemData.ID, trace);
		}
	}
}