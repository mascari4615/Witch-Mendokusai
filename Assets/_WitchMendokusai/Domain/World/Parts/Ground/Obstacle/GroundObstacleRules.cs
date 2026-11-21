using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 지형 장애물 한 종류. 모델은 기존 숲 장식 프리팹 재사용
	[Serializable]
	public sealed class GroundObstacleKind
	{
		public string Name = "나무";
		public GameObject Prefab;
		[Tooltip("비우면 프리팹 원래 재질")] public Material MaterialOverride;
		public Vector2 ScaleRange = new(.8f, 1.3f);
		[Min(0f), Tooltip("0 이면 프리팹 자체 캡슐 충돌체를 씀. 0 보다 크면 이 반지름 (배율 1 기준) 으로 캡슐을 붙임 (m)")] public float ColliderRadius;
		[Min(0.1f), Tooltip("ColliderRadius 가 0 보다 클 때 캡슐 높이 (m). 계단 오르기보다 높아야 막음")] public float ColliderHeight = 2f;
		[Min(0f)] public float Weight = 1f;
		[Tooltip("카메라와 캐릭터 사이에 걸리면 점무늬로 비움 (OcclusionFadeTarget)")] public bool CameraFade = true;
	}

	// 판 시드 재현 배치 규칙. 값만 담아 시험이 직접 생성
	[Serializable]
	public sealed class GroundObstacleRules
	{
		public GroundObstacleKind[] Kinds = Array.Empty<GroundObstacleKind>();
		public int SeedSalt = 7919;
		[Min(1f), Tooltip("칸 한 변 (m). 칸마다 장애물은 최대 하나")] public float CellSize = 6f;
		[Range(0f, 1f), Tooltip("칸에 장애물이 생길 확률. 밀도 = 확률 / 칸 면적")] public float Probability = .35f;
		[Min(0f), Tooltip("이웃 장애물 중심 사이 최소 거리 (m). 칸 크기보다 작아야 함. 어떤 두 장애물 사이든 이 거리 이상 보장")] public float MinSpacing = 3.4f;
		[Min(0f), Tooltip("판 시작 위치 주변 비움 반지름 (m)")] public float StartClearRadius = 8f;
		[Min(1f), Tooltip("길 무늬 크기 (m). 클수록 길이 느리게 굽음")] public float PathNoiseScale = 28f;
		[Range(0f, .5f), Tooltip("길 반폭 (무늬 값 단위). 0 이면 길 없음. 길 안에는 장애물을 두지 않음")] public float PathHalfWidth = .07f;
		[Min(1f), Tooltip("플레이어 주변 이 거리 안의 칸만 물체 배치 (m)")] public float ActiveRadius = 50f;
		[Min(0f), Tooltip("몹 생성 자리가 장애물 가장자리에서 떨어질 여유 (m)")] public float SpawnClearance = 1f;

		public float MaxScale(GroundObstacleKind kind) => Mathf.Max(kind.ScaleRange.x, kind.ScaleRange.y);

		// 설정 오류 목록. 입장 전 검사용
		public void Validate(List<string> errors)
		{
			if (Kinds == null || Kinds.Length == 0)
			{
				errors.Add("장애물 종류 없음");
				return;
			}
			float totalWeight = 0f;
			float maxDiameter = 0f;
			foreach (GroundObstacleKind kind in Kinds)
			{
				if (kind == null || kind.Prefab == null)
				{
					errors.Add("장애물 프리팹 미연결");
					continue;
				}
				totalWeight += kind.Weight;
				float radius = kind.ColliderRadius > 0f ? kind.ColliderRadius : PrefabRadius(kind);
				if (radius <= 0f)
					errors.Add($"{kind.Name}: 충돌 반지름 0 (프리팹에 캡슐 충돌체가 없고 ColliderRadius 도 0)");
				maxDiameter = Mathf.Max(maxDiameter, radius * MaxScale(kind) * 2f);
			}
			if (totalWeight <= 0f)
				errors.Add("장애물 가중치 합 0");
			if (MinSpacing >= CellSize)
				errors.Add("MinSpacing 은 CellSize 보다 작아야 함");
			// 몸 통과 틈 최소 1.2m 보장
			if (MinSpacing - maxDiameter < 1.2f)
				errors.Add($"틈 부족: MinSpacing {MinSpacing} - 최대 지름 {maxDiameter:F2} < 1.2m (끼임 방지)");
		}

		public static float PrefabRadius(GroundObstacleKind kind)
		{
			if (kind.Prefab == null)
				return 0f;
			CapsuleCollider capsule = kind.Prefab.GetComponentInChildren<CapsuleCollider>(true);
			if (capsule == null)
				return 0f;
			Vector3 scale = capsule.transform.lossyScale;
			return capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
		}
	}
}
