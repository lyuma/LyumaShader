#ifndef CUBED_GPUSKINNED_CGINC__
#define CUBED_GPUSKINNED_CGINC__
			uniform sampler2D _MainTex; uniform float4 _MainTex_ST;
			uniform sampler2D _ColorMask; uniform float4 _ColorMask_ST;
			uniform sampler2D _EmissionMap; uniform float4 _EmissionMap_ST;
			uniform sampler2D _BumpMap; uniform float4 _BumpMap_ST;

			uniform float4 _Color;
			uniform float _Shadow;
			uniform float4 _EmissionColor;
            uniform float _DebugWeights;
            
			static const float3 grayscale_vector = float3(0, 0.3823529, 0.01845836);

            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                fixed4 color : COLOR;
                float4 texcoord0 : TEXCOORD0;
                float4 texcoord1 : TEXCOORD1;
                float4 texcoord2 : TEXCOORD2;
                float4 texcoord3 : TEXCOORD3;
            };

#ifndef GPUSKINNED_HELPERS_ONLY
            struct VertexOutput {
                float4 pos : SV_POSITION;
                float2 uv0 : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
                float3 normalDir : TEXCOORD2;
                float3 tangentDir : TEXCOORD3;
                float3 bitangentDir : TEXCOORD4;
				fixed4 col : COLOR;
                LIGHTING_COORDS(5, 6)
                UNITY_FOG_COORDS(7)
            };
#endif

            struct v2g {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float4 binormal : BINORMAL;
                float2 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 boneWeights : TEXCOORD2; // .w is this bone index
                float4 bindPose_col0 : TEXCOORD3;
                float4 bindPose_col1 : TEXCOORD4;
                float4 bindPose_col2 : TEXCOORD5;
                float4 bindPose_col3 : TEXCOORD6;
                fixed4 col : COLOR;
            };

            struct SkinnedVertexInput {
                // Filled in by applyBones.
                float4 vertex;
                float3 normal;
                float4 tangent;
                float2 uv0;
                float4 uv1;
                float3 boneWeights;
                int boneIndex;
                float4x4 boneBindPose;
                float4x4 vertexBindPose;
                fixed4 col;
                float3 normalDir;
                float3 tangentDir;
                float3 bitangentDir;
                float4 posWorld;
            };
			struct VertexPos {
				float4 vertex;
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

            void applyBones(in v2g IN[3], out SkinnedVertexInput o[3]) {
                o[0].boneBindPose = CreateMatrixFromVert(IN[0]);
                o[1].boneBindPose = CreateMatrixFromVert(IN[1]);
                o[2].boneBindPose = CreateMatrixFromVert(IN[2]);
                for (int ii = 0; ii < 3; ii++) {
                    o[ii].vertexBindPose = IN[ii].boneWeights.x * o[0].boneBindPose +
                        IN[ii].boneWeights.y * o[1].boneBindPose + IN[ii].boneWeights.z * o[2].boneBindPose;
                    o[ii].vertex = IN[ii].vertex; //mul(o[ii].vertexBindPose, IN[ii].vertex);
                    o[ii].normal = IN[ii].normal; //mul(o[ii].vertexBindPose, float4(IN[ii].normal, 0));
                    o[ii].tangent = IN[ii].tangent; //mul(o[ii].vertexBindPose, float4(IN[ii].tangent.xyz, 0));
                    o[ii].vertex = mul(o[ii].vertexBindPose, IN[ii].vertex);
                    o[ii].vertex = float4(o[ii].vertex.xyz / o[ii].vertex.w, 1.);
                    o[ii].normal = mul(o[ii].vertexBindPose, float4(IN[ii].normal, 0));
                    o[ii].tangent = mul(o[ii].vertexBindPose, float4(IN[ii].tangent.xyz, 0));
                    o[ii].uv0 = IN[ii].uv0;
                    o[ii].uv1 = IN[ii].uv1;
                    o[ii].boneWeights = IN[ii].boneWeights.xyz;
                    o[ii].boneIndex = IN[ii].boneWeights.w;
                    o[ii].col = lerp(IN[ii].col, IN[ii].boneWeights, _DebugWeights);//IN[ii].col;
                    o[ii].normalDir = UnityObjectToWorldNormal(o[ii].normal);
                    o[ii].tangentDir = normalize( mul( unity_ObjectToWorld, float4( o[ii].tangent.xyz, 0.0 ) ).xyz );
                    o[ii].bitangentDir = normalize(cross(o[ii].normalDir, o[ii].tangentDir) * IN[ii].tangent.w);
                    o[ii].posWorld = mul(unity_ObjectToWorld, o[ii].vertex);
                    UNITY_TRANSFER_FOG(o[ii],o[ii].vertex);
                }
            }

            v2g vert (VertexInput v) {
                v2g o = (v2g)0;
                o.vertex = float4(v.texcoord2.xyz, 1);
                o.normal = v.texcoord3.xyz;
                o.tangent = float4(v.texcoord0.zw, v.texcoord2.w, sign(v.texcoord3.w));
                o.uv0 = v.texcoord0.xy;
                o.uv1 = v.texcoord1;
                o.boneWeights = float4(round(v.color.rgb * 2) * .5,  abs(v.texcoord3.w));
                float scale = length(v.normal.xyz);
                //v.normal = normalize(v.normal);
                //v.tangent = float4(normalize(v.tangent.xyz), v.tangent.w);
                o.bindPose_col2 = float4(scale * normalize(v.normal.xyz), 0);//scale * float4(1,0,0,0); //float4(scale * normalize(v.normal.xyz), 0);
                o.bindPose_col0 = float4(scale * normalize(v.tangent.xyz), 0);//scale * float4(0,1,0,0); //float4(scale * normalize(v.tangent.xyz), 0);
                o.bindPose_col1 = float4(scale * cross(normalize(v.normal.xyz), normalize(v.tangent.xyz)), 0); // / scale//scale * float4(0,0,1,0); //
                //sign(v.tangent.w) * 
                o.bindPose_col3 = float4(v.vertex.xyz, 1);
                uint col = uint(v.color.a);
                uint4 col4 = uint4(7, (col & 0x38) >> 3, (col & 0x1c0) >> 6, 7);
                //o.col = fixed4(abs(v.texcoord2.w), saturate(abs(v.texcoord3.w / 32.)), saturate(abs(v.texcoord3.w / 255.)), 1.);
                o.col = fixed4(col4 / 7.); //fixed4(0.005 * scale, 0.005 * scale, 0.005 * scale, 1.); //fixed4(col / 255.);
                return o;
            }

#ifndef GPUSKINNED_HELPERS_ONLY
			float _outline_width;
			float _outline_tint;
			[maxvertexcount(6)]
			void geom(triangle v2g vertin[3], inout TriangleStream<VertexOutput> tristream)
			{
                SkinnedVertexInput IN[3];
				// Lyuma modification: compute wanted position.
                applyBones(vertin, IN);

				VertexOutput o;
				#if 0 && !DONT_OU1TLINE
				for (int i = 2; i >= 0; i--)
				{
                    o.pos = UnityObjectToClipPos(IN[i].vertex);
                    o.posWorld = mul(unity_ObjectToWorld, IN[i].vertex);
					float3 outlinePos = IN[i].vertex + normalize(IN[i].normal) * _outline_width;

					o.uv0 = IN[i].uv0;
					o.col = fixed4(_outline_tint, _outline_tint, _outline_tint, 1.);
					o.normalDir = UnityObjectToWorldNormal(IN[i].normal);
					o.tangentDir = IN[i].tangentDir;
					o.bitangentDir = IN[i].bitangentDir;
                    VertexPos v;
                    v.vertex = IN[i].vertex;
					TRANSFER_VERTEX_TO_FRAGMENT(o)
					tristream.Append(o);
				}

				tristream.RestartStrip(); 
				#endif

				// START CRAZY CODE
				// END CRAZY CODE

				for (int ii = 0; ii < 3; ii++)
				{
					o.pos = UnityObjectToClipPos(IN[ii].vertex);
					o.posWorld = mul(unity_ObjectToWorld, IN[ii].vertex);

					o.uv0 = IN[ii].uv0;
					o.col = IN[ii].col; //float4(abs(vertin[ii].vertex.xyz) * 50, 1); // fixed4(1., 1., 1., 1.);
					o.normalDir = UnityObjectToWorldNormal(IN[ii].normal);
					o.tangentDir = IN[ii].tangentDir;
					o.bitangentDir = IN[ii].bitangentDir;
                    VertexPos v;
                    v.vertex = IN[ii].vertex;
                    TRANSFER_VERTEX_TO_FRAGMENT(o)
					tristream.Append(o);
				}

				tristream.RestartStrip();
			}

			float grayscaleSH9(float3 normalDirection)
			{
				return dot(ShadeSH9(half4(normalDirection, 1.0)), grayscale_vector);
			}

            float4 frag(VertexOutput i) : COLOR {
                float4 objPos = mul ( unity_ObjectToWorld, float4(0,0,0,1) );
                i.normalDir = normalize(i.normalDir);
                float3x3 tangentTransform = float3x3( i.tangentDir, i.bitangentDir, i.normalDir);
                float3 _BumpMap_var = UnpackNormal(tex2D(_BumpMap,TRANSFORM_TEX(i.uv0, _BumpMap)));
                float3 normalDirection = normalize(mul(_BumpMap_var.rgb, tangentTransform )); // Perturbed normals
                float4 _MainTex_var = tex2D(_MainTex,TRANSFORM_TEX(i.uv0, _MainTex));
                clip(_MainTex_var.a - 0.5);
                float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 lightColor = _LightColor0.rgb;
				float attenuation =  LIGHT_ATTENUATION(i);
#ifndef CUBED_FORWARDADD_PASS
                float4 _EmissionMap_var = tex2D(_EmissionMap,TRANSFORM_TEX(i.uv0, _EmissionMap));
                float3 emissive = (_EmissionMap_var.rgb*_EmissionColor.rgb);
#endif

                float4 _ColorMask_var = tex2D(_ColorMask,TRANSFORM_TEX(i.uv0, _ColorMask));
                float3 baseColor = lerp((_MainTex_var.rgb*_Color.rgb),_MainTex_var.rgb,_ColorMask_var.r);
#ifdef CUBED_FORWARDADD_PASS
				float lightContribution = dot(normalize(_WorldSpaceLightPos0.xyz-i.posWorld.xyz),normalDirection)*attenuation;
				float3 directContribution = floor(saturate(lightContribution) * 2.0);
				float3 finalColor = baseColor * lerp(0, _LightColor0.rgb, saturate(directContribution + ((1- _Shadow) * attenuation)));
				fixed4 finalRGBA = fixed4(finalColor,1) * i.col;
#else

				float3 reflectionMap = DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, normalize((_WorldSpaceCameraPos - objPos.rgb)), 7), unity_SpecCube0_HDR)* 0.02;

                float grayscalelightcolor = dot(_LightColor0.rgb, grayscale_vector);
                float bottomIndirectLighting = grayscaleSH9(float3(0.0, -1.0, 0.0));
				float topIndirectLighting = grayscaleSH9(float3(0.0, 1.0, 0.0));
				float grayscaleDirectLighting = dot(lightDirection, normalDirection)*grayscalelightcolor*attenuation + grayscaleSH9(normalDirection);

				float lightDifference = topIndirectLighting + grayscalelightcolor - bottomIndirectLighting;
				float remappedLight = (grayscaleDirectLighting - bottomIndirectLighting) / lightDifference;
				
				float3 indirectLighting = saturate((ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)) + reflectionMap));
				float3 directLighting = saturate((ShadeSH9(half4(0.0, 1.0, 0.0, 1.0)) + reflectionMap + _LightColor0.rgb));
				float3 directContribution = saturate((1.0 - _Shadow) + floor(saturate(remappedLight) * 2.0));
                float3 finalColor = emissive + (baseColor * lerp(indirectLighting, directLighting, directContribution));
				fixed4 finalRGBA = fixed4(finalColor, 1) * i.col;
#endif
                UNITY_APPLY_FOG(i.fogCoord, finalRGBA);
                return finalRGBA;
            }
#endif

#endif
