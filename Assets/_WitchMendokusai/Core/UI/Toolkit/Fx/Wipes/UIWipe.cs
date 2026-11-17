using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 전환은 종류마다 에셋 하나. 쓰는 자리 (UIFxSettings 기본 칸, Portal, SceneFader.LoadScene 과 TransitionView.Transition 호출) 에 에셋을 끼워 바꿈
	/// - 그리는 전환은 SingleUIWipe, 여럿 중 고르는 묶음은 RandomUIWipe
	/// - 새 종류: SingleUIWipe 상속 클래스 하나 (파일 이름과 클래스 이름이 같게) 와 IWipeRenderer 구현 하나. 무늬 지도 계열은 PatternUIWipe 상속에 Order 하나
	/// - 같은 종류의 변형: 에셋 복제 (Ctrl+D) 뒤 수치만 바꿈
	/// - 이름 규격 Wipe_{ID}_{이름}.asset (Presets 폴더)
	/// </summary>
	public abstract class UIWipe : ScriptableObject
	{
		/// <summary>어느 전환 에셋이든 인스펙터에서 바뀔 때마다 증가. 계산해 둔 무늬를 다시 만들 때 비교</summary>
		public static int Version { get; private set; }

		/// <summary>이번 전환에 그릴 에셋. 묶음은 후보 중 하나</summary>
		public abstract SingleUIWipe Pick();

		private void OnValidate() => Version++;

		[ContextMenu("Preview")]
		private void Preview() => SceneFader.PreviewWipe(this);
	}
}
