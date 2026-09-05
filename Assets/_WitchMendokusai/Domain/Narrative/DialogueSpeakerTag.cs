using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 「이 캐릭터가 원고에서 이 이름이다」 (TASK-WM-052).
	///
	/// ★ 왜 컴포넌트인가: 원고는 사람 이름으로 쓰는데(`&gt; 욘: "..."`), 게임은 그 글자가 누구인지 모른다.
	///   이걸 붙이고 이름만 적으면 그 캐릭터 위에 말풍선이 뜬다 — **씬 작업이 「부품 하나 + 글자 하나」로 끝난다.**
	///   (코드로 등록을 부탁하면 캐릭터마다 스크립트를 고쳐야 한다.)
	///
	/// 켜질 때 등록하고 꺼질 때 뺀다 — 죽은 캐릭터가 말풍선을 붙들고 있지 않게.
	/// 표가 아직 없어도 상관없다: 없으면 만들어 쓰므로 **대화 러너보다 먼저 깨어나도 된다**
	/// (초기화 순서에 안 걸린다 — 이 저장소에서 제일 자주 나는 사고다).
	/// </summary>
	[DisallowMultipleComponent]
	public class DialogueSpeakerTag : MonoBehaviour
	{
		[Tooltip("원고에 적는 이름 그대로. 비워 두면 이 오브젝트 이름을 쓴다.")]
		[SerializeField] private string speakerName;

		[Tooltip("말풍선이 붙을 자리. 비워 두면 이 오브젝트 자신(머리 위 빈 오브젝트를 두면 그걸 지정).")]
		[SerializeField] private Transform bubbleAnchor;

		/// <summary>실제로 등록되는 이름 — 비워 뒀으면 오브젝트 이름.</summary>
		public string ResolvedName => string.IsNullOrWhiteSpace(speakerName) ? name : speakerName.Trim();

		private Transform ResolvedAnchor => bubbleAnchor == null ? transform : bubbleAnchor;

		private void OnEnable()
		{
			DialogueSpeakerBridge.EnsureRegistry().Register(ResolvedName, ResolvedAnchor);
		}

		private void OnDisable()
		{
			DialogueSpeakerRegistry registry = DialogueSpeakerBridge.Current;
			if (registry == null)
			{
				return;
			}
			// 자기 것만 뺀다 — 같은 이름을 다른 캐릭터가 이미 가져갔으면 건드리지 않는다.
			registry.Unregister(ResolvedName, ResolvedAnchor);
		}
	}
}
