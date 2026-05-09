using System;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(DateTimeVariable), menuName = "WM/Variable/DateTime")]
	public class DateTimeVariable : CustomVariable<DateTime> { }
}