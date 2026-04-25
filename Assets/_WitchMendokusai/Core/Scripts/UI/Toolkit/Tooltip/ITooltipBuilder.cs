namespace WitchMendokusai
{
	/// <summary>
	/// 데이터 타입별 툴팁 컨텐츠 빌더.
	/// view는 매번 호출 전 controller가 children clear한 상태로 전달.
	/// 빌더는 자유롭게 element 추가 + USS class 부여 가능.
	/// </summary>
	public interface ITooltipBuilder
	{
		void Build(TooltipView view, object data, TooltipMode mode);
	}
}
