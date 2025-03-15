using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class Building : MonoBehaviour
	{
		public RuntimeBuildingData Data { get; private set; }

		public void Initialize(BuildingData so)
		{
			Data = new RuntimeBuildingData(so);
		}
	}
}