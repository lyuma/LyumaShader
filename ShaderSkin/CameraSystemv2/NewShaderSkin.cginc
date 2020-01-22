#ifndef SHADER_SKIN_BASE_CGINC__
#define SHADER_SKIN_BASE_CGINC__
#include "HLSLSupport.cginc"
#include "UnityCG.cginc"

#pragma warning (disable : 3206)
//#if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD) || defined(UNITY_PASS_SHADOWCASTER)
#if defined(SHADER_STAGE_VERTEX) || defined(SHADER_STAGE_FRAGMENT) || defined(SHADER_STAGE_DOMAIN) || defined(SHADER_STAGE_HULL) || defined(SHADER_STAGE_GEOMETRY)
#define TEX2DHALF Texture2D<half4>
#define TEXLOAD(tex, uvcoord) tex.Load(uvcoord)
#else
#define precise
#define centroid
#define TEX2DHALF float4
#define TEXLOAD(tex, uvcoord) half4(1,0,1,1)
#endif

float3 encodeLossyFloat(float inputFloat, inout uint remainder5Bits) {
    precise uint input = asuint(inputFloat);
    // Skip 3 bits of exponent in exchange for 3 bits of mantessa: fails on values equal to 0
    if ((input & 0x7f800000) < 0x38000000) {
        input = 0;
    }
    //if ((input & 0x7f800000) > 0x47800000) {
    //    input = 0x47ffffff;
    //}
    precise uint newRemainder = (((input & 0x80000000) >> 27) | ((input & 0x03800) >> 11));
    precise float in1 = (float)(0x0c | ((input & 0x40000000) >> 19) | ((input & 0x07e00000) >> 16) | ((input & 0x180) >> 7)); // weird bit overlap here.... this seems wrong!!!
    precise float in2 = (float)(0x0c | ((input & 0x001fc000) >> 9) | ((input & 0x0600) >> 9)); // to here (0x7 mask instead of 0xf which we use in decode??)
    precise float in3 = (float)(0x0c | ((remainder5Bits & 0x10) << 7) | ((newRemainder & 0x10) << 6) | ((newRemainder & 0x7) << 6) | ((remainder5Bits & 0x4) << 3) | (remainder5Bits & 0x3));
    remainder5Bits = newRemainder;
    precise float3 ret = (float3((floor(in1)) / 4096., (floor(in2)) / 4096., (floor(in3)) / 4096.));
    return ret;
}
uint2 decodeLossyFloatShared(float toDecode) {
    precise uint in1 = (uint)(toDecode * 4096.);
    return uint2(((in1&0x400) >>6) | (in1 & 0x1c0) >> 6, ((in1&0x800) >>7) | ((in1 & 0x20) >> 3) | (in1 & 0x3));
}
float decodeLossyFloat(float2 toDecode, uint sharedDecode) {
    precise uint in1 = (uint)(toDecode.x * 4096.);
    precise uint in2 = (uint)(toDecode.y * 4096.);
    precise uint in3 = sharedDecode;
    precise uint outbits = (((in3 & 0x10) << 27) | ((in1 & 0x7e0) << 16) | ((in2 & 0x00fe0) << 9) | ((in3 & 0x7) << 11) | ((in2 & 0x3) << 9) | ((in1 & 0x3) << 7));
    if ((outbits & 0x3fffffff) != 0) {
        outbits |= ((in1 & 0x800) != 0) ? 0x40000000 : 0x38000000;
    }
    return asfloat(outbits);
}

float4 matrix_to_quaternion(float3x3 m)
{
    float tr = m._m00 + m._m11 + m._m22;
    float4 q = float4(0, 0, 0, 0);

    if (tr > 0)
    {
        float s = sqrt(tr + 1.0) * 2; // S=4*qw 
        q.w = 0.25 * s;
        q.x = (m._m21 - m._m12) / s;
        q.y = (m._m02 - m._m20) / s;
        q.z = (m._m10 - m._m01) / s;
    }
    else if ((m._m00 > m._m11) && (m._m00 > m._m22))
    {
        float s = sqrt(1.0 + m._m00 - m._m11 - m._m22) * 2; // S=4*qx 
        q.w = (m._m21 - m._m12) / s;
        q.x = 0.25 * s;
        q.y = (m._m01 + m._m10) / s;
        q.z = (m._m02 + m._m20) / s;
    }
    else if (m._m11 > m._m22)
    {
        float s = sqrt(1.0 + m._m11 - m._m00 - m._m22) * 2; // S=4*qy
        q.w = (m._m02 - m._m20) / s;
        q.x = (m._m01 + m._m10) / s;
        q.y = 0.25 * s;
        q.z = (m._m12 + m._m21) / s;
    }
    else
    {
        float s = sqrt(1.0 + m._m22 - m._m00 - m._m11) * 2; // S=4*qz
        q.w = (m._m10 - m._m01) / s;
        q.x = (m._m02 + m._m20) / s;
        q.y = (m._m12 + m._m21) / s;
        q.z = 0.25 * s;
    }

    return q;
}

float3x3 quaternion_to_matrix(float4 quat)
{
    // https://gist.github.com/mattatz/86fff4b32d198d0928d0fa4ff32cf6fa
    float x = quat.x, y = quat.y, z = quat.z, w = quat.w;
    float x2 = x + x, y2 = y + y, z2 = z + z;
    float xx = x * x2, xy = x * y2, xz = x * z2;
    float yy = y * y2, yz = y * z2, zz = z * z2;
    float wx = w * x2, wy = w * y2, wz = w * z2;

    return float3x3(1.0 - (yy + zz), xy - wz, xz + wy,
        xy + wz, 1.0 - (xx + zz), yz - wx,
        xz - wy, yz + wx, 1.0 - (xx + yy));
}

// From glm
float3 quat_apply_vec_glmx(float4 q, float3 v) {
	float3 QuatVector = float3(q.x, q.y, q.z);
    float3 uv = cross(QuatVector, v);
    float3 uuv = cross(QuatVector, uv);
    return v + ((uv * q.w) + uuv) * 2;
}

float3 quat_apply_vec(float4 q, float3 v) {
    float3 u = q.xyz;
    float s = q.w;
    return 2.0f * dot(u, v) * u
          + (s*s - dot(u, u)) * v
          + 2.0f * s * cross(u, v);
}

////// FROM wavy code
float signpow(float base, float exp) {
    return sign(base) * pow(abs(base), exp);
}

float2 signpow(float2 base, float2 exp) {
    return sign(base) * pow(abs(base), exp);
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
//////////




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

//////
// Bone constants
#define HIPS_BONE 0
#define SPINE_BONE 1
#define CHEST_BONE 2
#define UPPERCHEST_BONE 3
#define NECK_BONE 4
#define HEAD_BONE 5
#define JAW_BONE 6

// Left/right
#define _EYE_BONE 10

#define _LEG_BONE 12
#define _KNEE_BONE 14
#define _FOOT_BONE 16
#define _TOES_BONE 18
#define _SHOULDER_BONE 20
#define _ARM_BONE 22
#define _ELBOW_BONE 24
#define _WRIST_BONE 26
#define _FINGER_THUMB_ 28
#define _FINGER_INDEX_ 34
#define _FINGER_MIDDLE_ 40
#define _FINGER_RING_ 46
#define _FINGER_LITTLE_ 52

#define _EAR_BONE 58
#define _BREAST_BONE 60
#define _WING_BONE 62

// Add to above bone constnats
#define LEFT 0
#define RIGHT 1

// Add to above finger constants
#define PROXIMAL_BONE 0
#define INTERMEDIATE_BONE 2
#define DISTAL_BONE 4

// Extra / virtual bones
#define HAIR_BONE 64
#define TAIL_BONE 65
#define SKIRT_BONE 66
#define FIRST_USER_BONE 72
#define ROOT_BONE_INDEX 7
#define PARENT_BONE_INDEX 8
#define MESH_BONE_INDEX 9


// For reading mesh data from the mesh bindpose texture
#define YOFFSET_BINDPOSE 0
#define YOFFSET_INVBINDPOSE 4
#define YOFFSET_RELBINDPOSE 8
#define YOFFSET_RELINVBINDPOSE 12
#define YOFFSET_PARENTS0 16
#define YOFFSET_LOCALGLOBALQPS 20
#define YOFFSET_LOCALQUAT 20
#define YOFFSET_LOCALPOSSCALE 21
#define YOFFSET_GLOBALQUAT 22
#define YOFFSET_GLOBALPOSSCALE 23
#define YOFFSET_BONETOROOT 24
#define YOFFSET_ROOTTOBONE 28

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
	float4 quat01 : TEXCOORD1;
	float3 vertex : TEXCOORD2;
	float4 pos : SV_POSITION;
};

TEX2DHALF _BoneBindTexture;
TEX2DHALF _MeshTex;

#ifdef USE_SCREEN_PARAMS
float2 pixelToUV(float2 pixelCoordinate, float2 offset) {
    //return (floor(pixelCoordinate) + offset) / _ScreenParams.xy;
    float2 correctedTexelSize = _ScreenParams.xy;
    return (floor(pixelCoordinate) + offset) / correctedTexelSize;
    //return float2(ret.x, 1.0 - ret.y);
}
#else
uniform float4 _BoneBindTexture_TexelSize;
float2 pixelToUV(float2 pixelCoordinate, float2 offset) {
    //return (floor(pixelCoordinate) + offset) / _ScreenParams.xy;
    float2 correctedTexelSize = _BoneBindTexture_TexelSize.zw;
    if (correctedTexelSize.x / _ScreenParams.x > 1.9) {
        correctedTexelSize.x *= 0.5;
    }
    return (floor(pixelCoordinate) + offset) / correctedTexelSize;
    //return float2(ret.x, 1.0 - ret.y);
}
#endif

boneblend_v2f newbonedump_vert(boneblend_VertexInput v) {
    boneblend_v2f o = (boneblend_v2f)0;
    o.vertex = v.vertex.xyz;
    o.texcoord = v.texcoord;
    float2 baseUV = float2(0,0);
    float2 dims = float2(0,0);
    float boneId = v.texcoord.w;
    float xheight = 3;
    if (v.texcoord.z >= 1. && v.texcoord.w >= 1.) {
        // blend shape
        o.quat01 = v.vertex.xxxx;//length(v.vertex).xxxx;//
        //o.texcoord.xy += float2(0, 16); // implement a special case 16: in _frag
        baseUV = float2(v.texcoord.w, 0);
        dims = float2(1,1);
    } else {
        float3x3 mat = float3x3(1,0,0,0,1,0,0,0,1);
        if (v.texcoord.z >= 1. && v.texcoord.w < 1.) {
            // root position
            mat = unity_ObjectToWorld;
            baseUV = float2(0, 1);
            dims = float2(1,xheight);
            o.vertex = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
        } else if (v.texcoord.z < 1. && v.texcoord.w >= 1. || v.texcoord.w <= -1) {
            // bone position
            float3 zaxis = normalize(v.normal);
            float3 xaxis = normalize(v.tangent.xyz);
            float3 yaxis = normalize(cross(zaxis, xaxis));
            mat = float3x3(xaxis, yaxis, zaxis);
            baseUV = float2(abs(v.texcoord.w), v.texcoord.w < 0 ? 4 : 1);
            dims = float2(1,xheight);
        }
        o.quat01 = 0.5 + 0.5 * matrix_to_quaternion(mat);
    }
    o.texcoord.xy *= dims;
#if UNITY_UV_STARTS_AT_TOP
    float2 uvflip = float2(1., -1.);
#else
    float2 uvflip = float2(1., 1.);
#endif
    o.pos = float4(uvflip*(pixelToUV(baseUV, v.texcoord.xy * dims) * 2. - float2(1.,1.)), 0., 1.);
#ifdef ONLY_ORTHO
    if (unity_CameraProjection._44 == 0){
        o.pos = float4(1,1,1,1);
    }
#endif
    return o;
}

float4 newbonedump_frag(boneblend_v2f i) : SV_Target {
	uint idx = (uint)floor(i.texcoord.y);
    uint yRemainder5Bits = 0;
    float2 encodedFloatY = encodeLossyFloat(i.vertex.y, yRemainder5Bits).xy;
    float3 encodedFloatXybits = encodeLossyFloat(i.vertex.x, yRemainder5Bits);
    yRemainder5Bits = 0;
    float3 encodedFloatZ = encodeLossyFloat(i.vertex.z, yRemainder5Bits);
	float4 ret = .0.xxxx;
	switch (idx) {
	case 0:
        ret = i.quat01;
		break;
	case 1:
        ret = float4(encodedFloatXybits.xy, encodedFloatY.x, encodedFloatXybits.z);
		break;
	case 2:
        ret = float4(encodedFloatZ.xy, encodedFloatY.y, encodedFloatZ.z);
		break;
	}
    return ret;
}

float3x3 rotationMatrix(float3 axis, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    float oc = 1.0 - c;
    
    return float3x3(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s,
                oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s,
                oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c);
}

float3 positionFromBoneData(float4 posxyRead, float4 posyzRead) {
    uint2 sharedXY = decodeLossyFloatShared(posxyRead.a);
    uint2 sharedZ = decodeLossyFloatShared(posyzRead.a);
    return float3(
        decodeLossyFloat(posxyRead.rg, sharedXY.x),
        decodeLossyFloat(float2(posxyRead.b, posyzRead.b), sharedXY.y),
        decodeLossyFloat(posyzRead.rg, sharedZ.x));
}

float readBlendShape(int blendIndex1base, int yOffset) {
    float4 blendshape = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(blendIndex1base, yOffset * 5)));
    return saturate(blendshape.r * 2);
}
float4 readBoneQuat(int boneIndex, int yOffset) {
    boneIndex = boneIndex + 1;
    float4 quatOut = (TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex, yOffset * 5 + 1))) - 0.5) * 2;
    //if (dot(quatOut, quatOut) > 2.0 || dot(quatOut, quatOut) < 0.5) {
    //    posOut = 0;
    //    quatOut = float4(0,0,0,1);
    //}
    if (dot(quatOut, quatOut) > 1.1 || dot(quatOut, quatOut) < 0.9) {
        //quatOut = float4(0,0,0,1);
    }
    quatOut = normalize(quatOut);
    return quatOut;
}
void readBoneQP(int boneIndex, int yOffset, out float4 quatOut, out float3 posOut, inout bool fail) {
    boneIndex = boneIndex + 1;
    quatOut = (TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex, yOffset * 5 + 1))) - 0.5) * 2;

    float4 posxyRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex, yOffset * 5 + 2)));
    float4 posyzRead = TEXLOAD(_BoneBindTexture, int3(genLoadTexCoord(boneIndex, yOffset * 5 + 3)));
    posOut = positionFromBoneData(posxyRead, posyzRead);
    //if (dot(quatOut, quatOut) > 2.0 || dot(quatOut, quatOut) < 0.5) {
    //    posOut = 0;
    //    quatOut = float4(0,0,0,1);
    //}
    if (dot(quatOut, quatOut) > 1.1 || dot(quatOut, quatOut) < 0.9) {
        ////////////////fail = true;
    }
    quatOut = normalize(quatOut);
}
void readBoneQP(int boneIndex, int yOffset, out float4 quatOut, out float3 posOut) {
    bool fail = false;
    readBoneQP(boneIndex, yOffset, quatOut, posOut, fail);
}
float4x4 boneQPToTransform(float4 quat, float3 pos) {
    float4 position = float4(pos, 1.0);

    float3x3 mat = quaternion_to_matrix(quat);
    //if (all(boneName == float4('H', 'a', 't', 0))) {
    //    mat = float3x3(0,0,0,0,0,0,0,0,0);
    return CreateMatrixFromCols(float4(mat._11_12_13, 0), float4(mat._21_22_23, 0), float4(mat._31_32_33, 0), position);
}

float4 readMeshBoneMapFloat4(int boneIndex, int yOffset) {
    return (float4)TEXLOAD(_MeshTex, int3(boneIndex, yOffset, 0));
}

half4x4 readBoneMapMatrix(int boneIndex, int yOffset, out int parentBoneIdx) {
    //return float4x4(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
    float4 lastColumn = readMeshBoneMapFloat4(boneIndex, yOffset + 3);
    float3 position = lastColumn.xyz;
    parentBoneIdx = (int)lastColumn.w; 
    //if (dot(position, position) <= 1.0) {
    //    return float4x4(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
    //}
    return CreateMatrixFromCols(
        readMeshBoneMapFloat4(boneIndex, yOffset + 0),
        readMeshBoneMapFloat4(boneIndex, yOffset + 1),
        readMeshBoneMapFloat4(boneIndex, yOffset + 2),
        float4(position, 1.0));
}

half4x4 readBoneMapMatrix(int boneIndex, int yOffset) {
    int parentBoneIdx;
    return readBoneMapMatrix(boneIndex, yOffset, parentBoneIdx);
}

/*
float3 readBoneMapPosition(int boneIndex, int yOffset, out int parentBoneIdx) {
    float4 lastColumn = readBoneMapFloat4(boneIndex, yOffset + 3);
    float3 position = lastColumn.xyz;
    parentBoneIdx = (int)lastColumn.w; 
    return position;
}

float3 readBoneMapPosition(int boneIndex, int yOffset) {
    int parentBoneIdx;
    return readBoneMapPosition(boneIndex, yOffset, parentBoneIdx);
}
*/

int4 readBoneMapParents(int boneIndex, int yOffset) {
    return (int4)readMeshBoneMapFloat4(boneIndex, yOffset);
}

void checkBadQuat(float4 quat, inout bool fail) {
}

///////////////////////////x/////////////////////

#ifndef WEIGHT_QUALITY
// #define WEIGHT_QUALITY 4
#define WEIGHT_QUALITY 2
#endif

void vertModifier(inout appdata_full v) {
    int yoffset = 0;
    float4x4 boneTransform = (float4x4)0;
    float4 boneIndices = floor(v.color);
    float4 boneWeights = frac(v.color) + float4(1.e-9, 0.0, 0.0, 0.0);
    boneWeights /= dot(boneWeights, float4(1, WEIGHT_QUALITY > 1, WEIGHT_QUALITY > 2, WEIGHT_QUALITY > 3));
    if (v.texcoord1.w > 0) {
        v.vertex.xyz += readBlendShape((int)v.texcoord1.w, yoffset) * v.texcoord1.xyz;
    }
    if (v.texcoord2.w > 0) {
        v.vertex.xyz += readBlendShape((int)v.texcoord2.w, yoffset) * v.texcoord2.xyz;
    }
    if (v.texcoord3.w > 0) {
        v.vertex.xyz += readBlendShape((int)v.texcoord3.w, yoffset) * v.texcoord3.xyz;
    }
    //v.vertex.x += v.texcoord3.w * 0.1;
    //if (abs(dot(1..xxxx, v.uv4) - 1.0) < 0.1) {
    bool fail = false;
    {
        float scale = 1.0;
        float3 pos;
        float4 quat;
        //boneIndices = uint4(boneIndexMap(boneIndices.x),boneIndexMap(boneIndices.y),boneIndexMap(boneIndices.z),boneIndexMap(boneIndices.w));
        if (boneWeights.x > 0) {
            readBoneQP((int)boneIndices.x, yoffset, quat, pos, fail);
            boneTransform += boneWeights.x * mul(boneQPToTransform(quat, pos), readBoneMapMatrix((int)boneIndices.x, yoffset));
        }
        if (WEIGHT_QUALITY > 1 && boneWeights.y > 0) {
            readBoneQP((int)boneIndices.y, yoffset, quat, pos, fail);
            boneTransform += boneWeights.y * mul(boneQPToTransform(quat, pos), readBoneMapMatrix((int)boneIndices.y, yoffset));
        }
        if (WEIGHT_QUALITY > 2 && boneWeights.z > 0) {
            readBoneQP((int)boneIndices.z, yoffset, quat, pos, fail);
            boneTransform += boneWeights.z * mul(boneQPToTransform(quat, pos), readBoneMapMatrix((int)boneIndices.z, yoffset));
        }
        if (WEIGHT_QUALITY > 3 && boneWeights.w > 0) {
            readBoneQP((int)boneIndices.w, yoffset, quat, pos, fail);
            boneTransform += boneWeights.w * mul(boneQPToTransform(quat, pos), readBoneMapMatrix((int)boneIndices.w, yoffset));
        }
    }
    //} else {
    //    boneTransform = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
    //}
    if (!fail) {
        v.vertex = mul(boneTransform, float4(v.vertex.xyz, 1.));
        v.normal = mul((float3x3)boneTransform, v.normal.xyz);
        v.tangent = float4(mul((float3x3)boneTransform, v.tangent.xyz), v.tangent.w);
    }
}

#endif
