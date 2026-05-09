namespace WitchMendokusai
{
	// Legacy: UI Toolkit DollView 로 대체됨. Tab 시스템에서 분리되어 더 이상 인스턴스화 안 됨.
	// prefab 정리 시 본 파일 + UIDollGrid + 관련 prefab 모두 제거 예정.
	public class UIDollPanel : UIPanel
	{
		public override bool IsFullscreen => true;
		protected override void OnInit() { }
		public override void UpdateUI() { }
	}
}
