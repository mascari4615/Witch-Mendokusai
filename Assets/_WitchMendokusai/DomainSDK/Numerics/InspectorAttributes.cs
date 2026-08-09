#if !UNITY_5_3_OR_NEWER
using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// 엔진 밖 빌드에서만 존재하는 <b>무음 대역</b> (TASK-WM-214).
	/// [Header]/[Tooltip] 은 Unity 인스펙터 UX 라 엔진 밖에선 의미가 없지만, 소스는 한 벌뿐이므로
	/// 컴파일은 통과해야 한다. Unity 안에서는 이 파일이 통째로 꺼지고 각 파일의 using 별칭이
	/// 진짜 UnityEngine 속성으로 되돌린다 — 즉 <b>디자이너가 보는 인스펙터는 그대로</b>다.
	/// </summary>
	[AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
	public sealed class HeaderAttribute : Attribute
	{
		public readonly string header;

		public HeaderAttribute(string header)
		{
			this.header = header;
		}
	}

	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public sealed class TooltipAttribute : Attribute
	{
		public readonly string tooltip;

		public TooltipAttribute(string tooltip)
		{
			this.tooltip = tooltip;
		}
	}
}
#endif
