namespace WitchMendokusai
{
	/// <summary>
	/// 모드 진입점 interface. ModLoader 가 reflection 으로 발견 + ModContentRegistryHost ctor 가 Initialize(ctx) 호출.
	/// 모드 .dll 은 *DomainSDK 만 reference* — IMod 구현 시 Domain type 사용 X (asmdef 단방향 + WM-184 게이트가 sandbox 강제).
	/// TASK-WM-188 — Initialize() → Initialize(IModContext) 격상. ModKind = 2-패러다임 통합 taxonomy (Behavior / AssetOverlay).
	/// </summary>
	public interface IMod
	{
		string Name { get; }
		string Version { get; }
		ModKind Kind { get; }
		void Initialize(IModContext context);
	}
}
