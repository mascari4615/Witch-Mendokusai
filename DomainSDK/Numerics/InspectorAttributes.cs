using System;

namespace WitchMendokusai.Numerics
{
	/// <summary>
	/// 판정 층의 인스펙터 표기 속성. 엔진 의존 0
	/// Unity 안에서는 WM.Editor 의 drawer 가 Header, Tooltip 처럼 표시
	/// 엔진 밖에서는 무동작
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
