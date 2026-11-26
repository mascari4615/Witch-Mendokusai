// WM/Fx/Painted 와 같은 패스. 침식 가장자리 색만 파랑 전기
Shader "WM/Fx/PaintedElectric"
{
	Properties
	{
		_MainTex ("Painted (premultiplied)", 2D) = "black" {}
		_NoiseTex ("Noise (R soft, G wisp)", 2D) = "gray" {}
		_NoiseScale ("Erosion noise scale", Float) = 1.3
		[HDR] _EdgeColor ("Erosion edge", Color) = (0.5, 1.5, 2.6, 1)
		_EdgeWidth ("Erosion edge width", Range(0.01, 0.5)) = 0.14
		_DistortScale ("Distort noise scale", Float) = 0.8
		_FlowSpeed ("Flow speed", Float) = 0.9
		_ParticleCover ("Cover for particles", Range(0, 1)) = 0
		_Intensity ("Intensity", Float) = 1
		[Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
		UsePass "WM/Fx/Painted/PAINTED"
	}
}
