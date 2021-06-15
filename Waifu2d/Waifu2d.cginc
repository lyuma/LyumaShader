// Code to flatten an object to 2d. Original by Lyuma

// Mathematicians beware! This file is littered with numerical stability hacks.
// Be prepared to see lots of sign(X) * max(0.001, abs(X)) all over the place to avoid division by 0.

#ifndef LYUMASHADER_WAIFU2D_CGINC_
#define LYUMASHADER_WAIFU2D_CGINC_
#include "UnityCG.cginc"

#ifndef NO_UNIFORMS
uniform float _2d_coef;
uniform float _facing_coef;
uniform float _lock2daxis_coef;
uniform float _zcorrect_coef;
#endif

float nonzeroify(float inp) {
    return (step(0.0, inp) * 2. - 1.) * max(0.000001, abs(inp));
}


#ifdef USING_STEREO_MATRICES
static float3 realCameraPos = 0.5 * (unity_StereoWorldSpaceCameraPos[0] +  unity_StereoWorldSpaceCameraPos[1]); //(mul(mvMat, float4(0., 0., 0., 1.))).xyz; //mvMat._14_24_34_44;
static float3x3 cameraToWorld = 0.5 * (unity_StereoCameraToWorld[0] +  unity_StereoCameraToWorld[1]); //(mul(mvMat, float4(0., 0., 0., 1.))).xyz; //mvMat._14_24_34_44;
static float3 approxEyeDir = normalize((0.5 * mul(unity_StereoMatrixInvV[0], float4(0., 0., 1., 0.)) +
		0.5 * mul(unity_StereoMatrixInvV[1], float4(0., 0., 1., 0.))).xyz);
#else
static float3 realCameraPos = _WorldSpaceCameraPos; //(mul(mvMat, float4(0., 0., 0., 1.))).xyz; //mvMat._14_24_34_44;
static float3x3 cameraToWorld = (float3x3)unity_CameraToWorld;
static float3 approxEyeDir = normalize(mul(UNITY_MATRIX_I_V, float4(0., 0., 1., 0.)).xyz);
#endif
static float3 objectPos = mul(unity_ObjectToWorld, float4(0., 0., 0., 1.)).xyz;

static float3 cameraPosInObjectSpace = mul(unity_WorldToObject, float4(realCameraPos, 1)).xyz;
static float3 targetCameraPosFacingVec = float3(sin(_facing_coef * 1.570796), 0, cos(_facing_coef * 1.570796));
static float3 targetCameraPos = mul(unity_ObjectToWorld, float4(targetCameraPosFacingVec * sign(dot(cameraPosInObjectSpace,targetCameraPosFacingVec)) * length(cameraPosInObjectSpace.xyz), 1)).xyz;
// _plock_coef

static float3 cameraPos = lerp(realCameraPos, targetCameraPos, min(_2d_coef * 4, 1) * _lock2daxis_coef);

static float3 cameraToObj = (cameraPos - objectPos); // + float3(0., 0., 0.0001);
//static float3 cameraToObj = float3(cameraToObjX.x, 0., nonzeroify(cameraToObjX.z));
static float2 cameraToObj2D = normalize(cameraToObj.xz); // FIXME: was normalize(cameraToObj).xz WHY?

#if defined(VR_ONLY_2D) && !defined(USING_STEREO_MATRICES)
static float desired_waifu_coef = 0.0;
#else
static float desired_waifu_coef = min(0.995, max(0.0, _2d_coef));
#endif
static float waifu_coef = desired_waifu_coef;

static float3 yAxis = float3(0,1,0);
static float3 xAxis = float3(cameraToObj2D.y,0,cameraToObj2D.x);
static float3 zAxis = float3(-cameraToObj2D.x,0,cameraToObj2D.y);

// world in camera space: [xAxis yAxis zAxis cameraPos]
static float4x4 myVMat = float4x4(
		xAxis.x, xAxis.y, xAxis.z, objectPos.x,
		yAxis.x, yAxis.y, yAxis.z, objectPos.y,
		zAxis.x, zAxis.y, zAxis.z, objectPos.z,
		0., 0., 0., 1.);
static float3x3 myInvVMat3x3 = float3x3(
		xAxis.x, yAxis.x, zAxis.x,
		xAxis.y, yAxis.y, zAxis.y,
		xAxis.z, yAxis.z, zAxis.z);

static float3 invObjectPos = mul((float3x3)myInvVMat3x3, -objectPos);
static float4x4 myInvVMat = float4x4(
		xAxis.x, yAxis.x, zAxis.x, invObjectPos.x,
		xAxis.y, yAxis.y, zAxis.y, invObjectPos.y,
		xAxis.z, yAxis.z, zAxis.z, invObjectPos.z,
		0., 0., 0., 1.);

//myInvVMat = inverse(myVMat);
static float3 planeNormal = cross(xAxis, yAxis);
static float approxAngle = atan2(planeNormal.z, planeNormal.x);
static float discreteAngle = floor(approxAngle * 2.) / 2.;
static float angleCorr = discreteAngle - approxAngle;

static float3 approxEyeDirInObjectSpace = mul(unity_WorldToObject, float4(approxEyeDir, 0)).xyz;
static float3 targetApproxEyeDir = mul(unity_ObjectToWorld, float4(0, 0, length(approxEyeDirInObjectSpace.xyz), 0)).xyz;
static float3 approxCameraDir = float4((cameraPos.xz - objectPos.xz), 0., 1.).xzy;
// was approxEyeDir not approxCameraDir but this changes rotation as you rotate...
static float3 facingApproxEyeDir = normalize(lerp(approxCameraDir, targetApproxEyeDir, min(_lock2daxis_coef, _2d_coef)));

static float2 eyeFacing = lerp(float2(1,0), facingApproxEyeDir.xz, _facing_coef);

float4 waifu_computeWorldFlatWorldPos(float4 objToWorld) {
    float3 oPos = UnityWorldToViewPos(objToWorld.xyz).xyz;
    float4 unprojectedPos = mul(myInvVMat, objToWorld);
    float4 semiProjectedPos = float4(unprojectedPos.xyz, 1.);
    semiProjectedPos.z = 0.;
    float4 actualObjectPos = mul(myVMat, semiProjectedPos);
    actualObjectPos.y = objToWorld.y; /// MAD HAX - doesn't fix the problem :-p
    return actualObjectPos;
}

// inVertex: original model space vertex;
float4 waifu_computeVertexWorldPos(float4 inVertex) {
	// START CRAZY PER VERTEX
    float4 objToWorld = mul(unity_ObjectToWorld, inVertex);
    float4 actualObjectPos = waifu_computeWorldFlatWorldPos(objToWorld);
    return waifu_coef * actualObjectPos + (1. - waifu_coef) * objToWorld;
}

float4 waifu_computeVertexLocalPos(float4 inVertex) {
    return mul(unity_WorldToObject, waifu_computeVertexWorldPos(inVertex));
}

float4 waifu_projectVertex2(float4 vertexWorldPos, float4 origPos) {
    float4 oPos = UnityObjectToClipPos(origPos);
    if (waifu_coef <= 1.0e-6) {
        return oPos;
    }
    float4 newViewPos = mul(UNITY_MATRIX_V, vertexWorldPos);
    float4 newPos = mul(UNITY_MATRIX_P, newViewPos);
    newPos.z = lerp(sign(oPos.w * oPos.z * newPos.w) * max(0.00001, abs(oPos.z)) * max(0.00001, abs(newPos.w)) / max(0.00001, abs(oPos.w)), newPos.z, _zcorrect_coef);
    return newPos;
    // END CRAZY PER VERTEX
}

// Helper function.
// inVertex: original model space vertex;
// oPos: result of UnityObjectToClipPos(vertexWorldPos)

float4 waifu_projectVertexWorldPos(float4 inVertex) {
	return waifu_projectVertex2(waifu_computeVertexWorldPos(inVertex), inVertex);
}

float4 waifu_projectVertexWorldPos(float3 inVertex) {
	return waifu_projectVertex2(waifu_computeVertexWorldPos(float4(inVertex, 1)), float4(inVertex, 1));
}

float4 waifu_projectVertexFromWorldPos(float3 inVertex) {
    inVertex = mul(unity_WorldToObject, float4(inVertex, 1)).xyz;
	return waifu_projectVertex2(waifu_computeVertexWorldPos(float4(inVertex, 1)), float4(inVertex, 1));
}

#ifdef LYUMA2D_HOTPATCH
#define UnityObjectToClipPos waifu_projectVertexWorldPos
#define UnityWorldToClipPos waifu_projectVertexFromWorldPos
#undef TRANSFER_SHADOW_CASTER_NOPOS_LEGACY
#undef TRANSFER_SHADOW_CASTER_NOPOS

float4 waifu_ClipSpaceShadowCasterPos(float4 v, float3 n) {
    return UnityClipSpaceShadowCasterPos(waifu_computeVertexLocalPos(v), n);
}
float4 waifu_ClipSpaceShadowCasterPos(float3 v, float3 n) {
    return UnityClipSpaceShadowCasterPos(waifu_computeVertexLocalPos(float4(v, 1.0)), n);
}
#define UnityClipSpaceShadowCasterPos waifu_ClipSpaceShadowCasterPos

#if defined(SHADOWS_CUBE) && !defined(SHADOWS_CUBE_IN_DEPTH_TEX)
    #define TRANSFER_SHADOW_CASTER_NOPOS_LEGACY(o,opos) float4 actualWorldPos = waifu_computeVertexWorldPos(v.vertex); o.vec = actualWorldPos.xyz - _LightPositionRange.xyz; opos = UnityWorldToClipPos(actualWorldPos);
    #define TRANSFER_SHADOW_CASTER_NOPOS(o,opos) TRANSFER_SHADOW_CASTER_NOPOS_LEGACY(o,opos)
#else
    #define TRANSFER_SHADOW_CASTER_NOPOS_LEGACY(o,opos) \
        opos = UnityObjectToClipPos(waifu_computeVertexLocalPos(v.vertex)); \
        opos = UnityApplyLinearShadowBias(opos);
    #define TRANSFER_SHADOW_CASTER_NOPOS(o,opos) \
        opos = waifu_ClipSpaceShadowCasterPos(v.vertex, v.normal); \
        opos = UnityApplyLinearShadowBias(opos);
#endif
#endif

#endif
