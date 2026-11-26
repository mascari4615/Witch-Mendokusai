namespace WitchMendokusai
{
	// CameraManager 의 외부 카메라 임대. 월드 대화 연출처럼 메인 카메라를 직접 움직이는 쪽이 잡는 동안
	// Cinemachine 최종 계산을 건너뜀. 돌려받으면 다음 프레임부터 원래 카메라 계산으로 복귀
	public partial class CameraManager
	{
		private object externalCameraOwner;

		public bool IsExternallyControlled => externalCameraOwner != null;

		// 이미 다른 주인이 잡고 있으면 거절
		public bool TryBeginExternalControl(object owner)
		{
			if (externalCameraOwner != null && externalCameraOwner != owner)
				return false;
			externalCameraOwner = owner;
			return true;
		}

		public void EndExternalControl(object owner)
		{
			if (externalCameraOwner == owner)
				externalCameraOwner = null;
		}
	}
}
