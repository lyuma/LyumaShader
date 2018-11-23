// Shader which uses Waifu2d.cginc to render a flat shadow behind an avatar.
// Compatible with the same set of properties used in other flat shaders.
// This shader was not Waifu2d Generated.
Shader "LyumaShader/DropShadowLiteToonTransparentScale"
{
    Properties
    {
        _2d_coef ("Twodimensionalness", Range(0, 1)) = 0.99
        _facing_coef ("Face in Profile", Range (-1, 1)) = 0.0
        _lock2daxis_coef ("Lock 2d Axis", Range (0, 1)) = 1.0
        _target_dist ("Target dist", Range(-2, 2)) = 0.0
        _scary ("Scariness", Range(0, 1)) = 1.0
        //_local3d_coef ("See self in 3d", Range (0, 1)) = 0.0
        //_zcorrect_coef ("Squash Z (good=.975; 0=3d; 1=z-fight)", Float) = 0.975
        //_ztweak_coef ("Tweak z clip", Range (-1, 1)) = 0.0
        [Enum(Always,0,VROnly,1)] _DisplayVROnly ("Visibility", Int) = 0
        [Enum(Off,0,FaceMirror,1)] _FaceMirrors ("Face Mirror/Camera?", Int) = 0
        _shadow_offset ("Shadow Offset [W=hardness,0-100]", Vector) = (0.03,0.015,0,3)
        _Color ("Color", Color) = (0,0,0,1)
        _MainTex("Main Tex", 2D) = "transparent" {}
        [HideInInspector] _texcoord( "", 2D ) = "white" {}
        [HideInInspector] __dirty( "", Int ) = 1
    }

    SubShader
    {
        Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+503" "IsEmissive" = "true" "IgnoreProjector"="True"  }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcColor

                    CGINCLUDE
            //#define UNITY_PASS_FORWARDADD
            #include "UnityCG.cginc"
/*            #define NO_UNIFORMS
static float _2d_coef = 1.;
uniform float _facing_coef;
uniform float _lock2daxis_coef;
uniform float _ztweak_coef;*/

uniform float4 _shadow_offset;
uniform float4 _Color;
#include "../Waifu2d/Waifu2d.cginc"

struct VertexInput {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 texcoord0 : TEXCOORD0;
};
struct v2f_surf {
    float4 normalCameraNormW : TEXCOORD1;
    float3 worldPos : TEXCOORD0;
    float4 pos : SV_POSITION;
    float4 color : TEXCOORD2;
};
v2f_surf vert (VertexInput v) {
    v2f_surf o = (v2f_surf)0;
    //float4 tmp = v.vertex;
    //v.vertex = waifu_preprocess(v.vertex, v.normal, tmp);// * 1.04;
    float3 worldNormal = UnityObjectToWorldNormal(v.normal);

    float3 bitang = myInvVMat._21_22_23; //normalize(posWorld - mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz);
    float3 tang = myInvVMat._11_12_13; //cross(bitang, normalize(posWorld - mul(unity_ObjectToWorld, float4(0,0,1,1)).xyz));
    float3 norm = myInvVMat._31_32_33; //cross(tang, bitang);
    bitang = cross(norm, tang);
    tang = cross(bitang, norm);
    float normAlignment = (abs(dot(normalize(worldNormal), normalize(norm))));//1 - max(abs(dot(normalize(v.normal), tang)), abs(dot(normalize(v.normal), bitang)));
    //normAlignment = pow(normAlignment, 0.5);
    normAlignment = saturate(normAlignment * 1.3);
    normAlignment = pow(normAlignment, 1.25);

    float4 origWorldPos = mul(unity_ObjectToWorld, v.vertex * lerp(_shadow_offset.z, 1.0, saturate(v.vertex.y)));
    float4 actualObjectPos = waifu_computeVertexWorldPos(v.vertex * lerp(_shadow_offset.z, 1.0, saturate(v.vertex.y)));
    //float4 actualObjectPos2 = mul(unity_WorldToObject, actualObjectPos);
    float3 posWorld = actualObjectPos.xyz;
    float3 baseCamDist = (realCameraPos.xyz - origWorldPos.xyz);
    float3 projCamDist = (realCameraPos.xyz - posWorld.xyz);
    float3 realZPoint = posWorld - normalize(baseCamDist) * (100. * (0.00124 + 0.00006 * (1-normAlignment)));// * saturate(1. * length(baseCamDist - projCamDist)))); // (1. - color)));
    float4 projZPoint = UnityWorldToClipPos(float4(realZPoint.xyz, 1));

    //actualObjectPos += float4(-0.03*bitang,0.);
    actualObjectPos += float4((_shadow_offset.y * 2. * norm + _shadow_offset.x * tang + _shadow_offset.y * bitang),0.);// * dot(approxEyeDir.xz, cameraToObj2D.xy), 0.);
    o.worldPos = actualObjectPos; //mul(unity_ObjectToWorld, actualObjectPos2);
    //o.worldPos += baseCamDist
    o.pos = UnityWorldToClipPos(actualObjectPos);
    o.pos.z = projZPoint.z * o.pos.w / projZPoint.w;
    o.normalCameraNormW = float4(v.normal, max(_shadow_offset.w, abs(dot(normalize(baseCamDist), norm))));
    //fixed3 worldNormal = UnityObjectToWorldNormal( v.normal );
    o.color = float4(_Color.rgb,normAlignment);
#if !defined(USING_STEREO_MATRICES)
    o.pos = _DisplayVROnly * (!isInMirror) > 0. ? float4(1,1,1,1) : o.pos;
#endif
    return o;
}

float4 frag(v2f_surf i) : COLOR {
    //clip(1.3 - i.color.a);
    //i.color.a = lerp(i.color.a, i.color.a > 1.3 ? 0. : i.color.a, 1.);
    //clip(0.5 - i.color.a);
    //i.color.a = sqrt(i.color.a);
    return float4(i.color.rgb, _Color.a * saturate(sqrt(1.04 * i.color.a * sqrt(i.normalCameraNormW.w))));//saturate(2 * pow(saturate(i.color.a * i.normalCameraNormW.w), 0.8)));
}
    ENDCG


        Pass
        {
        ColorMask 0
        ZWrite On
        ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature VR_ONLY_2D
            #define SHADOW_ZWRITE_PASS
            ENDCG
    }
        Pass
        {
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Equal
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature VR_ONLY_2D
            #define SHADOW_RENDER_PASS
            ENDCG
    }

    } // subshader
    //CustomEditor "ASEMaterialInspector"
}
