using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 비-지형 동숲 스타일 자연 오브젝트(나무/꽃/바위/마녀의집 등)의 데이터 정의.
	/// 지형은 voxel block, 비지형은 EntityData prefab 인스턴스.
	/// SOManager.DataSOs[typeof(EntityData)]로 자동 등록 (DataSODefine에 "ENT" prefix 등록).
	/// </summary>
	[CreateAssetMenu(fileName = nameof(EntityData), menuName = "WM/Variable/" + nameof(EntityData))]
	public class EntityData : DataSO
	{
		[field: Header("_" + nameof(EntityData))]
		[PropertyOrder(20)][field: SerializeField] public GameObject Prefab { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public EntityCategory Category { get; private set; } = EntityCategory.Default;
	}

	public enum EntityCategory
	{
		Default = 0,
		Tree = 1,
		Flower = 2,
		Rock = 3,
		Building = 4,
	}
}
