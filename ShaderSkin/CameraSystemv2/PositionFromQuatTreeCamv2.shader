Shader "LyumaShader/NewCamShaderSkin/PositionFromQuatTreeCamv2"
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

			static float leftFinger = 0;
			static float rightFinger = 0;

			float quatCos(float4 q1, float4 q2) {
				return (dot(q1, q2) / (length(q1) * length(q2)));
			}

			float quatAngle(float4 q1, float4 q2) {
				return acos(quatCos(q1, q2));
			}

			void preprocessKernel(inout uint boneId, uint pixeltype, inout uint yoffset) {
				float4 leftFingerParent = readBoneQuat(LEFT + _FINGER_LITTLE_ + PROXIMAL_BONE, yoffset);
				float4 leftFingerQuat = readBoneQuat(LEFT + _WRIST_BONE, yoffset);
				float4 rightFingerParent = readBoneQuat(RIGHT + _FINGER_LITTLE_ + PROXIMAL_BONE, yoffset);
				float4 rightFingerQuat = readBoneQuat(RIGHT + _WRIST_BONE, yoffset);
				leftFinger = quatCos(leftFingerParent, leftFingerQuat);
				rightFinger = quatCos(rightFingerParent, rightFingerQuat);
			}

			void preBoneKernel(inout uint boneId, uint2 mainBonePixelType, inout float4 globalParentRot, inout float3 globalParentPos) {
				if (mainBonePixelType.y < 4) {
					if (boneId >= _FINGER_LITTLE_ && boneId < _FINGER_LITTLE_ + 6) {
						boneId -= _FINGER_LITTLE_ - _FINGER_RING_;
					}
				}
			}

			void boneKernel(uint boneId, uint2 mainBonePixelType, inout float4 globalParentRot, inout float3 globalParentPos, inout float4 localRot, inout float3 localPos) {
				if (boneId >= 0 && boneId < 50 && leftFinger < 0.85 && leftFinger > 0.55) {
					localRot = qmul(localRot, rotate_angle_axis(.5*sin(_Time.g * 2 + 0.55), float3(0,1,0)));
					if (boneId != 0 && (boneId < 12 || boneId >= 16)) {
						localRot = qmul(localRot, rotate_angle_axis(.1*sin(_Time.g * 2 + 0.67 + sin(_Time.g * 1.38 + 18)), float3(0,0,1)));
					}
				}
				if (rightFinger < 0.85 && rightFinger > 0.55) {
					float angle = fmod(_Time.g * 10, 6.2831853);
					if (boneId == RIGHT + _ARM_BONE) {
						localRot = qmul(rotate_angle_axis(1, float3(0,1,0)), qmul(rotate_angle_axis(0, float3(0,0,1)), rotate_angle_axis(1 -angle, float3(1,0,0))));
					} else if (boneId == RIGHT + _SHOULDER_BONE) {
						localRot = qmul(rotate_angle_axis(-2, float3(0,0,1)), rotate_angle_axis(angle, float3(1,0,0)));
					}
				}
				/*
				if (mainBonePixelType == 4) {
					if (boneId == LEFT + _FINGER_LITTLE_ + PROXIMAL) {
					}
					if (boneId == RIGHT + _FINGER_LITTLE_ + PROXIMAL) {
					}
				}
				*/
			}

			void updateFinalQP(uint boneId, uint2 mainBonePixelType, uint yoffset, inout float4 lastReadQuat, inout float4 finalQuat, inout float4 finalPosScale) {
				// 7 is special root bone placeholder
				if (boneId != 7) {
					uint remapBoneId = boneId;
					preBoneKernel(remapBoneId, mainBonePixelType, finalQuat, finalPosScale.xyz);
					float4 readQuat = readBoneQuat(remapBoneId, yoffset);
					float4 localMeshPosScale = readMeshBoneMapFloat4(boneId, YOFFSET_LOCALPOSSCALE);
					float4 localQuat = qmul(q_inverse(lastReadQuat), readQuat);
					boneKernel(boneId, mainBonePixelType, finalQuat, finalPosScale.xyz, localQuat, localMeshPosScale.xyz);
					finalPosScale.xyz += quat_apply_vec(q_inverse(finalQuat), localMeshPosScale.xyz) * finalPosScale.w;
					finalPosScale.w *= localMeshPosScale.w;
					finalQuat = qmul(finalQuat, localQuat);
					lastReadQuat = readQuat;
				}
			}

			void calculateLocalPositionOfBone(uint boneId, uint pixelType, uint yoffset, out float4 finalQuat, out float4 finalPosScale, out float4 localQuat, out float4 localMeshPosScale) {
				uint2 mainBonePixelType = uint2(boneId, pixelType);
				localMeshPosScale = readMeshBoneMapFloat4(boneId, YOFFSET_LOCALPOSSCALE);
				finalQuat = float4(0,0,0,1);
				finalPosScale = float4(0,0,0,1);
				float4 lastReadQuat = float4(0,0,0,1);
				for (uint i = 0; i < 4; i++) {
					float4 parents = readMeshBoneMapFloat4(boneId, YOFFSET_PARENTS0 + 3 - (uint)(i));
					for (uint j = 0; j < 4; j ++) {
						updateFinalQP((uint)(parents[3 - j]), mainBonePixelType, yoffset, lastReadQuat, finalQuat, finalPosScale);
					}
				}
				uint remapBoneId = boneId;
				preBoneKernel(remapBoneId, mainBonePixelType, finalQuat, finalPosScale.xyz);
				localQuat = qmul(q_inverse(lastReadQuat), readBoneQuat(remapBoneId, yoffset));
				boneKernel(boneId, mainBonePixelType, finalQuat, finalPosScale.xyz, localQuat, localMeshPosScale.xyz);
				finalPosScale.xyz += quat_apply_vec(q_inverse(finalQuat), localMeshPosScale.xyz) * finalPosScale.w;
				finalQuat = qmul(finalQuat, localQuat);
			}

			float3 calculatePositionOfBone(uint boneId, uint pixelType, uint yoffset, out float4 outQuat) {
				float4 finalQuat, finalPosScale, localQuat, localMeshPosScale;
				calculateLocalPositionOfBone(boneId, pixelType, yoffset, finalQuat, finalPosScale, localQuat, localMeshPosScale);
				outQuat = finalQuat;
				return finalPosScale.xyz;
			}


			float4 frag (v2f i) : SV_Target {
				uint2 pixel = uint2(i.uv.zw);
				uint boneId = pixel.x - 1;
				uint yoffset = 0; // FIXME

				preprocessKernel(boneId, pixel.y, yoffset);

				if (pixel.y == 0) {
					// blend shape
					return _BoneBindTexture.Load(int3(pixel, yoffset));
				} else if (pixel.x == 0) {
					// position
					return _BoneBindTexture.Load(int3(pixel, yoffset));
				} else if (pixel.y == 1) {
					// quaternion
					float4 outQuat;
					float3 vertex = calculatePositionOfBone(boneId, pixel.y, yoffset, outQuat);
					return outQuat * 0.5 + 0.5;
				} else if (pixel.y == 2 || pixel.y == 3) {
					// position
					float4 outQuat;
					float3 vertex = calculatePositionOfBone(boneId, pixel.y, yoffset, outQuat);
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
				} else if (pixel.y == 4) {
					// local quaternion
					float4 finalQuat, finalPosScale, localQuat, localMeshPosScale;
					calculateLocalPositionOfBone(boneId, pixel.y, yoffset, finalQuat, finalPosScale, localQuat, localMeshPosScale);
					return localQuat * 0.5 + 0.5;
				} else if (pixel.y == 5) {
					return float4(0,0,0,0);
				} else {
					clip(-1);
					return float4(0,0,0,0);
				}
			}
			ENDCG
		}
	}
}
