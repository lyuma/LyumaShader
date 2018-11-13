Shader "LyumaShader/BoneBlendDumperToonUnlitTransparent" 
{
	Properties
	{
        // Shader properties
		_Color ("Main Color", Color) = (0,0,0,0)
		_MainTex ("Base (RGB)", 2D) = "transparent" {}
		_DigitTex("DigitTex", 2D) = "white" {}
		_Debug("Debug Mode", Float) = 0
    }
	SubShader
	{
    Tags {
            "Queue"="Transparent+9"
            "RenderType"="Transparent"
    }
        // Shader code
        /*Pass
        {
            Cull Off
            ZTest Always //LEqual
            ZWrite On //On
            ColorMask RGBA
            Blend One Zero, One Zero // 
            BlendOp Add, Add

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata {
                float4 vertex : POSITION;
            };
            struct v2f {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata app, uint vertexId : SV_VertexID) {
                v2f o = (v2f)0;
                if ((float)vertexId == 0.0) {
                    o.vertex = float4(20.,-20.,1,1);
                }
                if ((float)vertexId == 1.0) {
                    o.vertex = float4(-20.,0.,1,1);
                }
                if ((float)vertexId == 2.0) {
                    o.vertex = float4(20.,20.,1,1);
                }
                return o;
            }
            float4 frag(v2f i) : SV_Target {
                return float4(0,0,0,0);
            }
            ENDCG
        }*/

	Pass {
		Cull Back
		CGPROGRAM
		// compile directives
		#pragma vertex vert
		#pragma fragment frag
		#pragma geometry geom
		#pragma target 4.6
		#include "HLSLSupport.cginc"
		#include "UnityShaderVariables.cginc"
		#include "UnityShaderUtilities.cginc"
		#include "UnityCG.cginc"
		struct Input
		{
			float2 uv_texcoord;
		};
        struct VertexInput {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2g {
            float2 uv : TEXCOORD0;
        };
        struct g2f {
            float4 vertex : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2g vert (VertexInput v) {
            v2g o = (v2g)0;
            o.uv = v.uv;
            return o;
        }
		uniform sampler2D _DigitTex;
		uniform float _Debug;
		static float _Digits = 8;
		static fixed _Precision = 3;
		uniform float4 _BoneBindTexture_TexelSize;
#if defined(UNITY_SINGLE_PASS_STEREO)
		static float4 stereoOffset = 3000 + unity_StereoScaleOffset[unity_StereoEyeIndex];
#else
		static float4 stereoOffset = unity_StereoScaleOffset[0];
#endif

        [maxvertexcount(4)]
        void geom(triangle v2g vertin[3], inout TriangleStream<g2f> tristream,
                uint primitiveID: SV_PrimitiveID)
        {
        	if (primitiveID >= 2 * (uint)_Debug) {
        		return;
        	}
        	float leftTex = primitiveID;
        	float4 flip = float4(primitiveID * 2. - 1., 1., 1., 1.);
            g2f o = (g2f)0;
            o.vertex = UnityObjectToClipPos(flip * float4(-.5,-.1,-0.2,1.));
            o.uv = float2(leftTex,0.);
            tristream.Append(o);
            o.vertex = UnityObjectToClipPos(flip * float4(-.5,-.1425,-0.2,1.));
            o.uv = float2(leftTex,1.);
            tristream.Append(o);
            o.vertex = UnityObjectToClipPos(flip * float4(.5,-.1,-0.2,1.));
            o.uv = float2(1.-leftTex,0.);
            tristream.Append(o);
            o.vertex = UnityObjectToClipPos(flip * float4(.5,-.1425,-0.2,1.));
            o.uv = float2(1.-leftTex,1.);
            tristream.Append(o);
            tristream.RestartStrip(); 
        }

		float2 DigitCalculator8( float2 UV , float Places , float Precision , float Value )
		{
			// Cleanup/fix Precision.
			Precision = floor(Precision);
			Precision = (Precision+1)*saturate(Precision)-1;
			float digitNum = floor(UV.x*Places); // 0 1 2 ... Digits-1
			float decimalPos = Places-Precision-1;
			float x = digitNum - decimalPos;
			float e = 1-saturate(x)+x;
			//Value += .000001;
			while(e > 0) {
			  e -= 1;
			  Value *= 10;
			}
			while(e < 0) {
			  e += 1;
			  Value /= 10;
			}
			float charNum = floor(fmod(Value,10));
			charNum = lerp(10,charNum, abs(sign(x)));
			UV.x = (frac(UV.x*Places)+charNum)/11;
			return UV;
		}


		half4 frag( g2f i ) : SV_Target
		{
			float which = floor(i.uv.x * 7);
			float2 uv_TexCoord9 = fmod(i.uv * float2( 7,1 ) + float2( 0,0 ), 1.0);
			clip(uv_TexCoord9 - 0.125);
			uv_TexCoord9 = (uv_TexCoord9 - 0.125) / 0.875;
			float2 UV8 = uv_TexCoord9;
			float Places8 = _Digits;
			float Precision8 = _Precision;
			float Value8 = which < 1. ? stereoOffset.x : (which < 2. ? unity_StereoScaleOffset[0].x :
					(which < 3. ? _BoneBindTexture_TexelSize.z :
					(which < 4. ? _ScreenParams.x : (which < 5. ? (_BoneBindTexture_TexelSize.z / _ScreenParams.x) :
					(which < 6. ? _BoneBindTexture_TexelSize.w : (which < 7. ? (_BoneBindTexture_TexelSize.w / _ScreenParams.y) :
					(123.456)))))));
			float2 localDigitCalculator88 = DigitCalculator8( UV8 , Places8 , Precision8 , Value8 );
			float4 tex2DNode1 = tex2D( _DigitTex, localDigitCalculator88 );
			return float4(tex2DNode1.rgb, 1.);
		}
		ENDCG

	}
		Pass
        {
            Cull Off
            ZTest Always //LEqual
            ZWrite On //On
            ColorMask RGBA
            Blend One Zero, One Zero // SrcAlpha OneMinusSrcAlpha
            BlendOp Add, Add

            CGPROGRAM
            #include "HLSLSupport.cginc"
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 4.6

            struct VertexInput {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                half4 color : COLOR; // name in ASCII
                float4 uv1 : TEXCOORD0;
            };

            struct v2g {
                float4 uv1 : TEXCOORD0;
                half4 color : TEXCOORD2;
                float4 bindPose_col0 : TEXCOORD3;
                float4 bindPose_col1 : TEXCOORD4;
                float4 bindPose_col2 : TEXCOORD5;
                float4 bindPose_col3 : TEXCOORD6;
            };
            struct g2f {
                float4 vertex : SV_POSITION;
                //float4 normal : TEXCOORD1;
                //float4 tangent : TEXCOORD2;
                float4 color : TEXCOORD0;
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
                o.color = v.color;
                o.uv1 = v.uv1;
                return o;
            }

            uniform float4 _BoneBindTexture_TexelSize;
            Texture2D<half4> _BoneBindTexture;

//////////////////
/// COPIED FROM SkinnedMesh ///
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
///////////////////////////

            float2 pixelToUV(float2 pixelCoordinate, float2 offset) {
                //return (floor(pixelCoordinate) + offset) / _ScreenParams.xy;
                float2 correctedTexelSize = _BoneBindTexture_TexelSize.zw;
                if (correctedTexelSize.x / _ScreenParams.x > 1.9) {
                    correctedTexelSize.x *= 0.5;
                }
                return (floor(pixelCoordinate) + offset) / correctedTexelSize;
            }

            void appendPixelToStream(inout TriangleStream<g2f> tristream, float2 pixelCoordinate, float4 color) {

                g2f o = (g2f)0;
                o.color = color * 0.1;
#if UNITY_UV_STARTS_AT_TOP
                float2 uvflip = float2(1., -1.);
#else
                float2 uvflip = float2(1., 1.);
#endif
                o.vertex = float4(uvflip*(pixelToUV(pixelCoordinate, float2(0.,0.)) * 2. - float2(1.,1.)), 0., 1.);
                tristream.Append(o);
                o.vertex = float4(uvflip*(pixelToUV(pixelCoordinate, float2(1.,0.)) * 2. - float2(1.,1.)), 0., 1.);
                tristream.Append(o);
                o.vertex = float4(uvflip*(pixelToUV(pixelCoordinate, float2(0.,1.)) * 2. - float2(1.,1.)), 0., 1.);
                tristream.Append(o);
                o.vertex = float4(uvflip*(pixelToUV(pixelCoordinate, float2(1.,1.)) * 2. - float2(1.,1.)), 0., 1.);
                tristream.Append(o);
                tristream.RestartStrip(); 
            }

            void appendPixelToStream(inout PointStream<g2f> ptstream, float2 pixelCoordinate, float4 color) {

                g2f o = (g2f)0;
                o.color = color * 0.1;
#if UNITY_UV_STARTS_AT_TOP
                float2 uvflip = float2(1., -1.);
#else
                float2 uvflip = float2(1., 1.);
#endif
                o.vertex = float4(uvflip*(pixelToUV(pixelCoordinate, float2(.5,.5)) * 2. - float2(1.,1.)), 0., 1.);
                ptstream.Append(o);
            }

            [maxvertexcount(10)]
            void geom(triangle v2g vertin[3], inout PointStream<g2f> ptstream,
                    uint primitiveID: SV_PrimitiveID)
            {
                /*#ifdef USING_STEREO_MATRICES
                return;
                #endif
                if (unity_CameraProjection[2][0] != 0.f || unity_CameraProjection[2][1] != 0.f) {
                    return;
                }
                if(distance(mul(unity_ObjectToWorld,float4(0,0,0,1)).xyz, _WorldSpaceCameraPos) >= 0.001) {
                    return;
                }*/
                if (vertin[0].uv1.x >= 1. && vertin[0].uv1.y == 0.) {
                    float4x4 transformMatrix = unity_ObjectToWorld;
                    appendPixelToStream(ptstream, float2(0, 0), transformMatrix._11_21_31_41.yzwx);
                    appendPixelToStream(ptstream, float2(0, 1), transformMatrix._12_22_32_42.zwxy);
                    appendPixelToStream(ptstream, float2(0, 2), transformMatrix._13_23_33_43.wxyz);
                    appendPixelToStream(ptstream, float2(0, 3), transformMatrix._14_24_34_44.xyzw);
                    appendPixelToStream(ptstream, float2(0, 5), transformMatrix._11_22_33_44.xyzw);

                    float4x4 identity = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
                    float4x4 oldTransformMatrix = readBindTransform(-1, 0);
                    oldTransformMatrix = lerp(oldTransformMatrix, transformMatrix, all(oldTransformMatrix._11_12_13_21 == 0.0) * all(oldTransformMatrix._22_23_31_32 == 0.0));
                    float4x4 oldTransformMatrixRA = readBindTransform(-1, 1);
                    oldTransformMatrixRA = lerp(oldTransformMatrixRA, transformMatrix, all(oldTransformMatrixRA._11_12_13_21 == 0.0) * all(oldTransformMatrixRA._22_23_31_32 == 0.0));
                    float4x4 newTransformMatrix = lerp(oldTransformMatrix, oldTransformMatrixRA, 0.95);

                    appendPixelToStream(ptstream, float2(0, 6 + 0), newTransformMatrix._11_21_31_41.yzwx);
                    appendPixelToStream(ptstream, float2(0, 6 + 1), newTransformMatrix._12_22_32_42.zwxy);
                    appendPixelToStream(ptstream, float2(0, 6 + 2), newTransformMatrix._13_23_33_43.wxyz);
                    appendPixelToStream(ptstream, float2(0, 6 + 3), newTransformMatrix._14_24_34_44.xyzw);
                    appendPixelToStream(ptstream, float2(0, 6 + 5), newTransformMatrix._11_22_33_44.xyzw);
                } else if (vertin[0].uv1.x >= 1.) {
                    // blend shape
                    float blendValue = vertin[0].bindPose_col3.x;

                    appendPixelToStream(ptstream, float2(vertin[0].uv1.y, 4), float4(blendValue, 0.5, 1, 1.));
                } else {
                    float4x4 transformMatrix = CreateMatrixFromVert(vertin[0]);
                    float boneId = vertin[0].uv1.y;
                    appendPixelToStream(ptstream, float2(1+boneId, 0), transformMatrix._11_21_31_41.yzwx);
                    appendPixelToStream(ptstream, float2(1+boneId, 1), transformMatrix._12_22_32_42.zwxy);
                    appendPixelToStream(ptstream, float2(1+boneId, 2), transformMatrix._13_23_33_43.wxyz);
                    appendPixelToStream(ptstream, float2(1+boneId, 3), transformMatrix._14_24_34_44.xyzw);
                    appendPixelToStream(ptstream, float2(1+boneId, 5), transformMatrix._11_22_33_44.xyzw);

                    float4x4 identity = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
                    float4x4 oldTransformMatrix = readBindTransform(boneId, 0);
                    oldTransformMatrix = lerp(oldTransformMatrix, transformMatrix, all(oldTransformMatrix._11_12_13_21 == 0.0) * all(oldTransformMatrix._22_23_31_32 == 0.0));
                    float4x4 oldTransformMatrixRA = readBindTransform(boneId, 1);
                    oldTransformMatrixRA = lerp(oldTransformMatrixRA, transformMatrix, all(oldTransformMatrixRA._11_12_13_21 == 0.0) * all(oldTransformMatrixRA._22_23_31_32 == 0.0));
                    float4x4 newTransformMatrix = lerp(oldTransformMatrix, oldTransformMatrixRA, 0.95);

                    appendPixelToStream(ptstream, float2(1+boneId, 6 + 0), newTransformMatrix._11_21_31_41.yzwx);
                    appendPixelToStream(ptstream, float2(1+boneId, 6 + 1), newTransformMatrix._12_22_32_42.zwxy);
                    appendPixelToStream(ptstream, float2(1+boneId, 6 + 2), newTransformMatrix._13_23_33_43.wxyz);
                    appendPixelToStream(ptstream, float2(1+boneId, 6 + 3), newTransformMatrix._14_24_34_44.xyzw);
                    appendPixelToStream(ptstream, float2(1+boneId, 6 + 5), newTransformMatrix._11_22_33_44.xyzw);
                }
            }

            float4 frag(g2f fragin) : SV_Target {
                return float4(fragin.color.xyzw);
            }
            ENDCG
		}
        GrabPass { "_BoneBindTexture" }
	}
}
