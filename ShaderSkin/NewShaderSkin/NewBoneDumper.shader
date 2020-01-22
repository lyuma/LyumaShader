Shader "LyumaShader/NewShaderSkin/NewBoneDumper"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" "Queue" = "Background-996"}
		LOD 100
		ZTest Always
		ZWrite Off
		Cull Off

		Pass
		{
			CGPROGRAM
			#pragma vertex newbonedump_vert
			#pragma fragment newbonedump_frag
			#define _BoneBindTexture _LyumaBoneTexture
			#define _BoneBindTexture_TexelSize _LyumaBoneTexture_TexelSize
			#include "NewShaderSkin.cginc"
			ENDCG
		}
		GrabPass {
			"_LyumaBoneTexture"
		}
	}
}
