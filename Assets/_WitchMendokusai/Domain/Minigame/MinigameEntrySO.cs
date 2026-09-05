using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 티메토 허브에 뜨는 「게임 속 게임」 한 줄 (TASK-WM-195).
	/// 정본 = `memo/wm/design/systems/game-in-game-hub.md` — WM 본편이 제1 세계이고,
	/// 모든 미니게임은 티메토 NPC 대화 → 허브 UI 라는 *단일 관문*으로만 들어간다.
	///
	/// 아이콘/이름/설명은 <see cref="DataSO"/> 베이스가 이미 제공 — 여기선 "어느 모드로 가나"만 더한다.
	/// 미니게임 추가 = 이 SO 에셋 1개 + 티메토 NPC 의 PanelInfos 에 추가 (코드 수정 0).
	/// </summary>
	[CreateAssetMenu(fileName = nameof(MinigameEntrySO), menuName = "WM/Hub/MinigameEntrySO")]
	public class MinigameEntrySO : DataSO
	{
		[field: Header("_" + nameof(MinigameEntrySO))]
		[field: Tooltip("입장 시 전환할 게임 모드 — 실제 진입/이탈 처리는 각 모드 컨트롤러가 OnModeChanged 로 전담.")]
		[field: SerializeField] public GameMode TargetGameMode { get; private set; } = GameMode.Default;

		[field: Tooltip("한 줄 부제 — 목록에서 이름 아래 작게. 비우면 Description 만 쓴다.")]
		[field: SerializeField] public string Tagline { get; private set; } = string.Empty;
	}
}
