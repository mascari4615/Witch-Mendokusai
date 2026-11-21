using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 연출을 입힌 궁극기. 주제 연출 (메시, 입자, 광원, 바닥 반응) 은 다른 궁극기 에셋 (body) 그대로, 화면 장치 (시간, 카메라, 후처리, 화면 왜곡) 는 박자표 (feel) 로
	/// - 강한 판 V2 넷 (하늘 검, 창 왕관, 카드, 달) 이 이 클래스의 에셋 넷. body 는 V1 강한 판을 복사한 별개 에셋이라 V1 과 나란히 비교 가능
	/// - 정지 한 박 동안 주제 연출 시간선도 멈춤 (진행기 시계 배율). body 의 시간 값은 정지를 뺀 연출 시간
	/// - 재생: 개발 창 fx ultimate &lt;에셋 이름&gt;, 프리셋은 메뉴 WM/CombatFx/Build Ultimate Feel V2
	/// </summary>
	[CreateAssetMenu(fileName = "Ult3D_SkySwordHeavyV2", menuName = "WM/CombatFx/Feel Ultimate")]
	public sealed class FeelUltimateFx : UltimateFx
	{
		[Header("구성")]
		[SerializeField, Tooltip("주제 연출 에셋. 자기 자신이나 다른 FeelUltimateFx 금지")] private UltimateFx body;
		[SerializeField] private UltimateScreenFeel feel;
		[SerializeField, Range(0.5f, 2.5f), Tooltip("주제 연출 크기 배율 (반경, 높이, 물체 크기). 1 이면 body 그대로. 실행용 복제본에만 적용")] private float worldSize = 1f;

		public UltimateFx Body => body;
		public UltimateScreenFeel Feel => feel;
		public float WorldSize => worldSize;

		public override float DurationSeconds => body != null ? body.DurationSeconds : 0f;

		public override void Compose(UltimateFxRun run)
		{
			if (body == null || feel == null)
				throw new InvalidOperationException($"{name}: body 또는 feel 미연결. 메뉴 WM/CombatFx/Build Ultimate Feel V2");
			if (body is FeelUltimateFx)
				throw new InvalidOperationException($"{name}: body 가 FeelUltimateFx (겹쳐 입히기 금지)");
			float length = DurationSeconds + TailSeconds + feel.TotalHoldSeconds;
			UltimateFx source = body;
			if (Mathf.Approximately(worldSize, 1f) == false)
			{
				// 트랙이 끝까지 복제본 값을 읽으므로 실행이 끝날 때 지움
				source = Instantiate(body);
				source.hideFlags = HideFlags.DontSave;
				source.ScaleWorld(worldSize);
				UltimateFx scaled = source;
				run.Track(0f, length, _ => { }, () => Destroy(scaled));
			}
			source.Compose(run);
			Vector3 target = run.Center + run.ScreenForward * feel.TargetForward;
			target.y = UltimateFxStage.GroundHeight(target);
			UltimateScreenFeelDriver driver = new(run, feel, target, DurationSeconds);
			// body 의 트랙 (장면 빛 곡선 포함) 뒤에 올려 같은 프레임에 그 값 위로 더함
			run.Track(0f, length, _ => driver.Update());
		}
	}
}
