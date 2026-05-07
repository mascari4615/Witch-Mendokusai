namespace WitchMendokusai
{
	// Legacy: StatusView (UI Toolkit) 로 대체됨. World 씬 [Block] Status.prefab 가 본 컴포넌트를
	// 참조하고 있어 missing component 경고 방지용으로 stub 유지. prefab + 씬 정리 시 본 파일 제거.
	public class UIStatus : UIBase
	{
		public override void Init() { }
		public override void UpdateUI() { }
	}
}
