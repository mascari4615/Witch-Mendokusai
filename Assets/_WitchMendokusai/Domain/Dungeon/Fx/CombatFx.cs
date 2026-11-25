using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 전투 연출 한 번에 넘기는 값. 부품마다 쓰는 칸이 다르고 안 쓰는 칸은 무시
	/// 길이 단위는 월드 단위, 시간은 초
	/// </summary>
	public struct CombatFxContext
	{
		/// <summary> 연출이 놓일 자리 (바닥 예고면 바닥 위 중심, 선형이면 시작점) </summary>
		public Vector3 Position;
		/// <summary> 날아오는 것의 출발 (시전자 자리) </summary>
		public Vector3 Origin;
		/// <summary> 향하는 쪽 (선형, 부채꼴). 수평 성분만 씀 </summary>
		public Vector3 Direction;
		/// <summary> 따라다닐 대상. 비우면 Position 에 고정 </summary>
		public Transform Follow;
		public float Radius;
		public float Length;
		public float Width;
		/// <summary> 걸리는 시간. 0 이하면 부품의 기본값 </summary>
		public float Seconds;
		/// <summary> 띠에 쓸 글 (이름, 경고 문구) </summary>
		public string Title;
		public string Subtitle;
		/// <summary> 묶음 (SequenceFx) 안에서 재생된 깊이. 쓰는 쪽은 안 채움 </summary>
		public int NestingDepth;
	}

	/// <summary>
	/// 전투 연출 부품 한 종류. 에셋 하나가 연출 하나 (UI 쪽 UIWipe, UIIdleEffect 와 같은 꼴)
	/// - 몬스터, 보스, 맵의 외형과 무관하게 끼워 쓰는 것만. 예고, 경고 띠, 등장과 사망, 승패 연출
	/// - 쓰는 쪽은 에셋을 칸에 끼우고 Play 만 부름. 돌려받은 함수를 부르면 도중에 멈춤
	/// - 수치는 전부 에셋의 직렬화 필드. Play 중 에셋을 고치면 다음 재생부터 반영
	/// - 미리보기: Play 중 에셋 우클릭 Preview, 개발 창 명령 fx
	/// </summary>
	public abstract class CombatFx : ScriptableObject
	{
		private static readonly List<CombatFx> loaded = new();

		/// <summary> 지금 메모리에 올라온 부품. 개발 창 목록용 </summary>
		public static IReadOnlyList<CombatFx> Loaded => loaded;

		[SerializeField, Tooltip("미리보기에서 쓸 반지름")] private float previewRadius = 3f;
		[SerializeField, Tooltip("미리보기에서 쓸 길이")] private float previewLength = 8f;
		[SerializeField, Tooltip("미리보기에서 쓸 폭")] private float previewWidth = 2f;
		[SerializeField, Tooltip("미리보기에서 쓸 글")] private string previewTitle = "미리보기";
		[SerializeField] private string previewSubtitle = "";

		protected virtual void OnEnable()
		{
			if (loaded.Contains(this) == false)
				loaded.Add(this);
		}

		protected virtual void OnDisable() => loaded.Remove(this);

		/// <summary> 재생. 돌려받은 함수를 부르면 도중에 멈추고 치움 (끝난 뒤 불러도 무해) </summary>
		public Action Play(CombatFxContext context) => Play(CombatFxRunner.Instance, context);

		protected abstract Action Play(CombatFxRunner runner, CombatFxContext context);

		/// <summary> 플레이어 자리 (없으면 원점) 에서 미리보기 값으로 재생 </summary>
		public Action PlayPreview()
		{
			Vector3 origin = Vector3.zero;
			Vector3 forward = Vector3.forward;
			Transform follow = null;
			if (PlayerProvider.TryGetExistingInstance(out PlayerProvider playerProvider) && playerProvider.Current != null)
			{
				follow = playerProvider.Current.transform;
				origin = follow.position;
				forward = follow.forward;
			}

			// 인형 몸은 2D 그림을 카메라 쪽으로 기울여서 정면이 수평이 아님. 수평으로 눌러야 바닥 위에 놓임
			forward.y = 0f;
			if (forward.sqrMagnitude < 0.0001f && Camera.main != null)
			{
				forward = Camera.main.transform.forward;
				forward.y = 0f;
			}
			forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;

			return Play(new CombatFxContext
			{
				Position = origin,
				Direction = forward,
				Follow = follow,
				Radius = previewRadius,
				Length = previewLength,
				Width = previewWidth,
				Title = previewTitle,
				Subtitle = previewSubtitle,
			});
		}

		[ContextMenu("Preview")]
		private void PreviewFromMenu()
		{
			if (Application.isPlaying == false)
			{
				Debug.LogWarning($"{name}: 미리보기는 Play 중에만");
				return;
			}
			PlayPreview();
		}
	}
}
