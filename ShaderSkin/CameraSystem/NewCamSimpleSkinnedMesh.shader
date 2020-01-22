Shader "LyumaShader/NewCamShaderSkin/NewCamSimpleSkinnedMesh" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		[HDR] _MeshTex ("Mesh Data (Bind transforms)", 2D) = "white" {}
        [HDR] _BoneDataInputTexture ("Mesh Data (Bind transforms)", 2D) = "white" {}
		_Glossiness ("Smoothness", Range(0,1)) = 0.5
		_Metallic ("Metallic", Range(0,1)) = 0.0
		_xtracursed ("_xtracursed", Range(0,1)) = 0.0
		[Enum(Off,0,Front,1,Back,2)] _Culling ("Culling Mode", Int) = 2
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200
		Cull [_Culling]

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf Standard fullforwardshadows vertex:vertModifierOuter
		#define _BoneBindTexture _BoneDataInputTexture
		#define _BoneBindTexture_TexelSize _BoneDataInputTexture_TexelSize
		half _Glossiness;
		// #define FACTOR (100*_Glossiness)
		#include "NewShaderSkin.cginc"

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D _MainTex;

		struct Input {
			float2 uv_MainTex;
		};

		half _Metallic;
		fixed4 _Color;

		// Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
		// See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
		// #pragma instancing_options assumeuniformscaling
		UNITY_INSTANCING_BUFFER_START(Props)
			// put more per-instance properties here
		UNITY_INSTANCING_BUFFER_END(Props)

float signpow(float base, float exp) {
    return sign(base) * pow(abs(base), exp);
}
float2 signpow(float2 base, float2 exp) {
    return sign(base) * pow(abs(base), exp);
}

float4x4 create4x4RotationMatrixSinCos(float3 axis, float2 mysincos)
{
    float s = mysincos.x;
    float c = mysincos.y;
    float oc = 1.0 - c;
    
    return float4x4(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s,0,
                oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,0,
                oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c,0,
                0,0,0,1);
}

#ifdef USING_STEREO_MATRICES
static float3 realCameraPos = 0.5 * (unity_StereoWorldSpaceCameraPos[0] +  unity_StereoWorldSpaceCameraPos[1]);
#else
static float3 realCameraPos = _WorldSpaceCameraPos;
#endif

static float _misete_coef = 0.4;
static float waifu_coef = 0;

void positionModHairHack(inout float4 inVertex, inout float3 inNormal, inout float4 inTangent) {
    /*
    float4 hairQuat;
    float3 hairPos;
    readBoneQP(4, 0, hairQuat, hairPos);
    //hairQuat = q_inverse(qmul(normalize(float4(1,0,0,0)), hairQuat)); //normalize(rotate_angle_axis(float3(1,0,0),3.1415));
    //hairQuat = qmul(hairQuat, rotate_angle_axis(float3(1,0,0),3.1415));
    //float4x4 mat = boneQPToTransform(hairQuat, hairPos);
    float3x3 mat = (float3x3)boneQPToTransform(hairQuat, hairPos);
    inVertex.xyz = mul(mat, inVertex.xyz - hairPos) + hairPos;
    inNormal = mul(mat, inNormal);
    inTangent.xyz = mul(mat, inTangent.xyz);
    */
}

float4 q_conj(float4 q)
{
    return float4(-q.x, -q.y, -q.z, q.w);
}
float4 qmul(float4 q1, float4 q2)
{
    return float4(
        q2.xyz * q1.w + q1.xyz * q2.w + cross(q1.xyz, q2.xyz),
        q1.w * q2.w - dot(q1.xyz, q2.xyz)
    );
}


float4 q_inverse(float4 q)
{
    float4 conj = q_conj(q);
    return conj / (q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
}
float4 rotate_angle_axis(float angle, float3 axis)
{
    float sn = sin(angle * 0.5);
    float cs = cos(angle * 0.5);
    return float4(axis * sn, cs);
}

void positionModClassic(inout float4 inVertex, inout float3 inNormal, inout float4 inTangent, inout float2 uv, float4 color, float _Frilling) {
    if (_Frilling > 0 && uv.x > 0.92) {
        inVertex = float4(1,1,1,1);
    }
    float2 origUv = uv;
    float skirtHeight = 0.25 + 5 * (uv.x - 0.7)*(uv.x - 0.7) - uv.y;
    if (any(uv < .5) && (uv.x > 0.02 || uv.y < 0.98)) {
        if (uv.x < 0.49 || uv.y > 0.308 || uv.x > 0.909) {
            return;
        }
        if (abs(uv.x - 0.69140625) < 0.05 && uv.y > 0.292) {
            return;
        }
        if (uv.x > 0.81640625 && uv.y > 0.27734375) {
            return;
        }
        if (uv.x > 0.890625 && abs(uv.y - 0.15625) < 0.02) {
            return;
        }
        if (uv.x > 0.92 || skirtHeight < 0) {
            if (uv.y > 0.252) {
                uv.y -= 0.06;
            }
            return;
        }

        {
            float3 worldDown = normalize(mul((float3x3)unity_WorldToObject, float3(0,-1,0)));
            float3 localDown = float3(0,1,0);
            //if (IN[0].boneType > 3.5) {
                //localDown = mul((float3x3)boneBindPose, float3(0,-1,0));
            //}
            float3 relativeAcross = normalize(cross(localDown, worldDown));
            float3 relativeAnchor = normalize(cross(worldDown, relativeAcross));
            // IN[ii].vertex
            float2 mysincos = float2(dot(localDown, relativeAnchor), dot(localDown, worldDown));
            mysincos = normalize(lerp(float2(0,1), mysincos, length(inVertex.xyz)));
            float4x4 boneBindPose = (float4x4)create4x4RotationMatrixSinCos(relativeAcross, mysincos);
            boneBindPose._44 = 1;
            inVertex = mul(boneBindPose, inVertex);
            inNormal = mul((float3x3)boneBindPose, inNormal);
            inTangent = float4(mul((float3x3)boneBindPose, inTangent.xyz), inTangent.w);
        }
        float windaddangle = lerp(.07,.23,signpow(sin(_Time.g), 3) * .5 + .5) * pow(abs(sin((7 * _Time.y + inVertex.x * 31.6 + inVertex.y * 11) * 0.3)), 4);
        //windaddangle += lerp(0,2,_misete_coef*(pow(sin(_Time.g), 3) * .5 + .5)) * pow(sin((7 * _Time.y + inVertex.y * 11) * 0.3), 4);
        {
            //float addangle = 0.3 * sin((7 * _Time.y + 31.3113 * skirtHeight + 27.133 * origUv.x) * 0.3);
            float3 axis1 = normalize(inVertex.xyz + 0.2 * float3(sin(0.77 * _Time.y), sin( 1.03 * _Time.y) * cos(0.95 * _Time.y), cos(0.83 * _Time.y)));
            float3 axis2 = float3(0,1,0);
            float3 axis3 = normalize(cross(axis1, axis2));
            //outAxis3 = axis3;
            float3x3 outRotMat = rotationMatrix(axis3, windaddangle);
            inVertex = float4(mul(inVertex.xyz - float3(0,0.05,0), outRotMat) + float3(0,0.05,0), 1);
            inNormal = mul(inNormal.xyz, outRotMat);
            inTangent.xyz = mul(inTangent.xyz, outRotMat);
        }
    } else {
    float4 hairQuat;
    float3 hairPos;
    float3 offset = float3(0,0.1,-0.1);
    readBoneQP(4, 0, hairQuat, hairPos);
    //hairQuat = q_inverse(qmul(normalize(float4(1,0,0,0)), hairQuat)); //normalize(rotate_angle_axis(float3(1,0,0),3.1415));
    //hairQuat = qmul(hairQuat, rotate_angle_axis(float3(1,0,0),3.1415));
    //float4x4 mat = boneQPToTransform(hairQuat, hairPos);
    float3x3 invmat = (float3x3)boneQPToTransform(q_inverse(hairQuat), hairPos);
    inVertex.xyz = mul(invmat, inVertex.xyz - hairPos) - offset;
    inNormal = mul(invmat, inNormal);
    inTangent.xyz = mul(invmat, inTangent.xyz);
    float3x3 mat = (float3x3)boneQPToTransform((hairQuat), hairPos);

    float3 localDown = float3(0,-1,0);//mul(mat, float3(0,0,-1));
    float3 worldDown = normalize(mul(invmat, mul((float3x3)unity_WorldToObject, float3(0,-1,0))));
    //if (IN[0].boneType > 3.5) {
        //localDown = mul((float3x3)boneBindPose, float3(0,-1,0));
    //}
    float3 relativeAcross = normalize(cross(localDown, worldDown));
    float3 relativeAnchor = normalize(cross(worldDown, relativeAcross));
    // IN[ii].vertex
    float factor = saturate(1 + dot(localDown, worldDown));
    float2 mysincos = float2(dot(localDown, relativeAnchor), dot(localDown, worldDown));
    float period = 0.5 * (sin(_Time.y * 5) + 0.6 * cos(_Time.y * 4) * cos(_Time.y * 4));
    mysincos = normalize(lerp(float2(0,1), mysincos, sqrt(factor) * (0.8 + 0.4 * period *period*period*period*period) *
                pow(saturate(length(inVertex.xyz) * 1.7 - 0.3), 0.5)));
    float4x4 boneBindPose = (float4x4)create4x4RotationMatrixSinCos(relativeAcross, mysincos);
    boneBindPose._44 = 1;
    inVertex = mul(boneBindPose, inVertex);
    inNormal = mul((float3x3)boneBindPose, inNormal);
    inTangent = float4(mul((float3x3)boneBindPose, inTangent.xyz), inTangent.w);

    inVertex.xyz = mul(mat, inVertex.xyz + offset) + hairPos;
    inNormal = mul(mat, inNormal);
    inTangent.xyz = mul(mat, inTangent.xyz);

        /*
        if (origUv.x < 0.01 && origUv.y > 0.99) {
            origUv = float2(origUv.x, 1 - (1 - origUv.y) * 40);
            origUv += float2(.4689,-.0133);
        }
        skirtHeight = 30 * (origUv.y > 0.742 ? (0.97 - origUv.y) : (0.72 - origUv.y));
        if (skirtHeight < 0) {
            return;
        }
        float hairtimepr = origUv.y * 0.5 + _Time.g;
        float hairwindaddangle = (.1*smoothstep(0,1,skirtHeight*pow(1 + sin(hairtimepr * 2.81), 3) * .5 + .5)) * skirtHeight * (1.5*sin((.7 * hairtimepr + 1.173) * 0.3) + .3*cos(3.9 * hairtimepr + 0.331));
        if (origUv.y < 0.72) {
            // back hair
            float windaddangle = lerp(.2 + .17 * cos(uv.x + _Time.g * 3.114),.6,signpow(sin(_Time.g), 3) * .5 + .5) * (0.3 + 0.3 * signpow(hairwindaddangle, 0.3)) * skirtHeight * (1 + sin((7 * _Time.y + floor(sign(inTangent.w) * uv.x * 11) * 11.6 + uv.y * 3.3) * 0.3));
            //float windaddangle = hairwindaddangle * sign(inTangent.w) * 4 + 0.4;
            inVertex.xyz += 0.05 * inTangent.xyz * windaddangle + (0.003 * skirtHeight + 0.01 * saturate(2 - (skirtHeight - 1) * (skirtHeight - 1))) * lerp(inNormal, normalize(inVertex.xyz), 1.5).xyz;
        } else if (origUv.y < 0.9 && (origUv.y > 0.763 || (origUv.x > 0.8 && origUv.y > 0.753) || origUv.x > 0.815 )) {
            // bangs
            //float windaddangle = (.1*smoothstep(0,1,skirtHeight*pow(1 + sin(_Time.g * 2.81), 3) * .5 + .5)) * skirtHeight * (1.5*sin((.7 * _Time.y + 1.173) * 0.3) + .3*cos(3.9 * _Time.g + 0.331));
            //inVertex.xyz += 0.01 * normalize(inVertex.xyz).xyz * windaddangle;
            inVertex.xyz += lerp(0,0.01,skirtHeight * saturate(3 * (.7 - (uv.x - 0.5)))) * cross(normalize(inVertex.xyz).xyz, float3(-0.7 * normalize(inVertex.xyz).xz,1).xzy) * hairwindaddangle;
        }*/
        return;
    }
    if (_Frilling > 0) {
        float outsize = 0.3 + 0.05 * _Frilling;
        float outmul = 1.9 + 0.6 * _Frilling;
        //v.2.0.4.3 baked Normal Texture for Outline                
        //normalize(cross(v.normal, v.tangent.xyz))*0.1* (v.tangent.w < 0 ? -1 : 1)
        //o.pos = UnityObjectToClipPos(lerp(float4(v.vertex.xyz - mul(unity_WorldToObject, o.bitangentDir)*0.1 + v.normal*Set_Outline_Width,1), float4(v.vertex.xyz + _BakedNormalDir*Set_Outline_Width,1),_Is_BakedNormal));
        //o.pos = UnityObjectToClipPos(float4(v.vertex.xyz * float2(sqrt(v.vertex.y)*1.8, 1.3).xyx + float3(0,-0.03,0),1));
        //o.pos = UnityObjectToClipPos(float4(v.vertex.xyz * float3(pow(v.vertex.y, 0.5)*1.9, pow(v.vertex.y, 0.3) * 0.9, pow(v.vertex.y, 0.1) * 4.3).xzy + float3(0,-0.12,0),1));
        inVertex = float4(float2(inVertex.xz * signpow(inVertex.yy, float2(0.3,0.25)) * float2(1.6,1.2)), (outsize - signpow(0.3 - inVertex.y, 1.5) * outmul), 1).xzyw;
        uv = float2(0.5 * uv.x + 0.3, pow(abs(uv.y), 0.75) * 0.33 - 0.06);//, sqrt(o.uv0) * float2(0.9,0.1) + float2(0.1, -0.03);////float2(0.3, 1.0) + float2(0.6, 0.4);
    }
/*#ifdef _OUTLINE_NML
#ifdef OUTLINEEXTRA
float outsize = 0.4;
float outmul = 3.1;
#else
float outsize = 0.35;
float outmul = 2.5;
#endif
                //v.2.0.4.3 baked Normal Texture for Outline                
                //normalize(cross(v.normal, v.tangent.xyz))*0.1* (v.tangent.w < 0 ? -1 : 1)
                //o.pos = UnityObjectToClipPos(lerp(float4(v.vertex.xyz - mul(unity_WorldToObject, o.bitangentDir)*0.1 + v.normal*Set_Outline_Width,1), float4(v.vertex.xyz + _BakedNormalDir*Set_Outline_Width,1),_Is_BakedNormal));
                //o.pos = UnityObjectToClipPos(float4(v.vertex.xyz * float2(sqrt(v.vertex.y)*1.8, 1.3).xyx + float3(0,-0.03,0),1));
                //o.pos = UnityObjectToClipPos(float4(v.vertex.xyz * float3(pow(v.vertex.y, 0.5)*1.9, pow(v.vertex.y, 0.3) * 0.9, pow(v.vertex.y, 0.1) * 4.3).xzy + float3(0,-0.12,0),1));
                o.pos = UnityObjectToClipPos(float4(float2(v.vertex.xz * pow(v.vertex.y, float2(0.3,0.25)) * float2(1.6,1.2)), (outsize - pow(0.3 - v.vertex.y, 1.5) * outmul), 1).xzyw);
                o.uv0 = float2(0.5 * o.uv0.x + 0.3, pow(o.uv0.y, 0.75) * 0.33 - 0.06);//, sqrt(o.uv0) * float2(0.9,0.1) + float2(0.1, -0.03);////float2(0.3, 1.0) + float2(0.6, 0.4);
                */
    float2 normalApprox = normalize(inVertex.xz);
    float4 localCameraPos = mul(unity_WorldToObject, float4(realCameraPos, 1));
    float2 cameraVec = normalize(localCameraPos.xz);
    float3 cameraVec3d = normalize(localCameraPos.xyz);
    //inVertex.z *= lerp(1, 1.25, saturate(15 * skirtHeight));
    float EXT;
    if (_misete_coef < 0) {
        float ndotv = saturate(dot(normalApprox, cameraVec));
        {
            float addangle = signpow(-ndotv,3)*3.14159*.7/2;
            float lerpFactor2 = lerp(saturate(1 - cameraVec3d.y), cameraVec.y * cameraVec.y * abs(cameraVec3d.y), waifu_coef);
if (_Frilling) {
            EXT = 0.075;
} else {
            EXT = 0.03;
}
            float actual_misete = _misete_coef * lerp(0.6, 1.3, pow(0.5 + 0.25 * (sin(_Time.g * 0.1014 + 13.1827) + cos(_Time.g * 0.037226 + 11.2873)), 4));
            addangle = 0.5 * lerp(0, addangle, saturate(10 * (inVertex.y - EXT)) * lerpFactor2 * -actual_misete);
            addangle += lerp(0,0.3 + 0.5 * saturate(1 - signpow(ndotv,3)),-actual_misete*(signpow(sin(_Time.g), 3) * .5 + .5)) * pow(abs(sin((7 * _Time.y + inVertex.y * 11) * 0.3)), 4);
            float wavyscale = 0.2;
            //if (_Frilling > 1.5) {
                float xactual_misete = 0.7;
                float coef = saturate(pow(saturate(cameraVec3d.y - 0.2), 2) * 3.0) * 1.3  * sqrt(ndotv * 0.5 + 0.5);
                addangle *= lerp(1, 0.3, saturate(coef * abs(xactual_misete * 2)));
                addangle += -.5 * 1.2 * saturate(coef * abs(xactual_misete * 2));
                wavyscale *= lerp(1, 0.1, saturate(coef * abs(xactual_misete * 2)));
            //}
            addangle *= saturate(1 - .4 * saturate(_Frilling) * saturate(-normalize(inVertex.xz).y + 0.2)); // dampen front movement to prevent frilling clipping through skirt in front
            float3 axis1 = normalize(inVertex.xyz + wavyscale * float3(sin(0.77 * _Time.y), sin( 1.03 * _Time.y) * cos(0.95 * _Time.y), cos(0.83 * _Time.y)));
            float3 axis2 = float3(0,1,0);
            float3 axis3 = normalize(cross(axis1, axis2));
            //outAxis3 = axis3;
            float3x3 outRotMat = rotationMatrix(axis3, addangle);
            inVertex = float4(mul(inVertex.xyz - float3(0,0.05,0), outRotMat) + float3(0,0.05,0), 1);
            inNormal = mul(inNormal.xyz, outRotMat);
            inTangent.xyz = mul(inTangent.xyz, outRotMat);
        }
        /*
        float4 ret = inVertex;
        float addangle = sqrt(ndotv)*3.14159*1.15/2;
        float mult = cos(addangle);
        //float mult = cos(ndotv*3.14159/2);
        ret.y *= mult;
        float3 normInVertex = normalize(ret);
        float angle = atan2(normInVertex.y,length(normInVertex.xz));
        ret.xz *= (2 - cos(angle));
        float interpFactor = _misete_coef;
        float lerpFactor1 = (normalize(localCameraPos).y * 0.5 + 0.5 + 0.5 * saturate(interpFactor - 1));
        float lerpFactor2 = lerp(1.0, cameraVec.y * cameraVec.y * cameraVec.y * cameraVec.y, waifu_coef);
        ret = lerp(inVertex, ret, (inVertex.y > 0) * lerpFactor1 * lerpFactor1 * lerpFactor2);
        inVertex = inVertex * (1 - interpFactor) + ret * interpFactor;
        */
    } else {
        float ndotv = saturate(dot(normalApprox, cameraVec));
        float addangle = pow(ndotv,5)*3.14159*.7/2;
        float lerpFactor2 = lerp(saturate(1 - cameraVec3d.y), cameraVec.y * cameraVec.y * abs(cameraVec3d.y), waifu_coef);
if (_Frilling) {
        EXT = 0.1;
        //inVertex.y *= lerp(1.0,0.7,saturate(_misete_coef * 1.3));
} else {
        EXT = 0.03;
        //inVertex.y *= lerp(1.0,0.8,_misete_coef);
}
        addangle = 0.5 * lerp(0, addangle, saturate(10 * (inVertex.y - EXT)) * lerpFactor2 * _misete_coef);
        addangle += lerp(0,0.5,_misete_coef*(signpow(sin(_Time.g), 3) * .5 + .5)) * pow(abs(sin((7 * _Time.y + inVertex.y * 11) * 0.3)), 4);
        float3 axis1 = normalize(inVertex.xyz + 0.2 * float3(sin(0.77 * _Time.y), sin( 1.03 * _Time.y) * cos(0.95 * _Time.y), cos(0.83 * _Time.y)));
        float3 axis2 = float3(0,1,0);
        float3 axis3 = normalize(cross(axis1, axis2));
        //outAxis3 = axis3;
        float3x3 outRotMat = rotationMatrix(axis3, addangle);
        inVertex = float4(mul(inVertex.xyz - float3(0,0.05,0), outRotMat) + float3(0,0.05,0), 1);
        inNormal = mul(inNormal.xyz, outRotMat);
        inTangent.xyz = mul(inTangent.xyz, outRotMat);
    }
    //#else
    /*
if (_Frilling) {
        EXT = 0.1;
        inVertex.y *= 0.7;
        inNormal = normalize(inNormal / float3(1,.7,1));
} else {
        EXT = 0.03;
        inVertex.y *= 0.8;
        inNormal = normalize(inNormal / float3(1,.8,1));
}
*/
    //inVertex.xyz *= float3(0.85,0.7,0.85) + float3(0.15, 0.3, 0.15) * abs(cameraVec3d.y);
    //#endif
}

void vertModifierOuter(inout appdata_full v) {
	vertModifier(v);
    positionModHairHack(v.vertex, v.normal, v.tangent);
	float2 uvx = v.texcoord.xy;
	positionModClassic(v.vertex, v.normal, v.tangent, uvx, 0..xxxx, 0);
}



		void surf (Input IN, inout SurfaceOutputStandard o) {
			// Albedo comes from a texture tinted by color
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			o.Albedo = c.rgb;
			// Metallic and smoothness come from slider variables
			o.Metallic = _Metallic;
			o.Smoothness = _Glossiness;
			o.Alpha = c.a;
			clip(c.a - 0.1);
		}
		ENDCG

/*
		// ------------------------------------------------------------------
		//  Shadow rendering pass
		Pass {
			Name "ShadowCaster"
			Tags { "LightMode" = "ShadowCaster" }

			ZWrite On ZTest LEqual

			CGPROGRAM
			#pragma target 3.0

			// -------------------------------------


			#pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
			#pragma shader_feature _METALLICGLOSSMAP
			#pragma shader_feature _PARALLAXMAP
			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing

			#pragma vertex vertShadowCaster
			#pragma fragment fragShadowCaster

			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#include "UnityInstancing.cginc"
			#include "UnityStandardConfig.cginc"
			#include "UnityStandardUtils.cginc"
			#define _BoneBindTexture _LyumaBoneTexture
			#define _BoneBindTexture_TexelSize _LyumaBoneTexture_TexelSize
			#include "NewShaderSkin.cginc"

			#if (defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)) && defined(UNITY_USE_DITHER_MASK_FOR_ALPHABLENDED_SHADOWS)
				#define UNITY_STANDARD_USE_DITHER_MASK 1
			#endif

			// Need to output UVs in shadow caster, since we need to sample texture and do clip/dithering based on it
			#if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
			#define UNITY_STANDARD_USE_SHADOW_UVS 1
			#endif

			// Has a non-empty shadow caster output struct (it's an error to have empty structs on some platforms...)
			#if !defined(V2F_SHADOW_CASTER_NOPOS_IS_EMPTY) || defined(UNITY_STANDARD_USE_SHADOW_UVS)
			#define UNITY_STANDARD_USE_SHADOW_OUTPUT_STRUCT 1
			#endif


			half4       _Color;
			half        _Cutoff;
			sampler2D   _MainTex;
			float4      _MainTex_ST;
			#ifdef UNITY_STANDARD_USE_DITHER_MASK
			sampler3D   _DitherMaskLOD;
			#endif

			// Handle PremultipliedAlpha from Fade or Transparent shading mode
			half4       _SpecColor;
			half        _Metallic;
			#ifdef _SPECGLOSSMAP
			sampler2D   _SpecGlossMap;
			#endif
			#ifdef _METALLICGLOSSMAP
			sampler2D   _MetallicGlossMap;
			#endif

			//struct VertexInput
			//{
			//	float4 vertex   : POSITION;
			//	float3 normal   : NORMAL;
			//	float2 uv0      : TEXCOORD0;
			//	UNITY_VERTEX_INPUT_INSTANCE_ID
			//};

			struct VertexOutputShadowCaster
			{
				V2F_SHADOW_CASTER_NOPOS
				float2 tex : TEXCOORD1;
				float3 posWorld : TEXCOORD2;
				float4 pos : SV_POSITION;
			};

			// We have to do these dances of outputting SV_POSITION separately from the vertex shader,
			// and inputting VPOS in the pixel shader, since they both map to "POSITION" semantic on
			// some platforms, and then things don't go well.

			float _DisableShadow;

			VertexOutputShadowCaster vertShadowCaster (appdata_full v)
			{
				VertexOutputShadowCaster o = (VertexOutputShadowCaster)0;
				UNITY_SETUP_INSTANCE_ID(v);
				vertModifier(v);
				TRANSFER_SHADOW_CASTER_NOPOS(o,o.pos)
				o.tex = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
				o.posWorld = mul(unity_ObjectToWorld,float4(v.vertex.xyz, 1.0)).xyz;
				if (_DisableShadow > 0) {
					o.pos = float4(1,1,1,1);
				}
				return o;
			}

			half4 fragShadowCaster (
				//UNITY_POSITION(vpos),
				VertexOutputShadowCaster i
			) : SV_Target
			{
				clip (-_DisableShadow);
				half alpha = tex2D(_MainTex, i.tex).a * _Color.a;
				#if defined(_ALPHATEST_ON)
					clip (alpha - _Cutoff);
				#endif
				SHADOW_CASTER_FRAGMENT(i)
			}

			ENDCG
		}
		*/
	}
}
