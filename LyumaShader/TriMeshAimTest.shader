Shader "LyumaShader/TriMeshAimTest" 
{
	Properties
	{
        // Shader properties
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Base (RGB)", 2D) = "white" {}
        [HDR] _MeshTex ("Mesh Data", 2D) = "white" {}
	}
	SubShader
	{
    Tags {
            "Queue"="Geometry+10" // "Transparent+10"
            "RenderType"="Opaque" //Transparent"
    }
        // Shader code
		Pass
        {
            Cull Back
            Blend One Zero // SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 4.6

            uniform float4 _Color;
            sampler2D _MainTex;
            sampler2D _MeshTex;
            uniform float4 _MeshTex_TexelSize;

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
                float scale = 1.;///length(v.normal.xyz);
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
            };
            #define numInstancePerVert 32

            half4 readHalf4(uint2 pixelIndex) {
                float2 uvRatio = (pixelIndex + float2(0.5, 0.5)) * (_MeshTex_TexelSize.xy);
                float4 coord = float4(uvRatio.x, uvRatio.y, 0, 0);
                return tex2Dlod(_MeshTex, coord);
            }

            InputBufferElem readMeshData(uint instanceID, uint primitiveID, uint vertexNum) {
                uint pixelNum = 3 * (instanceID + primitiveID * numInstancePerVert) + vertexNum;
                uint2 pixelIndex = uint2(4 * (pixelNum % 64), pixelNum / 64);
                InputBufferElem o;
                o.vertex = readHalf4(pixelIndex + uint2(0,0));
                o.normal = readHalf4(pixelIndex + uint2(1,0));
                o.tangent = readHalf4(pixelIndex + uint2(2,0));
                o.uvColor = readHalf4(pixelIndex + uint2(3,0));
                //o.uvColor = float4(.1*readHalf4(pixelIndex + uint2(3,0)).w, 0.1*o.normal.w, o.tangent.w, 1);////float4(0.5,.5,.5,.5) + .001*readHalf4(pixelIndex + uint2(3,0)); // float4(instanceID/2. + 0.3, pixelNum / 1000.,0.,1.); //
                return o;
            }

            [instance(numInstancePerVert)]//3)]
            [maxvertexcount(6)]
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
                float4x4 transformMatrices[3];
                transformMatrices[0] = CreateMatrixFromVert(vertin[0]);
                transformMatrices[1] = CreateMatrixFromVert(vertin[1]);
                transformMatrices[2] = CreateMatrixFromVert(vertin[2]);
                /*
                float4 pos0 = transformMatrices[0]._14_24_34_44;
                float4 pos1 = transformMatrices[1]._14_24_34_44;
                float4 pos2 = transformMatrices[2]._14_24_34_44;
                transformMatrices[1]._14_24_34_44 = pos2;
                transformMatrices[2]._14_24_34_44 = pos1;
                float scale1 = distance(pos2, pos0);
                float scale2 = distance(pos1, pos2);
                float4x4 scaleMatrix1 = scale1 * float4x4(-1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
                scaleMatrix1._44 = 1.;
                float4x4 scaleMatrix2 = scale2 * float4x4(-1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
                scaleMatrix2._44 = 1.;
                */
                float distanceScale = -3. * distance(transformMatrices[1]._14_24_34, transformMatrices[2]._14_24_34);
                float destDistanceScale = -3. * distance(transformMatrices[0]._14_24_34, transformMatrices[1]._14_24_34);
                float4 srcToVertexCenterPos = float4(mul(transformMatrices[1], float4(0, 0, distanceScale, 1)));
                float4 destToVertexCenterPos = float4(mul(transformMatrices[2], float4(0, 0, destDistanceScale, 1)));
                float4 srcToVertexRightPos = float4(mul(transformMatrices[1], float4(-1, 0, 0, 1)));
                float4 destToVertexLeftPos = float4(mul(transformMatrices[2], float4(1, 0, 0, 1)));
                /*
                transformMatrices[1]._14_24_34 = float3(0,0,0);
                transformMatrices[2]._14_24_34 = float3(0,0,0);
                srcToVertexCenterPos.y = 0;
                destToVertexCenterPos.y = 0;
                float scale1 = 1;
                float scale2 = 1.;
                float4x4 scaleMatrix1 = scale1 * float4x4(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
                scaleMatrix1._44 = 1.;
                float4x4 scaleMatrix2 = scale2 * float4x4(-1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
                scaleMatrix2._44 = 1.;
                scaleMatrix1._14_24_34_44 = srcToVertexCenterPos;
                scaleMatrix2._14_24_34_44 = destToVertexCenterPos;
                scaleMatrix1._11_13_12 = float3(scale1 * normalize(transformMatrices[1]._11_13), 0);
                scaleMatrix1._31_33_32 = float3(scale1 * normalize(transformMatrices[1]._31_33), 0);
                scaleMatrix2._11_13_12 = float3(-scale2 * normalize(transformMatrices[2]._11_13), 0);
                scaleMatrix2._31_33_32 = float3(scale2 * normalize(transformMatrices[2]._31_33), 0);
                */
                float2 srcCenterToObj2D = normalize(srcToVertexRightPos.xz);
                //float2 srcCenterToObj2D = normalize(srcToVertexCenterPos.xz);
                // world in object space: [xAxis yAxis zAxis cameraPos]
                float4x4 srcVMat = float4x4(
                        float3(srcCenterToObj2D.y,0,srcCenterToObj2D.x), srcToVertexCenterPos.x,
                        float3(0,1,0), 0, //srcToVertexCenterPos.y,
                        float3(-srcCenterToObj2D.x,0,srcCenterToObj2D.y), srcToVertexCenterPos.z,
                        0., 0., 0., 1.);
                float2 destCenterToObj2D = normalize(destToVertexLeftPos.xz);
                //float2 destCenterToObj2D = normalize(destToVertexCenterPos.xz);
                // world in object space: [xAxis yAxis zAxis cameraPos]
                float4x4 destVMat = float4x4(
                        -float3(destCenterToObj2D.y,0,destCenterToObj2D.x), destToVertexCenterPos.x,
                        float3(0,1,0), 0, //destToVertexCenterPos.y,
                        float3(-destCenterToObj2D.x,0,destCenterToObj2D.y), destToVertexCenterPos.z,
                        0., 0., 0., 1.);

                g2f o = (g2f)0;
                uint i;
                float4x4 thisTransform = srcVMat; //mul(transformMatrices[1], scaleMatrix1);
                [unroll]
                for (i = 0; i < 3; i++) {
                    InputBufferElem buf = readMeshData(instanceID, primitiveID, i);
                    o.pos = UnityObjectToClipPos(mul(thisTransform, float4(buf.vertex.xyz, 1.)));
                    o.uv = float4(buf.uvColor.xy, 0, 1);
                    tristream.Append(o);
                }
                tristream.RestartStrip();

                thisTransform = destVMat; //mul(transformMatrices[2], scaleMatrix2);
                [unroll]
                for (i = 3; i > 0; i--) {
                    InputBufferElem buf = readMeshData(instanceID, primitiveID, i - 1);
                    o.pos = UnityObjectToClipPos(mul(thisTransform, float4(buf.vertex.xyz, 1.)));
                    o.uv = float4(buf.uvColor.xy, 1, 1);
                    tristream.Append(o);
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
                return lerp(tex2D(_MainTex, fragin.uv.xy).rgba, tex2D(_MainTex, fragin.uv.xy).bgra, fragin.uv.z) * _Color;
                //return float4(fragin.uv.xy, 0, 1) * _Color;
            }
            ENDCG
		}
	} 
}
