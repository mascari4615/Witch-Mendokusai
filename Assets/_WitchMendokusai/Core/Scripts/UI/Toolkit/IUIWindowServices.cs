namespace WitchMendokusai
{
	/// <summary>
	/// panel-root owner-push 핸들의 Core-가시 facet (TASK-WM-133 증분 2).
	/// UIServices(WM.Domain) 가 구현 — Domain→Core 단방향이라 Core 의 WMWindow
	/// 는 본 인터페이스로만 panel-scoped WindowManager 획득 (DIP, WM IXxxBridge
	/// 기존 패턴 정합). global static Instance reach 제거.
	/// </summary>
	public interface IUIWindowServices
	{
		WindowManager WindowManager { get; }
	}
}
