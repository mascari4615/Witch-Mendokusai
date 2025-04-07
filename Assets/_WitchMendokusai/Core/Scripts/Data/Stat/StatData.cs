using System;
using UnityEngine;

namespace WitchMendokusai
{
	public abstract class StatData<T> : DataSO where T : Enum
	{
		[field: SerializeField] public T Type { get; set; }
	}
}