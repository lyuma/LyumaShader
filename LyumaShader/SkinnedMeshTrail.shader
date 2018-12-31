Shader "LyumaShader/SkinnedMeshTrail" 
{
	Properties
	{
        // Shader properties
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB)", 2D) = "white" {}
        [HDR] _MeshTex ("Mesh Data", 2D) = "white" {}
        [HDR] _BlendShapeTexArray ("Blend Shape Data", 2DArray) = "black" {}
        // [HDR] _BoneBindTransforms ("Bone Bind Transform Data", 2D) = "black" {}
        _NumBlendShapes ("Number of blend shapes", Float) = 0
        _FrameOffset ("Age of frame (0 = current)", Float) = 0
        _FrameOffsetLerp ("Lerp to old frame", Float) = 0
        _TrailOffset ("Where trail starts", Float) = 0
        _TrailLength ("How many copies are trailed (up to 6)", Float) = 0
        _BoneCutoff ("Up to 2 bone ranges to render", Vector) = (0,0,0,0)
        _TrailColorPow ("Trail color decay (x=const, y=mul)", Vector) = (0,0,0,0)
    }
	SubShader
	{
    Tags {
            "Queue"="Transparent+11"
            "RenderType"="Transparent"
            "IgnoreProjector"="true"
    }
        // Shader code
		Pass
        {
            Cull Back
            ZTest Less
            ZWrite Off
            Blend SrcColor OneMinusSrcColor // SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #include "HLSLSupport.cginc"
            #include "UnityCG.cginc"
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 4.6
            uniform float4 _Color;
            sampler2D _MainTex;
            Texture2D<half4> _MeshTex;
            //SamplerState sampler_MeshTex;
            uniform float4 _MeshTex_TexelSize;
            Texture2DArray<half4> _BlendShapeTexArray;
            uniform float4 _BlendShapeTexArray_TexelSize;
            Texture2D<half4> _BoneBindTexture;
            uniform float4 _BoneBindTexture_TexelSize;
            uniform float _NumBlendShapes;
            uniform float _FrameOffset;
            uniform float _FrameOffsetLerp;
            uniform float _TrailOffset;
            uniform float _TrailLength;
            uniform float4 _BoneCutoff;
            uniform float4 _TrailColorPow;

            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2g {
                float4 bindPose_col0 : TEXCOORD3;
                float4 bindPose_col1 : TEXCOORD4;
                float4 bindPose_col2 : TEXCOORD5;
                float4 bindPose_col3 : TEXCOORD6;
            };
            struct g2f {
                float4 pos : SV_POSITION;
                //float4 normal : TEXCOORD1;
                //float4 tangent : TEXCOORD2;
                float4 uv : TEXCOORD3;
            };
            float4x4 CreateMatrixFromCols(float4 c0, float4 c1, float4 c2, float4 c3) {
                return float4x4(c0.x, c1.x, c2.x, c3.x,
                                c0.y, c1.y, c2.y, c3.y,
                                c0.z, c1.z, c2.z, c3.z,
                                c0.w, c1.w, c2.w, c3.w);
            }
            float4x4 CreateMatrixFromVert(v2g v) {
                return CreateMatrixFromCols(v.bindPose_col0, v.bindPose_col1, v.bindPose_col2, v.bindPose_col3);
            }

            v2g vert (VertexInput v) {
                v2g o = (v2g)0;
                float scale = length(v.normal.xyz);
                o.bindPose_col2 = float4(scale * normalize(v.normal.xyz), 0);//scale * float4(1,0,0,0); //float4(scale * normalize(v.normal.xyz), 0);
                o.bindPose_col0 = float4(scale * normalize(v.tangent.xyz), 0);//scale * float4(0,1,0,0); //float4(scale * normalize(v.tangent.xyz), 0);
                o.bindPose_col1 = float4(scale * cross(normalize(v.normal.xyz), normalize(v.tangent.xyz)), 0); // / scale//scale * float4(0,0,1,0); //
                o.bindPose_col3 = float4(v.vertex.xyz, 1);
                return o;
            }

            struct InputBufferElem {
                float4 vertex;
                float4 normal;
                float4 tangent;
                float4 uvColor;
                float4 boneIndices;
                float4 boneWeights;
            };
            #define numInstancePerVert 32

            half4 readHalf4(uint2 pixelIndex) {
                //return _MeshTex.Load(int3(pixelIndex.x, _MeshTex_TexelSize.w - pixelIndex.y - 1, 0));
                return _MeshTex.Load(int3(pixelIndex, 0));
            }

            half3 readBlendShapeHalf3(uint2 pixelIndex, int blendIdx) {
                return _BlendShapeTexArray.Load(int4(pixelIndex, blendIdx, 0)).xyz;
            }

            int3 genStereoTexCoord(int x, int y) {
            	float2 xyCoord = float2(x, y);
            	return int3(xyCoord, 0);
            }

            half4x4 readBindTransform(int boneIndex, int yOffset) {
                boneIndex = boneIndex + 1;
                //int adj = sin(_Time.y) < 0.5 ? 1 : -1;//float adj = .5;
                int adj = 0;//float adj = -0.5; // sin(_Time.y) < 0.5 ? 0.5 : -0.5;//1. + lerp(2 * sin(.3*_Time.y), 1., sin(_Time.x) < 0);
                half4x4 ret = 10*CreateMatrixFromCols(
                // bone poses will be written in swizzled format, such that
                // an opaque black texture will give the identity matrix.
                    _BoneBindTexture.Load(int3(genStereoTexCoord(boneIndex-adj, yOffset * 6 + 0))).wxyz,
                    _BoneBindTexture.Load(int3(genStereoTexCoord(boneIndex-adj, yOffset * 6 + 1))).zwxy,
                    _BoneBindTexture.Load(int3(genStereoTexCoord(boneIndex-adj, yOffset * 6 + 2))).yzwx,
                    _BoneBindTexture.Load(int3(genStereoTexCoord(boneIndex-adj, yOffset * 6 + 3))).xyzw
                );
                ret._11_22_33 = 10*_BoneBindTexture.Load(int3(genStereoTexCoord(boneIndex-adj, yOffset * 6 + 5))).xyz;
                ret._44 = 1.0;
                return ret;
            }
            half readBlendShapeState(int blendIndex) {
                float adj = sin(10 * _Time.x);
                return _BoneBindTexture.Load(int3(blendIndex-adj, 4, 0)).x;
            }
            InputBufferElem readMeshData(uint instanceID, uint primitiveID, uint vertexNum) {
                uint pixelNum = 3 * (instanceID + primitiveID * numInstancePerVert) + vertexNum;
                uint2 elemIndex = uint2((pixelNum % 64), pixelNum / 64);
                uint2 pixelIndex = uint2(6 * elemIndex.x, elemIndex.y);
                InputBufferElem o;
                o.vertex = readHalf4(pixelIndex + uint2(0,0));
                o.normal = readHalf4(pixelIndex + uint2(1,0));
                o.tangent = readHalf4(pixelIndex + uint2(2,0));
                o.uvColor = readHalf4(pixelIndex + uint2(3,0));
                o.boneIndices = readHalf4(pixelIndex + uint2(4,0));
                o.boneWeights = readHalf4(pixelIndex + uint2(5,0));
                /*
                for (uint blendIdx = 0; blendIdx < 255 && blendIdx < (uint)_NumBlendShapes; blendIdx++) {
                    half blendValue = readBlendShapeState(blendIdx);
                    pixelIndex = uint2(elemIndex.x, elemIndex.y);// * 3
                    //if (blendValue > 0.0) {
                        o.vertex.xyz += .001 * ((float3)readBlendShapeHalf3(pixelIndex + uint2(0,0), blendIdx)!=0.);
                        //o.normal.xyz += ((float3)readBlendShapeHalf3(pixelIndex + uint2(1,0), blendIdx)!=0.);
                        //o.tangent.xyz += ((float3)readBlendShapeHalf3(pixelIndex + uint2(2,0), blendIdx)!=0.);
                    //}
                }
                //o.uvColor = float4(.1*readHalf4(pixelIndex + uint2(3,0)).w, 0.1*o.normal.w, o.tangent.w, 1);////float4(0.5,.5,.5,.5) + .001*readHalf4(pixelIndex + uint2(3,0)); // float4(instanceID/2. + 0.3, pixelNum / 1000.,0.,1.); //
				*/
                return o;
            }

            [instance(numInstancePerVert)]//3)]
            [maxvertexcount(3)]//24)]
            void geom(triangle v2g vertin[3], inout TriangleStream<g2f> tristream,
                    uint primitiveID: SV_PrimitiveID, uint instanceID : SV_GSInstanceID)
            {
                /*float4x4 transformMatrices[3];
                transformMatrices[0] = CreateMatrixFromVert(vertin[0]);
                transformMatrices[1] = CreateMatrixFromVert(vertin[1]);
                transformMatrices[2] = CreateMatrixFromVert(vertin[2]);

                float4x4 thisTransform;
                if (instanceID == 2) {
                    thisTransform = transformMatrices[2];
                } else if (instanceID == 1) {
                    thisTransform = transformMatrices[1];
                } else {
                    thisTransform = transformMatrices[0];
                }

                g2f o;
                //float4 col = float4(saturate(0.7 - abs(transformMatrices[0]._24) * 0.6),
                //        0.7 * abs(mul((float3x3)transformMatrices[1], float3(0,0,1)).z),
                //        saturate(0.7 - abs(transformMatrices[2]._24) * 0.4), 1.);
                float4 col = saturate(0.3 * abs(float4(
                    mul(transformMatrices[0], float4(0,0,1,1)).y,
                    mul(transformMatrices[1], float4(0,0,0,1)).x,
                    mul(transformMatrices[2], float4(0,0,0,1)).x,
                    1
                )));
                //col.r += 0.3 + (instanceID == 0);
                //col.g += 0.3 + (instanceID == 1);
                //col.b += 0.3 + (instanceID == 2);
                float headmul = -2 * ((instanceID == 0) - 0.5);
                //float worldTransform = mul(unity_LocalToWorld, transformMatrices[instanceID]);
                o.uv = readMeshData(instanceID, primitiveID, 0).uvColor;
                o.pos = UnityObjectToClipPos(mul(thisTransform, float4(- 0.001 * primitiveID -0.1,headmul * -0.1,0.,1.)) + float4(0,0,0.05,0));
                tristream.Append(o);
                o.uv = readMeshData(instanceID, primitiveID, 1).uvColor;
                o.pos = UnityObjectToClipPos(mul(thisTransform, float4(- 0.001 * primitiveID -0.,headmul * 0.2,0.,1.)) + float4(0,0,0.05,0));
                tristream.Append(o);
                o.uv = readMeshData(instanceID, primitiveID, 2).uvColor;
                o.pos = UnityObjectToClipPos(mul(thisTransform, float4(- 0.001 * primitiveID + 0.1,headmul * -0.1,0.,1.)) + float4(0,0,0.05,0));
                tristream.Append(o);*/
                g2f o = (g2f)0;
                float4x4 worldTransformCur;
                float4x4 worldTransformPrev;
                float4x4 boneTransformCur[3];
                float4x4 boneTransformPrev[3];
                float4x4 defaultTransform = (float4x4)CreateMatrixFromVert(vertin[2]);
                InputBufferElem meshBuf[3];
                uint i;
                [unroll]
                for (i = 0; i < 3; i++) {
                    InputBufferElem buf = readMeshData(instanceID, primitiveID, i);
                    meshBuf[i] = buf;
                    boneTransformCur[i] = buf.boneWeights.x * readBindTransform(buf.boneIndices.x, _FrameOffset - 1);
                    boneTransformCur[i] += buf.boneWeights.y * readBindTransform(buf.boneIndices.y, _FrameOffset - 1);
                    boneTransformCur[i] += buf.boneWeights.z * readBindTransform(buf.boneIndices.z, _FrameOffset - 1);
                    boneTransformCur[i] += buf.boneWeights.w * readBindTransform(buf.boneIndices.w, _FrameOffset - 1);
                    boneTransformPrev[i] = buf.boneWeights.x * readBindTransform(buf.boneIndices.x, _FrameOffset);
                    boneTransformPrev[i] += buf.boneWeights.y * readBindTransform(buf.boneIndices.y, _FrameOffset);
                    boneTransformPrev[i] += buf.boneWeights.z * readBindTransform(buf.boneIndices.z, _FrameOffset);
                    boneTransformPrev[i] += buf.boneWeights.w * readBindTransform(buf.boneIndices.w, _FrameOffset);
                }
                if (_FrameOffset <= 1) {
                    worldTransformCur = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);//unity_ObjectToWorld;
                } else {
                    worldTransformCur = mul(unity_WorldToObject, readBindTransform(-1, _FrameOffset - 1));
                }
                if (_FrameOffset == 0) {
                    worldTransformPrev = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);//unity_ObjectToWorld;
                } else {
                    worldTransformPrev = mul(unity_WorldToObject, readBindTransform(-1, _FrameOffset));
                }
                float4x4 boneTransform;
                float4x4 worldTransform;
                [unroll]
                for (uint j = 0; j < 1; j++) { // More than 1 crashes my Mac in unity 2017???! // j < 8; j++) {
                    if ((float)j > _TrailLength) {
                        break;
                    }
	                [unroll]
	                for (i = 0; i < 3; i++) {
	                    InputBufferElem buf = meshBuf[i];
	                    if (!((buf.boneIndices.x >= _BoneCutoff.x &&
	                            (_BoneCutoff.y == 0 || buf.boneIndices.x < _BoneCutoff.y)) ||
	                            ((_BoneCutoff.z > 0 && buf.boneIndices.x >= _BoneCutoff.z) &&
	                            (_BoneCutoff.w == 0 || buf.boneIndices.x < _BoneCutoff.w)))) {
	                        return;
	                    }
	                    float trailLerp = 1. - _FrameOffsetLerp * (j + _TrailOffset);
	                    if (dot(buf.boneWeights, buf.boneWeights) > 0.0) {
	                        // Should be the likely case.
	                        boneTransform = (float4x4)0;
	                        worldTransform = (float4x4)0;
	                        float scale = 1.0;
	                        if (_FrameOffset > 0 && trailLerp != 0) {
	                            boneTransform += trailLerp * boneTransformCur[i];
	                            worldTransform += trailLerp * worldTransformCur;
	                            scale = length(boneTransform._13_23_33);
	                        }
	                        boneTransform += (1. - trailLerp) * boneTransformPrev[i];
	                        worldTransform += (1. - trailLerp) * worldTransformPrev;
							boneTransform = lerp(boneTransform, defaultTransform, all(boneTransform._11_12_13_21 == 0.0) * all(boneTransform._22_23_31_32 == 0.0));
	                        if (_FrameOffset > 0 && trailLerp != 0) {
	                            float scale2 = length(boneTransform._13_23_33);
	                            float3 normal = normalize(boneTransform._13_23_33);
	                            float3 tangent = normalize(boneTransform._11_21_31);
	                            boneTransform._13_23_33 = scale2 * normal;
	                            boneTransform._11_21_31 = scale2 * tangent;
	                            boneTransform._12_22_32 = scale2 * cross(normal, tangent);
	                            //boneTransform._14_24_34 = scale2 / scale2 * boneTransform._14_24_34;
	                            normal = normalize(worldTransform._13_23_33);
	                            tangent = normalize(worldTransform._11_21_31);
	                            worldTransform._13_23_33 = normal;
	                            worldTransform._11_21_31 = tangent;
	                            worldTransform._12_22_32 = cross(normal, tangent);
	                        }
	                    } else {
	                        boneTransform = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
	                        worldTransform = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);//unity_ObjectToWorld;
	                    }
	                    o.pos = UnityObjectToClipPos(mul(mul(worldTransform, boneTransform), float4(buf.vertex.xyz, 1.)));
	                    o.uv = float4(buf.uvColor.xy, buf.boneIndices.x, trailLerp);
	                    tristream.Append(o);
	                }
                    tristream.RestartStrip();
                }
                /*float4 col = float4(saturate(0.7 - abs(transformMatrices[0]._24) * 0.6),
                        0.7 * abs(mul((float3x3)transformMatrices[1], float3(0,0,1)).z),
                        saturate(0.7 - abs(transformMatrices[2]._24) * 0.4), 1.);*/
                /*float4 col = saturate(0.3 * abs(float4(
                    mul(transformMatrices[0], float4(0,0,1,1)).y,
                    mul(transformMatrices[1], float4(0,0,0,1)).x,
                    mul(transformMatrices[2], float4(0,0,0,1)).x,
                    1
                )));*/
                //col.r += 0.3 + (instanceID == 0);
                //col.g += 0.3 + (instanceID == 1);
                //col.b += 0.3 + (instanceID == 2);
                //float headmul = -2 * ((instanceID == 0) - 0.5);
                //float worldTransform = mul(unity_LocalToWorld, transformMatrices[instanceID]);


                /*o.col = float4(1,.5,.5,1) * col;
                o.pos = UnityObjectToClipPos(mul(thisTransform, float4(-0.1,headmul * -0.1,0.,1.)) + float4(0,0,0.05,0));
                tristream.Append(o);
                o.col = float4(.5,1,.5,1) * col;
                o.pos = UnityObjectToClipPos(mul(thisTransform, float4(-0.,headmul * 0.2,0.,1.)) + float4(0,0,0.05,0));
                tristream.Append(o);
                o.col = float4(.5,.5,1,1) * col;
                o.pos = UnityObjectToClipPos(mul(thisTransform, float4(0.1,headmul * -0.1,0.,1.)) + float4(0,0,0.05,0));
                tristream.Append(o);*/
            }

            float4 frag(g2f fragin) : SV_Target {
                //return float4(0,0,1,1);
                float4 tex = tex2D(_MainTex, float4(fragin.uv.xy, 0, 1));
                //tex += _BoneBindTexture.Sample(sampler_MeshTex, float3(fragin.uv.xy, 0)); //tex2D(_MainTex, fragin.uv);
                clip(tex.a - 0.01);

                return tex * pow(_Color, 1. + _TrailColorPow.x + 0.25 * _TrailColorPow.y * fragin.uv.w);
                //return float4(fragin.uv.xy, 0, 1) * _Color;
            }
            ENDCG
		}
	} 
}
