using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(DollBuffer), menuName = "WM/DataBuffer/" + nameof(Doll))]
	public class DollBuffer : DataBufferSO<Doll> { }
}