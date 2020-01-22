#ifndef SHADER_SKIN_BASE_CGINC__
#define SHADER_SKIN_BASE_CGINC__
#include "HLSLSupport.cginc"
#include "UnityCG.cginc"

#if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD) || defined(UNITY_PASS_SHADOWCASTER)
#define PRECISE precise
#define TEX2DHALF Texture2D<half4>
#define TEXLOAD(tex, uvcoord) tex.Load(uvcoord)
#else
#define PRECISE
#define TEX2DHALF float4
#define TEXLOAD(tex, uvcoord) half4(1,0,1,1)
#endif

//////////////////// FROM MERLIN //////////////
// Packing/unpacking routines for saving integers to R16G16B16A16_FLOAT textures
// Heavily based off of https://github.com/apitrace/dxsdk/blob/master/Include/d3dx_dxgiformatconvert.inl
// For some reason the last 2 bits get stomped so we'll only allow uint14 for now :(
float uint14ToFloat(uint input)
{
    PRECISE float output = (f16tof32((input & 0x00003fff)));
    return output;
}

uint floatToUint14(PRECISE float input)
{
    uint output = (f32tof16(input)) & 0x00003fff;
    return output;
}

// Encodes a 32 bit uint into 3 half precision floats
float3 uintToHalf3(uint input)
{
    PRECISE float3 output = float3(uint14ToFloat(input), uint14ToFloat(input >> 14), uint14ToFloat((input >> 28) & 0x0000000f));
    return output;
}

uint half3ToUint(PRECISE float3 input)
{
    return floatToUint14(input.x) | (floatToUint14(input.y) << 14) | ((floatToUint14(input.z) & 0x0000000f) << 28);
}

float4x4 CreateMatrixFromCols(float4 c0, float4 c1, float4 c2, float4 c3) {
    return float4x4(c0.x, c1.x, c2.x, c3.x,
                    c0.y, c1.y, c2.y, c3.y,
                    c0.z, c1.z, c2.z, c3.z,
                    c0.w, c1.w, c2.w, c3.w);
}
float4x4 CreateMatrixFromRows(float4 r0, float4 r1, float4 r2, float4 r3) {
    return float4x4(r0, r1, r2, r3);
}
int3 genLoadTexCoord(int x, int y) {
    return int3(x, y, 0);
}

///////////////////////////////////////////////////

struct boneblend_VertexInput {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    half4 color : COLOR; // name in ASCII
    float4 texcoord : TEXCOORD0;
};
struct boneblend_v2f {
    float4 texcoord : TEXCOORD0;
	float3 zaxis : TEXCOORD1;
	float3 xaxis : TEXCOORD2;
	float3 vertex : TEXCOORD3;
    half4 color : TEXCOORD4;
	float4 pos : SV_POSITION;
};

#define COLORMASK_ALPHA_ONLY 1
#define COLORMASK_RGB 14
#define COLORMASK_RGBA 15
//uniform float _ColorMask;
static float _ColorMask = COLORMASK_RGBA;

uniform float4 _BoneBindTexture_TexelSize;
TEX2DHALF _BoneBindTexture;
TEX2DHALF _MeshTex;

float2 pixelToUV(float2 pixelCoordinate, float2 offset) {
    //return (floor(pixelCoordinate) + offset) / _ScreenParams.xy;
    float2 correctedTexelSize = _BoneBindTexture_TexelSize.zw;
    if (correctedTexelSize.x / _ScreenParams.x > 1.9) {
        correctedTexelSize.x *= 0.5;
    }
    return (floor(pixelCoordinate) + offset) / correctedTexelSize;
    //return float2(ret.x, 1.0 - ret.y);
}

boneblend_v2f newbonedump_vert(boneblend_VertexInput v) {
    boneblend_v2f o = (boneblend_v2f)0;
	o.zaxis = 0.5  + 0.5 * normalize(v.normal);
	o.xaxis = 0.5  + 0.5 * normalize(v.tangent.xyz);
    o.vertex = v.vertex.xyz;
    o.color = v.color;
    o.texcoord = v.texcoord;
    float2 baseUV = float2(0,0);
    float2 dims = float2(0,0);
    float boneId = v.texcoord.w;
    float xheight = _ColorMask == COLORMASK_ALPHA_ONLY ? 15 :
            (_ColorMask == COLORMASK_RGB ? 6 : 5);
            //(_ColorMask == COLORMASK_RGB ? 5 : 4);
    if (v.texcoord.z >= 1. && v.texcoord.w == 0.) {
        // root position
        baseUV = float2(0, 1);
        dims = float2(1,xheight);
        o.zaxis = mul((float3x3)unity_ObjectToWorld, float3(0,0,1));
        o.xaxis = mul((float3x3)unity_ObjectToWorld, float3(1,0,0));
        o.vertex = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
    } else if (v.texcoord.z >= 1.) {
        // blend shape
        o.xaxis = v.vertex.xxx;
        o.texcoord.xy += float2(0, 16);
        baseUV = float2(v.texcoord.w, 0);
        dims = float2(1,1);
    } else if (v.texcoord.w >= 1.) {
        // bone position
        baseUV = float2(v.texcoord.w, 1);
        dims = float2(1,xheight);
    }
    o.texcoord.xy *= dims;
#if UNITY_UV_STARTS_AT_TOP
    float2 uvflip = float2(1., -1.);
#else
    float2 uvflip = float2(1., 1.);
#endif
    o.pos = float4(uvflip*(pixelToUV(baseUV, v.texcoord.xy * dims) * 2. - float2(1.,1.)), 0., 1.);
    return o;
}

float4 newbonedump_frag(boneblend_v2f i) : SV_Target {
	uint idx = (uint)floor(i.texcoord.y);
    float3 encodedPosX = uintToHalf3(asuint(i.vertex.x));
    float3 encodedPosY = uintToHalf3(asuint(i.vertex.y));
    float3 encodedPosZ = uintToHalf3(asuint(i.vertex.z));
	float4 ret = .0.xxxx;
	switch (idx) {
	case 0:
		ret.rgb = i.xaxis.xyz;
        ret.a = encodedPosX.z;
		break;
	case 1:
		ret.rgb = i.zaxis.xyz;
        ret.a = encodedPosY.z;
		break;
	case 2:
        ret.rg = encodedPosX.xy;
		ret.ba = encodedPosZ.yz;
		break;
	case 3:
		ret.rg = encodedPosY.xy;
        ret.b = encodedPosZ.x;
        ret.a = encodedPosX.y;
		break;
	case 4:
	    ret.r = encodedPosX.z;
        ret.g = encodedPosY.z;
        ret.b = encodedPosZ.z;
        ret.a = encodedPosY.y;
        ret = (_ColorMask == COLORMASK_RGBA ? i.color : ret);
		break;
    case 5:
        ret.rgb = i.color.rgb;
        ret.a = _ColorMask == COLORMASK_ALPHA_ONLY ? encodedPosZ.y : i.color.a;
        break;
    case 6:
        ret.a = encodedPosX.x;
        break;
    case 7:
        ret.a = encodedPosY.x;
        break;
    case 8:
        ret.a = encodedPosZ.x;
        break;
    case 9:
        ret.a = i.xaxis.x;
        break;
    case 10:
        ret.a = i.xaxis.y;
        break;
    case 11:
        ret.a = i.xaxis.z;
        break;
    case 12:
        ret.a = i.zaxis.x;
        break;
    case 13:
        ret.a = i.zaxis.y;
        break;
    case 14:
        ret.a = i.zaxis.z;
        break;
    case 16:
        ret.rgba = i.xaxis.xxxx;
        break;
	}
    return ret;
}

//static bool realIsInMirror = 1;
#ifdef USING_STEREO_MATRICES
static bool realIsInMirror = 0;
#else
static bool realIsInMirror = (unity_CameraProjection[2][0] != 0.f || unity_CameraProjection[2][1] != 0.f);
#endif

float3x3 rotationMatrix(float3 axis, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    float oc = 1.0 - c;
    
    return float3x3(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s,
                oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,
                oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c);
}

float3 positionFromBoneData(float4 posxzRead, float4 posyzRead, float3 posextraRead) {
    float3 encodedPosX = float3(posxzRead.rg, posextraRead.r);
    float3 encodedPosY = float3(posyzRead.rg, posextraRead.g);
    float3 encodedPosZ = float3(posyzRead.b, posxzRead.b, posextraRead.b);
    float3 position = float3(
        asfloat(half3ToUint(encodedPosX)),
        asfloat(half3ToUint(encodedPosY)),
        asfloat(half3ToUint(encodedPosZ)));
    return position;
}

uniform float _xtracursed;
float4x4 readBindTransform(int boneIndex, int yOffset) {
    float oldBoneIndex = boneIndex;
    if (realIsInMirror) {
        if (boneIndex <= 27 && boneIndex >= 24) { // larm
            boneIndex += 41;
        } else if (boneIndex >= 28 && boneIndex <= 43) { // lfinger
            boneIndex = 68;
        } else if (boneIndex <= 47 && boneIndex >= 44) { // rarm
            boneIndex += 25;
        } else if (boneIndex >= 48 && boneIndex <= 62) { // rfinger
            boneIndex = 72;
        } else if (boneIndex >= 65 && boneIndex <= 68) { // lleg
            boneIndex -= 41;
        } else if (boneIndex >= 69 && boneIndex <= 72) { // rleg
            boneIndex -= 25;
        }
    }
    boneIndex = (int)lerp(oldBoneIndex, (float)boneIndex, _xtracursed);
    boneIndex = boneIndex + 1;
    //int adj = sin(_Time.y) < 0.5 ? 1 : -1;//float adj = .5;
    int adj = 0;//float adj = -0.5; // sin(_Time.y) < 0.5 ? 0.5 : -0.5;//1. + lerp(2 * sin(.3*_Time.y), 1., sin(_Time.x) < 0);
    float4 xaxisRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex-adj, yOffset * 5 + 1)));
    float4 zaxisRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex-adj, yOffset * 5 + 2)));
    float4 posxzRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex-adj, yOffset * 5 + 3)));
    float4 posyzRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex-adj, yOffset * 5 + 4)));

    float4 position = float4(positionFromBoneData(posxzRead, posyzRead,
        float3(xaxisRead.a, zaxisRead.a, posxzRead.a)), 1.0);

    // Version for no alpha channel:
    //float4 posextraRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex-adj, yOffset * 5 + 5)));
    //float4 position = float4(positionFromBoneData(posxzRead, posyzRead, posextraRead.rgb), 1.0);

    float3 xaxis = normalize(2 * (xaxisRead.rgb - .5.xxx));
    float3 zaxis = normalize(2 * (zaxisRead.rgb - .5.xxx));
    float3 yaxis = cross(zaxis, xaxis);

    float4 boneName = TEXLOAD(_BoneBindTexture, genLoadTexCoord(boneIndex-adj, yOffset * 5 + 5));
    if (all(boneName == float4('H', 'a', 't', 0))) {
        xaxis = yaxis = zaxis = .0.xxx;
    }
    if (realIsInMirror) {
        yaxis.xy = -yaxis.xy;
        xaxis.xy = -xaxis.xy;
        position.xy *= -1;
        if ((all(boneName == float4('H', 'e', 'a', 'd')) && _xtracursed > 0.1)||(boneIndex==23&&_xtracursed < 0.1)) {
            position.y += 0.06;
            yaxis *= -1;
            xaxis *= -1;
        }
        if (all(boneName == float4('S', 'k', 'i', 'r'))) {
            position.y += 0.06 - (1 - _xtracursed) * 0.1;
            yaxis *= 2 *(_xtracursed - 0.5);
            xaxis *= 2 *(_xtracursed <= 0.5 ? -0.5 : 0.5);
        }
        if ( (boneIndex >= 25 && boneIndex <= 63) && _xtracursed > 0.95) { // arms&fingers
            position.y -= 0.2;
            position.x *= -2;
            xaxis *= -1;
            yaxis *= -1;
        }
        /*if (boneIndex >= 66 && boneIndex <= 73) {
            position.y *= 0.5;
        }*/
        if (boneIndex >= 9 && boneIndex <= 23) { // hair
            position.z += 0.09;//0.0;//15;
            position.z *= 1.3;
            position.y += 0.05;
        }
    }
#if 0 //def FACTOR
    if (FACTOR < .005) {
        if (( boneIndex >= 20 && boneIndex <= 21)) {
            float3 eyeVec = normalize(mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz);// - position);
            eyeVec.x = lerp(float3(-0.5,0,-0.8), float3(-4,0,1)*eyeVec, saturate(5*abs(eyeVec.x))).x;
            zaxis = normalize(lerp(zaxis, eyeVec, 0.5));
            xaxis = normalize(cross(yaxis, zaxis));
            yaxis = normalize(cross(zaxis, xaxis));
        }
        if (( boneIndex >= 15 && boneIndex <= 19)) {
            float3 eyeVec = normalize(mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz);// - position);
            zaxis = normalize(eyeVec);
            xaxis = normalize(cross(yaxis, zaxis));
            yaxis = normalize(cross(zaxis, xaxis));
        }
        /*if (boneIndex >= 5 && boneIndex <= 21) {
            float3 relcamera = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1)).xyz;
            //position.xyz += normalize(relcamera) * pow((0.1 / length(relcamera)), 2);
        }*/
        if (boneIndex == 68) {
            xaxisRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+46-adj, yOffset * 5 + 1)));
            zaxisRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+46-adj, yOffset * 5 + 2)));
            posxzRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+46-adj, yOffset * 5 + 3)));
            posyzRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+46-adj, yOffset * 5 + 4)));
            float3 leftPosition = positionFromBoneData(posxzRead, posyzRead, float3(xaxisRead.a, zaxisRead.a, posxzRead.a));
            xaxisRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+27-adj, yOffset * 5 + 1)));
            zaxisRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+27-adj, yOffset * 5 + 2)));
            posxzRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+27-adj, yOffset * 5 + 3)));
            posyzRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(1+27-adj, yOffset * 5 + 4)));
            float3 rightPosition = positionFromBoneData(posxzRead, posyzRead, float3(xaxisRead.a, zaxisRead.a, posxzRead.a));
            
            float handDistance = distance(leftPosition, rightPosition);
            float lerpFactor = clamp(handDistance - 0.3, 0, 1);
            if (lerpFactor > 0.1) {

                float3x3 rotmat = rotationMatrix(zaxis, lerpFactor * sin((handDistance > 0.8 ? 12 : 4) * _Time.g));
                yaxis = mul(rotmat, yaxis);
                xaxis = mul(rotmat, xaxis);
            } else {
                float3x3 rotmat = rotationMatrix(xaxis, 1.0 * sin(4 * _Time.g));
                yaxis = mul(rotmat, yaxis);
                zaxis = mul(rotmat, zaxis);
            }
        }
    }
#endif

    //return CreateMatrixFromCols(float4(xaxis.x, yaxis.x, zaxis.x, 0), float4(xaxis.y, yaxis.y, zaxis.y, 0), float4(xaxis.z, yaxis.z, zaxis.z, 0), position);
    return CreateMatrixFromCols(float4(xaxis, 0), float4(yaxis, 0), float4(zaxis, 0), position);
}

half4x4 readBoneMapMatrix(int boneIndex, int yOffset) {
    //return float4x4(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
    float4 lastColumn = TEXLOAD(_MeshTex, int3(boneIndex, yOffset + 3, 0));
    if (dot(lastColumn, lastColumn) <= 1.0) {
        return float4x4(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
    }
    return CreateMatrixFromCols(
        TEXLOAD(_MeshTex, int3(boneIndex, yOffset + 0, 0)),
        TEXLOAD(_MeshTex, int3(boneIndex, yOffset + 1, 0)),
        TEXLOAD(_MeshTex, int3(boneIndex, yOffset + 2, 0)),
        lastColumn);
}

uint boneIndexMap(uint boneIndex) {
    if (realIsInMirror && _xtracursed > 0.1) {
        if (boneIndex >= 27 && boneIndex <= 43) {
            boneIndex = 26;
        } else if (boneIndex >= 47 && boneIndex <= 62) {
            boneIndex = 46;
        } else if (boneIndex == 67 || boneIndex == 68) {
            boneIndex = 66;
        } else if (boneIndex == 71 || boneIndex == 72) {
            boneIndex = 70;
        } else if (boneIndex == 5 || boneIndex == 6 || (boneIndex >= 7 && boneIndex <= 7)) {// + (1-_xtracursed) * 15)) {
            boneIndex = 4;
        }
    } else if (realIsInMirror && boneIndex >= 8 && boneIndex <= 7 + (1-_xtracursed) * 15) {
        boneIndex = 22;
    }
    return boneIndex;
}

///////////////////////////x/////////////////////


void vertModifier(inout appdata_full v) {
    float4x4 boneTransform;
    uint4 boneIndices = (uint4)floor(v.texcoord2);
    float4 boneWeights = (float4)(v.texcoord3 + float4(1.e-10, 0, 0, 0));
    boneWeights /= dot(boneWeights, 1..xxxx);
    //if (abs(dot(1..xxxx, v.uv4) - 1.0) < 0.1) {
        boneTransform = (float4x4)0;
        float scale = 1.0;
        uint yoffset = 0;
        boneIndices = uint4(boneIndexMap(boneIndices.x),boneIndexMap(boneIndices.y),boneIndexMap(boneIndices.z),boneIndexMap(boneIndices.w));
        boneTransform += boneWeights.x * mul(readBindTransform(boneIndices.x, yoffset), readBoneMapMatrix(boneIndices.x, yoffset));
        boneTransform += boneWeights.y * mul(readBindTransform(boneIndices.y, yoffset), readBoneMapMatrix(boneIndices.y, yoffset));
        boneTransform += boneWeights.z * mul(readBindTransform(boneIndices.z, yoffset), readBoneMapMatrix(boneIndices.z, yoffset));
        boneTransform += boneWeights.w * mul(readBindTransform(boneIndices.w, yoffset), readBoneMapMatrix(boneIndices.w, yoffset));
    //} else {
    //    boneTransform = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
    //}
    v.vertex = mul(boneTransform, float4(v.vertex.xyz, 1.));
    v.normal = mul((float3x3)boneTransform, v.normal.xyz);
    v.tangent = float4(mul((float3x3)boneTransform, v.tangent.xyz), v.tangent.w);
}
#endif
