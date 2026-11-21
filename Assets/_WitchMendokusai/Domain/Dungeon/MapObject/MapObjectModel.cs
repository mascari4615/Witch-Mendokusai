using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 맵 오브젝트 외형 부품 모양. 저폴리, 면마다 정점을 따로 둬 각진 음영 (PS 결)
	public enum MapObjectPartShape
	{
		Box = 0,
		// 옆면 Sides 개 기둥. TopScale 로 위를 좁히거나 넓힘 (오벨리스크, 그릇)
		Prism = 1,
		// 위아래 뾰족한 결정. Sides 개 옆면
		Crystal = 2,
	}

	// 재질 종류. 돌은 지면 바위와 같은 텍스처 재질 (StoneMaterial), 빛은 정점 색 그대로 그리는 표식 재질 (밤에도 보임)
	public enum MapObjectPartSurface
	{
		Stone = 0,
		Glow = 1,
	}

	[Serializable]
	public class MapObjectModelPart
	{
		[field: SerializeField] public MapObjectPartShape Shape { get; private set; }
		[field: SerializeField] public MapObjectPartSurface Surface { get; private set; }
		[field: SerializeField, Tooltip("돌은 재질 색에 곱, 빛은 그 색으로 그림")] public Color Color { get; private set; } = Color.white;
		[field: SerializeField, Tooltip("오브젝트 중심 기준 (m). 바닥이 0")] public Vector3 Position { get; private set; }
		[field: SerializeField, Tooltip("도")] public Vector3 Rotation { get; private set; }
		[field: SerializeField, Tooltip("가로, 높이, 세로 (m)")] public Vector3 Size { get; private set; } = Vector3.one;
		[field: SerializeField, Tooltip("Prism, Crystal 옆면 수"), Range(3, 16)] public int Sides { get; private set; } = 6;
		[field: SerializeField, Tooltip("Prism 윗면 크기 배율"), Min(0f)] public float TopScale { get; private set; } = 1f;
		[field: SerializeField, Tooltip("같은 부품을 중심 둘레에 몇 개. 1 이면 Position 그대로")] [field: Min(1)] public int RadialCount { get; private set; } = 1;
		[field: SerializeField, Tooltip("RadialCount 2 이상일 때 둘레 반지름 (m)"), Min(0f)] public float RadialRadius { get; private set; }
		[field: SerializeField, Tooltip("둘레 첫 자리 각도 (도)")] public float RadialStartAngle { get; private set; }
		[field: SerializeField, Tooltip("Y 축 회전 (도/초). 0 이면 멈춤")] public float SpinSpeed { get; private set; }
		[field: SerializeField, Tooltip("위아래 흔들림 폭 (m). 0 이면 멈춤"), Min(0f)] public float BobHeight { get; private set; }
		[field: SerializeField, Tooltip("흔들림 한 번 (초)"), Min(0.1f)] public float BobPeriod { get; private set; } = 2f;
		[field: SerializeField, Tooltip("이 부품 크기 상자 충돌체 (주인공과 적이 못 지나감)")] public bool Solid { get; private set; }
	}

	// 맵 오브젝트 외형 한 벌. 코드로 짓는 저폴리 부품 목록. 오브젝트 데이터 (MapObjectData.Model) 가 참조
	// 새 외형은 자산 하나. 부품 모양 추가만 코드 (MapObjectModelMesh)
	[CreateAssetMenu(fileName = "MOM_", menuName = "WM/Variable/" + nameof(MapObjectModel))]
	public class MapObjectModel : ScriptableObject
	{
		[field: SerializeField] public List<MapObjectModelPart> Parts { get; private set; } = new();

		public void Validate(List<string> errors)
		{
			if (Parts.Count == 0)
				errors.Add($"{name}: 부품 없음");
			foreach (MapObjectModelPart part in Parts)
			{
				if (part.Size.x <= 0f || part.Size.y <= 0f || part.Size.z <= 0f)
					errors.Add($"{name}: 부품 크기 {part.Size}");
				if (part.RadialCount > 1 && part.RadialRadius <= 0f)
					errors.Add($"{name}: 둘레 {part.RadialCount}개인데 반지름 0");
			}
		}
	}
}
