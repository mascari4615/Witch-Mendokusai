using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.MHelper;

namespace WitchMendokusai
{
	public class PoolingObject : MonoBehaviour
	{
		private void OnDisable()
		{
			if (IsPlaying == false)
				return;

			ObjectPoolManager.Instance.Despawn(gameObject);
		}
	}
}