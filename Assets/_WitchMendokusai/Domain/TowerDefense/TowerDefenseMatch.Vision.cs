using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ★ 이 파일의 좌표는 「판정 쪽」이다 (TASK-WM-214).
//   개척 판의 셈은 거의 전부 시뮬이고(Vector3 118 · Vector2Int 27 · Vector3Int 13),
//   엔진을 실제로 만지는 자리는 스무 곳 남짓((Vector3)transform.position 등)이다.
//   그래서 이 파일에서 Vector* 는 SDK 타입을 뜻하고, 엔진으로 나갈 때만 자동으로 변환된다.
//   반대로 엔진 값을 받아올 때는 캐스트가 필요하다 — 그 자리가 곧 경계다.
using Vector2 = WitchMendokusai.Numerics.Vector2;
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;

namespace WitchMendokusai
{
	// TowerDefenseMatch 의 시야와 신호 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 TowerDefenseMatch.cs 를 본다.
	public partial class TowerDefenseMatch : MonoBehaviour
	{
		// 신호장 그림 — 덮인 땅의 테두리와 퍼져 나가는 파동. 무대가 서는 순간 만들어진다.
		private TowerDefenseSignalView signalView;

		// 이번 매치의 판 — 절차 생성이면 layout 이 정본, 끄면 null 이고 스테이지 SO 의 고정 레이아웃을 쓴다.
		// 아래 active* 목록이 *둘을 하나로 합친 단일 출처* — 매치 본문은 어느 쪽인지 신경 쓰지 않는다.
		// 시야 — 내 건물이 밝힌 만큼만 보인다. 건물은 안 움직이므로 *지어질 때만* 다시 계산한다.
		private TowerDefenseVision vision;
		private TowerDefenseFogView fogView;
		private readonly List<TowerDefenseVision.Source> visionSources = new();
		private readonly List<TowerDefenseVision.Source> scaledVisionSources = new();

		/// <summary>
		/// 그 자리를 밝힌다(검증 전용) — 「밝힌 서식지만 지도에 뜬다」는 규칙 때문에, 밝히지 않으면
		/// 그 표시를 영영 못 잰다(못 잰 것을 통과로 세면 검사가 있으나 마나다).
		/// </summary>
		public void RevealForVerification(Vector3 worldPosition, float radius)
		{
			AddVisionSource(worldPosition, radius);
		}

		// ── 라이브 검증용 창 ─────────────────────────────────────────────────────────
		// ★ 「돌아간다」를 사람 눈에만 맡기면 영영 안 재게 된다. 하네스가 판을 돌리며 직접 물어볼 수
		//   있어야 신호·서식지·침공이 *실제로* 살아 있는지 매번 확인된다(안 그러면 컴파일만 초록).
		public float CoreSignalCharge => powerGrid.Field.ChargeAt(0);
		public float CoreSignalRadius => powerGrid.Field.LiveRadiusAt(0);
		public int SignalNodeCount => powerGrid.Field.NodeCount;

		/// <summary> 그 자리가 지금 보이는가 — 안 보이면 포탑도 못 쏘고 마수도 안 그려진다. </summary>
		public bool IsVisibleAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true; // 시야 없는 판(고정 레이아웃) = 전부 보임.

			return vision.IsVisible(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim()));
		}

		/// <summary> 한 번이라도 밝혔던 자리인가 — 기억한 지형·노드는 계속 보여준다. </summary>
		public bool IsExploredAt(Vector3 worldPosition)
		{
			if (vision == null || mapLayout == null || stageRoot == null)
				return true;

			return vision.IsExplored(mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim()));
		}

		/// <summary> 시야원 하나 추가 + 즉시 반영 — 건물을 세운 그 순간 밝아져야 「넓혔다」가 읽힌다. </summary>
		private void AddVisionSource(Vector3 worldPosition, float radius)
		{
			if (vision == null || mapLayout == null || stageRoot == null || radius <= 0f)
				return;

			visionSources.Add(new TowerDefenseVision.Source(
				mapLayout.WorldToCell(stageRoot.InverseTransformPoint(worldPosition.ToUnity()).ToSim()), radius));
			RefreshVision();
		}

		private void RefreshVision()
		{
			if (vision == null)
				return;

			// 어스름 웨이브면 모든 시야가 함께 좁아진다 — 「보이는 만큼만 쏜다」가 아프게 걸린다.
			float visionScale = CurrentVisionScale() * boons.VisionMultiplier;
			if (Mathf.Approximately(visionScale, 1f))
			{
				vision.Recompute(visionSources);
			}
			else
			{
				scaledVisionSources.Clear();
				foreach (TowerDefenseVision.Source source in visionSources)
					scaledVisionSources.Add(new TowerDefenseVision.Source(source.Cell, source.Radius * visionScale));
				vision.Recompute(scaledVisionSources);
			}
			if (fogView != null)
				fogView.Apply(vision);
		}

		/// <summary> 신호장을 화면에 그린다. 무대가 있어야 그릴 자리가 생기므로 여기서 늦게 만든다. </summary>
		private void TickSignalView()
		{
			if (stageRoot == null || stage == null)
				return;

			if (signalView == null)
				signalView = TowerDefenseSignalView.Create(stageRoot);

			signalView.Tick(powerGrid.Field, stage, Time.deltaTime);
		}

		/// <summary> 지금 웨이브의 시야 배수 — 어스름이면 좁아진다. </summary>
		private float CurrentVisionScale()
		{
			return TowerDefenseWaveEvent.VisionScale(WaveEventAt(core != null ? core.WaveIndex : 0));
		}
	}
}
