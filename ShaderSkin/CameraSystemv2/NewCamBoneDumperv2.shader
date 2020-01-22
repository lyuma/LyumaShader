Shader "LyumaShader/NewCamShaderSkin/NewCamBoneDumperv2"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue" = "Overlay+1000"}
		LOD 100
		ZTest Always
		ZWrite On
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex newbonedump_vert
			#pragma fragment newbonedump_frag
			//static float4 _BoneBindTexture_TexelSize = float4(0,0,_ScreenParams.xy);
			#define ONLY_ORTHO
			#define USE_SCREEN_PARAMS
			#include "NewShaderSkin.cginc"
			ENDCG
		}
	}
}
