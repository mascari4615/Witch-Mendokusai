using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "AD_", menuName = "Variable/" + nameof(AspectData))]
	public class AspectData : StatData<AspectType>
	{
	}
}