Shader "LyumaShader/NewCamShaderSkin/PositionFromQuatTreeCam"
{
	Properties
	{
		[HDR] _MeshTex ("Mesh Data (Bind transforms)", 2D) = "white" {}
        [HDR] _BoneDataInputTexture ("current frame bone data", 2D) = "white" {}
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
			#pragma vertex pixel_process_vert
			#pragma fragment frag
			#define _BoneBindTexture _BoneDataInputTexture
			#define ONLY_ORTHO
			#define USE_SCREEN_PARAMS
			#include "NewShaderSkin.cginc"

			struct appdata {
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};
			struct v2f {
				float4 pos : SV_Position;
				float4 uv : TEXCOORD0;
			};

			v2f pixel_process_vert (appdata v) {
				v2f o = (v2f)0;
                o.pos = float4(v.uv * 2 - 1, 0, 1);
                #ifdef UNITY_UV_STARTS_AT_TOP
                v.uv.y = 1-v.uv.y;
                #endif
                o.uv.xy = UnityStereoTransformScreenSpaceTex(v.uv);
				o.uv.zw = o.uv.xy * _ScreenParams.xy;
				if (distance(_WorldSpaceCameraPos, mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz) > 1) {
					o.pos = float4(1,1,1,1);
				}
#ifdef ONLY_ORTHO
    if (unity_CameraProjection._44 == 0){
        o.pos = float4(1,1,1,1);
    }
#endif
				return o;
			}

			void updateFinalQP(int boneId, int yoffset, inout float4 finalQuat, inout float3 finalPos, inout float3 prevBindPos) {
				if (boneId != 7) {
					float4 thisQuat;
					float3 thisPos;
					bool fail = false;
					readBoneQP(boneId, yoffset, thisQuat, thisPos, fail);
					float3 bindPos = readBindPoseBonePosition(parents.w, YOFFSET_BONE_POSITION);
					float boneLength = distance(bindPos, prevBindPos); //// <<<<<<<< This needs to be baked!!!!!!!!!! maybe 4x4 grid of bone lengths
				}
			}

			float3 calculatePositionOfBone(uint boneId) {
				bool fail = false;
				float4 quat;
				float3 pos;
				readBoneQP(boneId, 0, quat, pos, fail);
				if (fail) {
					pos = float3(0,0,0);
				}
				float4 finalQuat = float4(0,0,0,1);
				float4 finalPos = float3(0,0,0);
				float4 thisQuat = float4(0,0,0,1);
				float4 thisPos = float3(0,0,0);
				float3 prevBindPos = float3(0,0,0);
				[unroll]
				for (int i = 3; i >= 0; i--) {
					int4 parents = readParents(i)
					updateFinalQP(parents.w, 0, finalQuat, finalPos, prevBindPos);
					if (parents.w != 7) {
						readBoneQP(parents.w, 0, thisQuat, thisPos, fail);
						readBindPoseBoneQP(parents.w, YOFFSET_BONE_POSITION);
					}
				}
				return pos;
			}

			float4 frag (v2f i) : SV_Target {
				uint2 pixel = uint2(i.uv.zw);
				if (pixel.y == 0) {
					// blend shape
					return _BoneBindTexture.Load(int3(pixel, 0));
				} else if (pixel.x == 0) {
					// position
					return _BoneBindTexture.Load(int3(pixel, 0));
				} else if (pixel.y == 1) {
					// quaternion
					return _BoneBindTexture.Load(int3(pixel, 0));
				} else if (pixel.y == 2 || pixel.y == 3) {
					// position
					float3 vertex = calculatePositionOfBone(pixel.x - 1);
					uint yRemainder5Bits = 0;
					float2 encodedFloatY = encodeLossyFloat(vertex.y, yRemainder5Bits).xy;
					float3 encodedFloatXybits = encodeLossyFloat(vertex.x, yRemainder5Bits);
					yRemainder5Bits = 0;
					float3 encodedFloatZ = encodeLossyFloat(vertex.z, yRemainder5Bits);
					if (pixel.y == 2) {
			        	return float4(encodedFloatXybits.xy, encodedFloatY.x, encodedFloatXybits.z);
					} else {
				        return float4(encodedFloatZ.xy, encodedFloatY.y, encodedFloatZ.z);
					}
				} else {
					clip(-1);
					return float4(0,0,0,0);
				}
			}
			ENDCG
		}
	}
}
