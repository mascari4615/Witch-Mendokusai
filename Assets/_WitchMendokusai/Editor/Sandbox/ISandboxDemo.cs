using UnityEngine;

namespace WitchMendokusai.Sandbox
{
	// 한 기능을 격리된 미니 무대에서 시각 검증하는 provisioner. 구현체를 에디터 어셈블리(WM.Editor 등)에 두면
	// SandboxRegistry 가 TypeCache 로 자동발견. 새 데모 = 이 인터페이스 구현 + 파라미터 없는 ctor 만.
	//
	// ★ WM 부트스트랩(playModeStartScene=Boot)이 Play 를 World 로 가로채고, 단일 공유 에디터라 Play=다세션 방해.
	//   그래서 Sandbox 는 Play 를 쓰지 않고 *에디트 모드*에서 Build() 로 오브젝트를 깔고(Start 안 돎)
	//   EditorApplication.update 로 Tick() 을 돌려 눈으로 보여준다(WM-177).
	public interface ISandboxDemo
	{
		// 갤러리에 표시될 이름.
		string Title { get; }

		// 분류(폴더처럼 묶음). 예: "Farming".
		string Category { get; }

		// 격리 무대(빈 additive 씬)에 검증 대상을 구성하고 루트를 반환. 땅/조명은 SandboxStage 가 깐다.
		// 에디트 모드에서 호출되므로 여기서 가시 오브젝트(큐브 등)를 즉시 생성해야 한다(런타임 Start 안 돎).
		GameObject Build();
	}

	// Build 후 시간 흐름을 보여주는 데모가 추가 구현. SandboxStage 가 TickInterval 마다 Tick() 호출(에디터 틱).
	public interface ISandboxAnimatedDemo : ISandboxDemo
	{
		// Tick 사이 간격(에디터 시간 초).
		float TickInterval { get; }

		// 한 스텝 진행(예: 온실 하루 경과). 가시 상태를 갱신해야 한다.
		void Tick();
	}
}
