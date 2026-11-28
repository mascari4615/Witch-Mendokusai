using UnityEngine;

namespace WitchMendokusai
{
	// 욘의 집 건물 프리팹의 뿌리. 모델 (껍데기, 안쪽), 벽 충돌, 등은 프리팹에 구워 둠
	// 자리와 실내 카메라도 프리팹이 들고 HomeRoom 이 씀. 프리팹은 memo village-model 의 굽는 스크립트로 만듦
	public sealed class HomeHouse : MonoBehaviour
	{
		[Tooltip("바닥, 바깥 벽, 천장. 인물을 가려도 점무늬로 비우지 않음 (벽 충돌 상자가 카메라를 당김)")]
		[SerializeField] private GameObject shell;
		[Tooltip("놓은 가구의 원점. 방 크기 칸의 가운데")]
		[SerializeField] private Transform furnitureArea;
		[SerializeField] private Transform deskSpot;
		[SerializeField] private Transform restSpot;
		[SerializeField] private Transform benchSpot;
		[SerializeField] private Transform wardrobeSpot;
		[Tooltip("서쪽 부엌 조리대 앞. 요리 자리")]
		[SerializeField] private Transform cookSpot;
		[Tooltip("집 안에서 쓰는 카메라. 바깥보다 가깝게")]
		[SerializeField] private OrbitCameraProfile indoorView = new();

		public GameObject Shell => shell;
		public Transform FurnitureArea => furnitureArea;
		public Transform DeskSpot => deskSpot;
		public Transform RestSpot => restSpot;
		public Transform BenchSpot => benchSpot;
		public Transform WardrobeSpot => wardrobeSpot;
		public Transform CookSpot => cookSpot;
		public OrbitCameraProfile IndoorView => indoorView;
	}
}
