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

		public UltimateFx Body => body;
		public UltimateScreenFeel Feel => feel;

		public override float DurationSeconds => body != null ? body.DurationSeconds : 0f;

		public override void Compose(UltimateFxRun run)
		{
			if (body == null || feel == null)
				throw new InvalidOperationException($"{name}: body 또는 feel 미연결. 메뉴 WM/CombatFx/Build Ultimate Feel V2");
			if (body is FeelUltimateFx)
				throw new InvalidOperationException($"{name}: body 가 FeelUltimateFx (겹쳐 입히기 금지)");
			body.Compose(run);
			Vector3 target = run.Center + run.ScreenForward * feel.TargetForward;
			target.y = UltimateFxStage.GroundHeight(target);
			UltimateScreenFeelDriver driver = new(run, feel, target);
			// body 의 트랙 (장면 빛 곡선 포함) 뒤에 올려 같은 프레임에 그 값 위로 더함
			run.Track(0f, DurationSeconds + TailSeconds + feel.TotalHoldSeconds, _ => driver.Update());
		}
	}
}
