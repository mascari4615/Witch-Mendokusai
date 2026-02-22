using System;
using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace WitchMendokusai
{
	public class MagnetObject : GameItemObject
	{
		[SerializeField] private bool isImmediate = false;

		protected override void OnEffect()
		{
			List<MagnetObject> magnetObjects = new();

			ObjectBufferManager.GetObjects(ObjectType.Drop).ForEach(item =>
			{
				if ((item == null) || (item == gameObject))
					return;

				if (item.TryGetComponent(out LootObject lootObject))
				{
					if (lootObject is MagnetObject)
					{
						magnetObjects.Add(lootObject as MagnetObject);
						// lootObject.gameObject.SetActive(false);
						return;
					}

					if (isImmediate)
					{
						lootObject.Effect();
					}
					else
					{
						lootObject.Equip();
					}
				}
			});

			magnetObjects.ForEach(magnet => magnet.gameObject.SetActive(false));
		}
	}
}