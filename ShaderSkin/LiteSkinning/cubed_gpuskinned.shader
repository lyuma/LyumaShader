Shader "LyumaShader/cubed_gpuskinned" {
    // gpuskinned by Lyuma
    // Based on an old version of CubedParadox's "Flat Lit Toon.shader"
    Properties {
        _MainTex ("MainTex", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _ColorMask ("ColorMask", 2D) = "black" {}
        _Shadow ("Shadow", Range(0, 1)) = 0.4
        _EmissionMap ("Emission Map", 2D) = "white" {}
        _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _BumpMap ("BumpMap", 2D) = "bump" {}
        _DebugWeights ("Debug Weights", Range(0, 1)) = 0
        [HideInInspector]_Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
    }
	SubShader {
        Tags {
            "Queue"="Geometry"
            "RenderType"="TransparentCutout"
        }
        Cull Back
      
        Pass {
            Name "FORWARD"
            Tags {
                "LightMode"="ForwardBase"
            }
            
            
            CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom
			//#define UNITY_PASS_FORWARDADD
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
			
			#pragma multi_compile_fwdbase_fullshadows
			#pragma multi_compile_fog
			#pragma only_renderers d3d9 d3d11 glcore gles 
			#pragma target 4.0
			#pragma shader_feature DONT_OUTLINE
			#pragma shader_feature VR_ONLY_2D

			#include "cubed_gpuskinned.cginc"
            ENDCG
        }
		
		Pass{
			Name "FORWARD_DELTA"
			Tags{
				"LightMode" = "ForwardAdd"
			}
			Blend One One


			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom
			//#define UNITY_PASS_FORWARDADD
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
			
			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog
			#pragma only_renderers d3d9 d3d11 glcore gles 
			#pragma target 4.0
			#pragma shader_feature DONT_OUTLINE
			#pragma shader_feature VR_ONLY_2D
			#define CUBED_FORWARDADD_PASS

			#include "cubed_gpuskinned.cginc"
			ENDCG
		}

	// Pass to render object as a shadow caster
	Pass {
		Name "ShadowCaster"
		Tags { "LightMode" = "ShadowCaster" }
		
CGPROGRAM
#pragma vertex vert
#pragma geometry geom
#pragma fragment frag
#pragma target 2.0
#pragma multi_compile_shadowcaster
#pragma multi_compile_instancing // allow instanced shadow pass for most of the shaders
#pragma shader_feature VR_ONLY_2D
#include "UnityCG.cginc"
#include "AutoLight.cginc"

struct v2f { 
	V2F_SHADOW_CASTER;
	UNITY_VERTEX_OUTPUT_STEREO
};

#define GPUSKINNED_HELPERS_ONLY
#include "cubed_gpuskinned.cginc"

[maxvertexcount(3)]
void geom(triangle v2g vertin[3], inout TriangleStream<v2f> tristream )
{
    SkinnedVertexInput IN[3];
    applyBones(vertin, IN);

    for (int ii = 0; ii < 3; ii++) {
    	v2f o = (v2f)0;
        SkinnedVertexInput v = IN[ii];
    	UNITY_SETUP_INSTANCE_ID(v);
    	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    	float4 actualObjectPos2 = v.vertex;
#ifdef SHADOWS_CUBE
   	o.vec = mul(unity_ObjectToWorld, actualObjectPos2).xyz - _LightPositionRange.xyz;
   	o.pos = UnityObjectToClipPos(actualObjectPos2);
#else
       o.pos = UnityClipSpaceShadowCasterPos(actualObjectPos2, v.normal); \
       o.pos = UnityApplyLinearShadowBias(o.pos);
#endif
        tristream.Append(o);
    }

    tristream.RestartStrip();
}

float4 frag( v2f i ) : SV_Target
{
	SHADOW_CASTER_FRAGMENT(i)
}
ENDCG

	}
    }

    //FallBack "Diffuse"
    //CustomEditor "waifu2dInspector"
}
