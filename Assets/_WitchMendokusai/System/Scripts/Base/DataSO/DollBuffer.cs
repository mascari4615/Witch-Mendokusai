using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(DollBuffer), menuName = "DataBuffer/" + nameof(Doll))]
	public class DollBuffer : DataBufferSO<Doll> { }
}