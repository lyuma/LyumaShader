Shader "LyumaShader/NewCamShaderSkin/PositionFromQuatTreeCamv2Bouncy"
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

/*
			float3 calculatePositionOfBone(uint boneId) {
				bool fail = false;
				float4 quat;
				float3 pos;
				readBoneQP(boneId, 0, quat, pos, fail);
				if (fail) {
					pos = float3(0,0,0);
				}
				[unroll]
				for (int i = 3; i >= 0; i--) {
					int4 parents = readBoneMapParents(boneId, YOFFSET_PARENTS0 + i);
					if (parents.w != ROOT_BONE_INDEX) {
						// TODO: Switch to quats!!!
					}
				}
				return pos;
			}
*/

			void boneKernel(uint boneId, float4 globalParentRot, float3 globalParentPos, inout float4 localRot, inout float3 localPos) {
				if (boneId < 8) {
					//localRot = qmul(localRot, rotate_angle_axis(10, float3(0,1,0)));
				}
			}

			void updateFinalQP(uint boneId, uint yoffset, inout float4 lastMeshQuat, inout float4 finalQuat, inout float4 finalPosScale) {
				if (boneId != 7) {
					//float4 thisQuat;
					//float3 thisPos;
					//bool fail = false;
					//readBoneQP(boneId, yoffset, thisQuat, thisPos, fail);
					float4 readQuat = readBoneQuat(boneId, yoffset);
					float4 localMeshQuat = readMeshBoneMapFloat4(boneId, YOFFSET_LOCALQUAT);
					float4 localMeshPosScale = readMeshBoneMapFloat4(boneId, YOFFSET_LOCALPOSSCALE);
					float4 globalMeshQuat = readMeshBoneMapFloat4(boneId, YOFFSET_GLOBALQUAT);
					float4 thisQuat = qmul(q_inverse(finalQuat), readQuat);//globalMeshQuat);
					float4 globalMeshPosScale = readMeshBoneMapFloat4(boneId, YOFFSET_GLOBALPOSSCALE);
					boneKernel(boneId, finalQuat, finalPosScale.xyz, thisQuat, localMeshPosScale.xyz);
					// THE BUG IS: localMeshPosScale.xyz is in the frame of reference of the original mesh pose.
					finalPosScale.xyz += quat_apply_vec(q_inverse(finalQuat), localMeshPosScale.xyz) * finalPosScale.w;
					finalPosScale.w *= localMeshPosScale.w;
					//finalPosScale.xyz = globalMeshPosScale.xyz;
					//finalQuat = globalMeshQuat;//qmul(finalQuat, localMeshQuat); // T-Pose!
					//finalQuat = thisQuat;//normalize(qmul(thisQuat,rotate_angle_axis(0,float3(0,1,0)))); //qmul(finalQuat, thisQuat); // Use actual rotations.
					/////lastMeshQuat = localMeshQuat;
					finalQuat = qmul(finalQuat, thisQuat);//localMeshQuat);
					if (boneId > 5 && boneId < 50 || boneId < 5) {
						finalQuat = qmul(rotate_angle_axis(2 * sin(_Time.g*0.1+0.4) * sin(_Time.g*8+0.118),float3(0,1,0)), finalQuat);//float4(0,0,0,1);
					}
				}
			}

			float3 calculatePositionOfBone(uint boneId, uint yoffset, out float4 outQuat) {
				bool fail = false;
				float4 thisQuat;
				float3 thisPos;
				readBoneQP(boneId, yoffset, thisQuat, thisPos, fail);	

				//float directParent = readMeshBoneMapFloat4(boneId, YOFFSET_PARENTS0).x;
				//float4 parentQuat = readBoneQuat(directParent, yoffset);
				//float4 globalQuat = readBoneQuat(boneId, yoffset);
				float4 localMeshPosScale = readMeshBoneMapFloat4(boneId, YOFFSET_LOCALPOSSCALE);
				float4 globalMeshPosScale = readMeshBoneMapFloat4(boneId, YOFFSET_GLOBALPOSSCALE);
				float4 rootglobalMeshPosScale = readMeshBoneMapFloat4(0, YOFFSET_GLOBALPOSSCALE);
				float4 rootQuat = readMeshBoneMapFloat4(0, YOFFSET_LOCALQUAT);
				float4 finalQuat = float4(0,0,0,1);
				float4 finalPosScale = float4(0,0,0,1);
				float4 lastMeshQuat = float4(0,0,0,1);
				// cycle through properties of a float4
				finalPosScale.w = (0.8 + (0.3 * sin(_Time.g*0.2) * sin(_Time.g*10)));
				float4 mods = float4(3,2,1,0);
				for (uint i = 0; i < 4; i++) {
					float4 parents = readMeshBoneMapFloat4(boneId, YOFFSET_PARENTS0 + 3 - (uint)(i));
					for (uint j = 0; j < 4; j ++) {
						//updateFinalQP(dot(parents, mods == j), yoffset, lastMeshQuat, finalQuat, finalPosScale);
						updateFinalQP((uint)(parents[3 - j]), yoffset, lastMeshQuat, finalQuat, finalPosScale);
					}
				}
				finalPosScale.xyz += quat_apply_vec(q_inverse(finalQuat), localMeshPosScale.xyz) * finalPosScale.w;
				//finalPosScale.xyz = globalMeshPosScale.xyz;
				outQuat = finalQuat;
				return finalPosScale.xyz;//float3(finalPosScale.x, -finalPosScale.z, finalPosScale.y).xzy;// - rootglobalMeshPosScale.xyz;
			}


			float4 frag (v2f i) : SV_Target {
				uint2 pixel = uint2(i.uv.zw);
				uint boneId = pixel.x - 1;
				if (pixel.y == 0) {
					// blend shape
					return _BoneBindTexture.Load(int3(pixel, 0));
				} else if (pixel.x == 0) {
					// position
					return _BoneBindTexture.Load(int3(pixel, 0));
				} else if (pixel.y == 1) {
					// quaternion
					float4 thisQuat = readBoneQuat(boneId, 0);
					float4 globalMeshQuat = readMeshBoneMapFloat4(boneId, YOFFSET_GLOBALQUAT);
					//return globalMeshQuat * 0.5 + 0.5;
					return thisQuat * 0.5 + 0.5;
					float4 outQuat;
					//float3 vertex = calculatePositionOfBone(boneId, 0, outQuat);
					//return outQuat * 0.5 + 0.5;
				} else if (pixel.y == 2 || pixel.y == 3) {
					// position
					float4 outQuat;
					float3 vertex = calculatePositionOfBone(boneId, 0, outQuat);
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
				} else if (pixel.y == 5 && pixel.x <16) {
					float4 parents = readMeshBoneMapFloat4(3, 16 + 3 - (uint)(pixel.x / 4));
					//return float4(parents[pixel.x%4].xxx == 0, 1.0);
					float4 mods = float4(3,2,1,0);
					return float4(7 == dot(parents, mods == (pixel.x % 4)),0,0,1);
				} else if (pixel.y == 5 && pixel.x >= 16) {
					return float4(0,0,0,1);
				} else {
					clip(-1);
					return float4(0,0,0,0);
				}
			}
			ENDCG
		}
	}
}
