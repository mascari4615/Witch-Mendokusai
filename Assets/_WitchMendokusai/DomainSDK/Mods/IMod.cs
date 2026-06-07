namespace WitchMendokusai
{
	/// <summary>
	/// 모드 진입점 interface. ModLoader 가 RuntimeInitializeOnLoadMethod 시점에 모든 IMod 구현을 reflection 으로 발견 + Initialize(context) 호출.
	/// 모드 .dll 은 *DomainSDK 만 reference* — IMod 구현 시 Domain type 사용 X (sandbox 강제).
	/// Initialize(IModContext) = 콘텐츠 등록 seam (껍데기 아닌 실기능). TASK-WM-188.
	/// </summary>
	public interface IMod
	{
		string Name { get; }
		string Version { get; }
		void Initialize(IModContext context);
	}
}
