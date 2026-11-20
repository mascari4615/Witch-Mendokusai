using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary> 슬라임 왕 공격 패턴. 순서 목록과 개발 창 boss 명령의 이름. Projectile 은 직선 투척 </summary>
	public enum SlimeKingPattern
	{
		Projectile = 0,
		Dash = 1,
		Launch = 2,
		Pull = 3,
	}

	/// <summary>
	/// 슬라임 왕 공격 수치 한 벌. 수치는 전부 여기 (프리팹은 이 에셋 참조만)
	/// - 예고는 CombatFx 바닥 예고 부품 (보스 등급 프리셋). 같은 개념은 부품 하나
	/// - 패턴 순서: 처음 하나만 무작위, 이후 목록 순서로 돎
	/// </summary>
	[CreateAssetMenu(menuName = "WM/Slime King Attack Config")]
	public sealed class SlimeKingAttackConfig : ScriptableObject
	{
		[Header("공통")]
		[Min(0f), Tooltip("이 거리 안이면 공격 상태")] public float AttackRange = 15f;
		[Min(0f), Tooltip("패턴 사이 쉬는 시간 (초)")] public float WaitDuration = 2f;
		[Min(0f), Tooltip("추적 중 최소 이동 속도 (초당 월드 단위). 0 이면 스탯 그대로")] public float ApproachMinimumSpeed = 3f;
		[Min(0f), Tooltip("개발 창 boss 명령이 새로 꺼낼 때 플레이어에서 떨어뜨리는 거리 (월드 단위)")] public float DevSpawnDistance = 7f;
		[Tooltip("패턴 순서. 비면 투사체와 돌진")] public List<SlimeKingPattern> PatternOrder = new() { SlimeKingPattern.Projectile, SlimeKingPattern.Dash, SlimeKingPattern.Launch, SlimeKingPattern.Pull };

		[Header("직선 투척 (패턴 이름 Projectile)")]
		[Min(0.05f), Tooltip("조준 시간 (초). 이 동안 멈춰서 조준 띠가 차오름. 방향은 시작 때 고정")] public float ShotWindup = 0.9f;
		[Min(0.1f), Tooltip("던지는 거리 (월드 단위). 조준 띠 길이")] public float ShotRange = 11f;
		[Min(0.1f), Tooltip("조준 띠 폭 (월드 단위). 맞는 폭 (ShotHitRadius x 2) 보다 조금 넓게")] public float ShotWidth = 1.6f;
		[Min(0.05f), Tooltip("끝까지 날아가는 시간 (초)")] public float ShotFlightSeconds = 0.55f;
		[Min(0f), Tooltip("낮은 포물선 최고 높이 (월드 단위)")] public float ShotArcHeight = 0.6f;
		[Min(0.05f), Tooltip("날아가는 슬라임에 닿는 반지름 (월드 단위)")] public float ShotHitRadius = 0.7f;
		[Min(0), Tooltip("닿았을 때 피해. 0 이면 없음")] public int ShotDamage = 3;
		[Min(0f), Tooltip("던진 뒤 회복 (초)")] public float ShotRecovery = 0.5f;
		[Tooltip("끝에 닿으면 슬라임 소환 (발사와 같은 살아 있는 수 상한)")] public bool ShotSummonsSlime = true;
		[Tooltip("조준 띠 예고")] public CombatFx ShotTelegraph;

		[Header("반격 창 (돌진 회복, 빨아들이기 경직)")]
		[Tooltip("보스가 빈틈을 보이는 동안 발밑에 띄우는 예고. 두 패턴이 같이 씀. 비우면 표시 없음")] public CombatFx CounterWindowFx;
		[Min(0.1f), Tooltip("반격 창 원 반지름 (월드 단위)")] public float CounterWindowRadius = 2.2f;

		[Header("돌진")]
		[Min(0.01f)] public float DashSpeed = 15f;
		[Min(0.01f), Tooltip("기본 돌진 시간 (초). 도주 계산으로 늘어날 수 있음")] public float DashDuration = 0.5f;
		[Min(0f), Tooltip("돌진 전 준비 시간 (초). 이 동안 멈춰서 예고가 차오름")] public float DashPreDelay = 0.8f;
		[Min(0f), Tooltip("돌진 후 회복 시간 (초)")] public float DashPostDelay = 0.5f;
		[Range(0f, 1f), Tooltip("한 tick 에 계획 이동의 이 비율도 못 나가고 무언가에 닿아 있으면 부딪혀 멈춘 것으로 보고 바로 회복으로")] public float DashBlockedProgressRatio = 0.3f;
		[Tooltip("돌진 예고 (띠). 비우면 예고 없음")] public CombatFx DashTelegraph;
		[Min(0f), Tooltip("돌진 예고 폭 (월드 단위)")] public float DashTelegraphWidth = 2.4f;

		[Header("돌진 도주 보장")]
		[Min(0.01f)] public float MinimumEscapeSpeed = 0.96f;
		[Min(0.01f)] public float EscapeAcceleration = 100f;
		[Min(0f)] public float EscapeReactionSeconds = 0.5f;
		[Min(0f)] public float EscapePadding = 0.2f;
		[Min(0f)] public float MaximumFleeSpeed = 4f;
		[Min(0.01f)] public float MaximumDashDuration = 2.5f;
		[Min(0.01f)] public float MaximumDashDistance = 37.5f;

		[Header("슬라임 발사")]
		[Tooltip("발사해서 착지 자리에 소환할 몬스터")] public Monster LaunchMonster;
		[Min(1)] public int LaunchCount = 4;
		[Min(0f), Tooltip("첫 발사 전 멈칫 (초)")] public float LaunchWindup = 0.4f;
		[Min(0f), Tooltip("발사 간격 (초)")] public float LaunchInterval = 0.25f;
		[Min(0.05f), Tooltip("날아가는 시간 (초). 착지 예고가 이 시간 동안 차오름")] public float LaunchFlightSeconds = 0.9f;
		[Min(0f), Tooltip("포물선 최고 높이 (월드 단위)")] public float LaunchArcHeight = 3f;
		[Min(0f), Tooltip("착지 자리: 플레이어에서 최소 거리")] public float LaunchLandMinDistance = 1.5f;
		[Min(0f), Tooltip("착지 자리: 플레이어에서 최대 거리")] public float LaunchLandMaxDistance = 5f;
		[Range(0f, 1f), Tooltip("착지 각도 흔들림. 둘레를 나눈 칸 폭에 대한 비율")] public float LaunchAngleJitter = 0.5f;
		[Min(0), Tooltip("이 보스가 소환해 살아 있는 슬라임 상한. 넘으면 착지해도 소환 안 함. 0 이면 상한 없음")] public int LaunchMaxAlive = 8;
		[Min(0.1f), Tooltip("착지 예고 원 반지름 (월드 단위). 착지 피해 범위")] public float LaunchMarkerRadius = 1.1f;
		[Min(0), Tooltip("착지 때 원 안 플레이어 피해. 0 이면 없음")] public int LaunchLandingDamage = 2;
		[Min(0f), Tooltip("마지막 착지 뒤 회복 (초)")] public float LaunchRecovery = 0.6f;
		[Tooltip("착지 예고 (원)")] public CombatFx LaunchTelegraph;

		[Header("빨아들이기")]
		[Min(0.1f), Tooltip("끌어당김 범위 반지름 (월드 단위)")] public float PullRadius = 9f;
		[Min(0f), Tooltip("안쪽 접촉 피해 반지름 (월드 단위)")] public float PullCoreRadius = 1.6f;
		[Min(0f), Tooltip("예고 시간 (초). 끌어당기기 전")] public float PullWarningSeconds = 1f;
		[Min(0f), Tooltip("끌어당기는 시간 (초)")] public float PullSeconds = 2.5f;
		[Range(0f, 0.99f), Tooltip("끌어당김 속도. 플레이어 기본 이동 속도에 대한 비율. 1 미만이라 반대로 걸으면 벗어남")] public float PullSpeedRatio = 0.8f;
		[Min(0), Tooltip("안쪽 접촉 피해")] public int PullCoreDamage = 3;
		[Min(0.05f), Tooltip("안쪽 접촉 피해 간격 (초)")] public float PullCoreDamageInterval = 0.5f;
		[Min(0f), Tooltip("끝난 뒤 경직 (초). 반격 창")] public float PullStunSeconds = 1.2f;
		[Min(0.05f), Tooltip("끌림 유지 시간 (초). 매 tick 다시 걺. 보스가 사라지면 이 시간 뒤 풀림")] public float PullHoldSeconds = 0.2f;
		[Tooltip("끌어당김 범위 예고 (안쪽 화살 원)")] public CombatFx PullTelegraph;
		[Tooltip("안쪽 접촉 피해 범위 예고 (원)")] public CombatFx PullCoreTelegraph;
	}
}
