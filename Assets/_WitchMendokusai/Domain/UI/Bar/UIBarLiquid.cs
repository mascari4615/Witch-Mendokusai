using UnityEngine;
using UnityEngine.UI;

namespace WitchMendokusai
{
	/// <summary>
	/// 체력 바 채움을 액체처럼 출렁이게 함 (사용자 2026-11-18 "체력바 찰랑찰랑 효과")
	/// - 채움 비율은 같은 Image 의 fillAmount 를 그대로 읽음. UIBarStat 의 보간이 그 값을 움직임
	/// - Image 는 Simple 로 바꾸고 채움 경계는 셰이더 (WM/UI/LiquidBar) 가 그림. 그래야 경계가 물결칠 자리가 생김
	/// - 비율이 바뀌면 용수철 하나에 충격을 주고, 그 흔들림을 꼭짓점 자료로 셰이더에 넘김
	/// - 잔잔할 때는 셰이더의 시간 값만으로 물결이 돌아서 꼭짓점 재작성 없음
	/// 왼쪽에서 오른쪽으로 차는 가로 바 전용
	/// </summary>
	[RequireComponent(typeof(Image))]
	public class UIBarLiquid : BaseMeshEffect
	{
		[Tooltip("액체 셰이더 (WM/UI/LiquidBar) 재질. 색과 윗면 띠 두께는 재질에서")]
		[SerializeField] private Material liquidMaterial;

		[Header("Wave")]
		[Tooltip("평소 물결 높이 (픽셀)")]
		[SerializeField, Min(0f)] private float idleWaveHeight = 1f;
		[Tooltip("출렁일 때 물결이 더 커지는 정도. 출렁임 1픽셀당 물결 높이 추가 (픽셀)")]
		[SerializeField, Min(0f)] private float sloshWaveGain = 0.3f;
		[Tooltip("물결 흐름 빠르기 (초당 바퀴)")]
		[SerializeField] private float waveSpeed = 0.5f;
		[Tooltip("채움 끝 세로 경계의 물결 개수 (바 높이 한 번당)")]
		[SerializeField, Min(0f)] private float edgeWaveCount = 1.2f;
		[Tooltip("윗면 띠의 물결 개수 (바 길이 한 번당)")]
		[SerializeField, Min(0f)] private float surfaceWaveCount = 5f;

		[Header("Slosh")]
		[Tooltip("체력 변화로 생기는 출렁임 세기. 바 전체만큼 바뀔 때의 흔들림 속도 (픽셀/초)")]
		[SerializeField, Min(0f)] private float sloshImpulse = 400f;
		[Tooltip("출렁임이 제자리로 돌아오는 힘. 클수록 빨리 흔들림")]
		[SerializeField, Min(0.01f)] private float sloshStiffness = 120f;
		[Tooltip("출렁임 감쇠. 클수록 빨리 잔잔해짐")]
		[SerializeField, Min(0f)] private float sloshDamping = 5f;
		[Tooltip("출렁임 최대 폭 (픽셀)")]
		[SerializeField, Min(0f)] private float sloshMax = 10f;
		[Tooltip("출렁임과 그 속도가 이보다 작으면 잔잔해진 것으로 보고 계산 멈춤 (픽셀)")]
		[SerializeField, Min(0f)] private float sloshRestThreshold = 0.02f;
		[Tooltip("한 프레임에 계산할 최대 시간 (초). 프레임이 크게 끊겨도 용수철이 튀지 않게")]
		[SerializeField, Min(0.001f)] private float maxStepSeconds = 0.05f;

		private Image bar;
		private float displayedFill;
		private float slosh;
		private float sloshVelocity;
		private float writtenSlosh;

		// OnEnable 에서 바꾼 Image 값. OnDisable 에서 복원. 재질 null 은 기본 재질
		private bool isApplied;
		private Material originalMaterial;
		private Image.Type originalType;

		private Image Bar
		{
			get
			{
				if (bar == null)
					bar = GetComponent<Image>();
				return bar;
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			if (Application.isPlaying == false)
				return;

			if (isApplied == false)
			{
				isApplied = true;
				originalType = Bar.type;
				Material currentMaterial = Bar.material;
				bool isDefault = currentMaterial == Bar.defaultMaterial || currentMaterial == Image.defaultETC1GraphicMaterial;
				originalMaterial = isDefault ? null : currentMaterial;
			}
			Bar.type = Image.Type.Simple;
			Bar.material = liquidMaterial;
			EnableCanvasChannels();

			displayedFill = Bar.fillAmount;
			slosh = 0f;
			sloshVelocity = 0f;
			graphic.SetVerticesDirty();
		}

		protected override void OnDisable()
		{
			if (isApplied)
			{
				isApplied = false;
				Bar.type = originalType;
				Bar.material = originalMaterial;
			}
			base.OnDisable();
		}

		// 꼭짓점의 TEXCOORD1, 2 는 캔버스가 켜 줘야 셰이더까지 감. 중첩 캔버스면 맨 위 캔버스도
		private void EnableCanvasChannels()
		{
			Canvas canvas = graphic.canvas;
			if (canvas == null)
				return;

			const AdditionalCanvasShaderChannels LIQUID_CHANNELS = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
			canvas.additionalShaderChannels |= LIQUID_CHANNELS;
			if (canvas.rootCanvas != null)
				canvas.rootCanvas.additionalShaderChannels |= LIQUID_CHANNELS;
		}

		// UIBarStat 의 보간 코루틴이 Update 뒤에 fillAmount 를 바꿔서 LateUpdate 에서 읽음
		private void LateUpdate()
		{
			float deltaTime = Mathf.Min(Time.unscaledDeltaTime, maxStepSeconds);

			float fill = Bar.fillAmount;
			float fillDelta = fill - displayedFill;
			displayedFill = fill;
			sloshVelocity += fillDelta * sloshImpulse;

			float acceleration = -sloshStiffness * slosh - sloshDamping * sloshVelocity;
			sloshVelocity += acceleration * deltaTime;
			slosh = Mathf.Clamp(slosh + sloshVelocity * deltaTime, -sloshMax, sloshMax);

			bool isResting = Mathf.Abs(slosh) < sloshRestThreshold && Mathf.Abs(sloshVelocity) < sloshRestThreshold;
			if (isResting)
			{
				slosh = 0f;
				sloshVelocity = 0f;
			}

			// fillAmount 변화는 Image 가 스스로 다시 그림. 여기서는 출렁임 값이 바뀔 때만
			if (slosh != writtenSlosh)
				graphic.SetVerticesDirty();
		}

		public override void ModifyMesh(VertexHelper vertexHelper)
		{
			// 조기 return 경로에서도 맞춤. 안 맞으면 LateUpdate 가 매 프레임 더티 표시
			writtenSlosh = slosh;
			if (IsActive() == false)
				return;

			Rect rect = graphic.rectTransform.rect;
			if (rect.width <= 0f || rect.height <= 0f)
				return;

			float fill = Mathf.Clamp01(Bar.fillAmount);
			float waveHeight = idleWaveHeight + Mathf.Abs(slosh) * sloshWaveGain;
			Vector4 waveData = new(waveHeight, edgeWaveCount, waveSpeed, surfaceWaveCount);

			UIVertex vertex = new();
			for (int i = 0; i < vertexHelper.currentVertCount; i++)
			{
				vertexHelper.PopulateUIVertex(ref vertex, i);
				float normalizedX = (vertex.position.x - rect.xMin) / rect.width;
				float normalizedY = (vertex.position.y - rect.yMin) / rect.height;
				vertex.uv1 = new Vector4(normalizedX, normalizedY, fill, slosh);
				vertex.uv2 = waveData;
				vertexHelper.SetUIVertex(vertex, i);
			}
		}
	}
}
