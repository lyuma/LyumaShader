#ifndef SHADER_SKIN_BASE_CGINC__
#define SHADER_SKIN_BASE_CGINC__
#include "HLSLSupport.cginc"
#include "UnityCG.cginc"
/*
#pragma vertex vert
#pragma geometry geom
#pragma fragment frag
#pragma only_renderers d3d9 d3d11 glcore gles 
#pragma target 4.6
*/

#if NO_POINT_STREAM
#define BONEBLEND_VERTEX_MULTIPLIER 4
#define BONEBLEND_STREAM_TYPE(cls) TriangleStream<cls>
#else
#define BONEBLEND_VERTEX_MULTIPLIER 1
#define BONEBLEND_STREAM_TYPE(cls) PointStream<cls>
#endif
#define BONEBLEND_GEOM_TYPE(numpt) [maxvertexcount(numpt * BONEBLEND_VERTEX_MULTIPLIER)]

#if SHADERSKIN_INSTANCES
#elif INSTANCE_HEAVY
#define SHADERSKIN_INSTANCES 32
#else
#define SHADERSKIN_INSTANCES 1
#endif
#define SHADERSKIN_GEOM_TYPE(numpt) [instance(SHADERSKIN_INSTANCES)] [maxvertexcount(numpt)]





struct boneblend_VertexInput {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    half4 color : COLOR; // name in ASCII
    float4 uv1 : TEXCOORD0;
};

struct boneblend_v2g {
    float4 uv1 : TEXCOORD0;
    half4 color : TEXCOORD2;
    float4 bindPose_col0 : TEXCOORD3;
    float4 bindPose_col1 : TEXCOORD4;
    float4 bindPose_col2 : TEXCOORD5;
    float4 bindPose_col3 : TEXCOORD6;
};
struct boneblend_g2f {
    float4 vertex : SV_POSITION;
    float4 color : TEXCOORD0;
};

struct shaderskin_VertexInput {
    float4 texcoord1 : TEXCOORD1;
    float4 texcoord2 : TEXCOORD2;
};

shaderskin_VertexInput shaderskin_noop_vert(shaderskin_VertexInput i) {
    return i;
}

float4x4 CreateMatrixFromCols(float4 c0, float4 c1, float4 c2, float4 c3) {
    return float4x4(c0.x, c1.x, c2.x, c3.x,
                    c0.y, c1.y, c2.y, c3.y,
                    c0.z, c1.z, c2.z, c3.z,
                    c0.w, c1.w, c2.w, c3.w);
}
#define CreateMatrixFromVert(v) CreateMatrixFromCols(v.bindPose_col0, v.bindPose_col1, v.bindPose_col2, v.bindPose_col3)

boneblend_v2g boneblend_vert (boneblend_VertexInput v) {
    boneblend_v2g o = (boneblend_v2g)0;
    float scale = length(v.normal.xyz);

    o.bindPose_col2 = float4(scale * normalize(v.normal.xyz), 0);//scale * float4(1,0,0,0); //float4(scale * normalize(v.normal.xyz), 0);
    o.bindPose_col0 = float4(scale * normalize(v.tangent.xyz), 0);//scale * float4(0,1,0,0); //float4(scale * normalize(v.tangent.xyz), 0);
    o.bindPose_col1 = float4(scale * v.tangent.w * cross(normalize(v.normal.xyz), normalize(v.tangent.xyz)), 0); // / scale//scale * float4(0,0,1,0); //
    //o.bindPose_col1 = float4(scale * cross(normalize(v.normal.xyz), normalize(v.tangent.xyz)), 0); // / scale//scale * float4(0,0,1,0); //
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
    //return float2(ret.x, 1.0 - ret.y);
}

void appendPixelToStream(inout TriangleStream<boneblend_g2f> tristream, float2 pixelCoordinate, float4 color) {

    boneblend_g2f o = (boneblend_g2f)0;
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

void appendPixelToStream(inout PointStream<boneblend_g2f> ptstream, float2 pixelCoordinate, float4 color) {

    boneblend_g2f o = (boneblend_g2f)0;
    o.color = color * 0.1;
#if UNITY_UV_STARTS_AT_TOP
    float2 uvflip = float2(1., -1.);
#else
    float2 uvflip = float2(1., 1.);
#endif
    o.vertex = float4(uvflip*(pixelToUV(pixelCoordinate, float2(.49,.49)) * 2. - float2(1.,1.)), 0., 1.);
    ptstream.Append(o);
}

struct InputBufferElem {
    float4 vertex;
    float4 normal;
    float4 tangent;
    float4 uvColor;
    float4 boneIndices;
    float4 boneWeights;
};

#define readHalf4(tex, pixelIndex) (tex).Load(int3((pixelIndex), 0))

#define READ_MESH_DATA_BLEND(tex1, tex2, morphValue) \
        o.vertex = lerp(readHalf4(tex1, pixelIndex + uint2(0,0)), readHalf4(tex2, pixelIndex + uint2(0,0)), frac(morphValue)); \
        o.normal = lerp(readHalf4(tex1, pixelIndex + uint2(1,0)), readHalf4(tex2, pixelIndex + uint2(1,0)), frac(morphValue)); \
        o.tangent = lerp(readHalf4(tex1, pixelIndex + uint2(2,0)), readHalf4(tex2, pixelIndex + uint2(2,0)), frac(morphValue)); \
        o.uvColor = lerp(readHalf4(tex1, pixelIndex + uint2(3,0)), readHalf4(tex2, pixelIndex + uint2(3,0)), frac(morphValue)); \
        o.boneIndices = lerp(readHalf4(tex1, pixelIndex + uint2(4,0)), readHalf4(tex2, pixelIndex + uint2(4,0)), frac(morphValue)); \
        o.boneWeights = lerp(readHalf4(tex1, pixelIndex + uint2(5,0)), readHalf4(tex2, pixelIndex + uint2(5,0)), frac(morphValue))
        // why bone is *2????

/*
#define READ_MESH_DATA_MORPH(tex1, tex2, morphValue) \
// float whichTex = floor(_MorphValue);
// float texFrac = frac(_MorphValue);
    if (whichTex < 1) {
        READ_MESH_DATA_BLEND(_MeshTex, _MeshTex1, morphValue);
    } else if (whichTex < 2) {
        READ_MESH_DATA_BLEND(_MeshTex1, _MeshTex2, morphValue);
    } else if (whichTex < 3) {
        READ_MESH_DATA_BLEND(_MeshTex2, _MeshTex3, morphValue);
    } else if (whichTex < 4) {
        READ_MESH_DATA_BLEND(_MeshTex3, _MeshTex4, morphValue);
    } else {
        READ_MESH_DATA_BLEND(_MeshTex4, _MeshTex, morphValue);
    }*/
#define READ_MESH_DATA(tex1) \
        o.vertex = readHalf4(tex1, pixelIndex + uint2(0,0)); \
        o.normal = readHalf4(tex1, pixelIndex + uint2(1,0)); \
        o.tangent = readHalf4(tex1, pixelIndex + uint2(2,0)); \
        o.uvColor = readHalf4(tex1, pixelIndex + uint2(3,0)); \
        o.boneIndices = readHalf4(tex1, pixelIndex + uint2(4,0)); \
        o.boneWeights = readHalf4(tex1, pixelIndex + uint2(5,0))

#define TEX2D_BLEND(tex1, tex2, coord, morphValue) lerp(tex2D(tex1, coord), tex2D(tex2, coord), frac(morphValue))
//half3 readBlendShapeHalf3(uint2 pixelIndex, int blendIdx) {
//    return _BlendShapeTexArray.Load(int4(pixelIndex, blendIdx, 0)).xyz;
//}

/*
    float4 tex;
    float whichTex = floor(_MorphValue % 5);
    float texFrac = frac(_MorphValue);
    if (whichTex < 1) {
        tex = lerp(
            tex2D(_MorphTex, float4(fragin.uv.xy, 0, 1)),
            tex2D(_MorphTex1, float4(fragin.uv.xy, 0, 1)),
            texFrac);
    } else if (whichTex < 2) {
        tex = lerp(
            tex2D(_MorphTex1, float4(fragin.uv.xy, 0, 1)),
            tex2D(_MorphTex2, float4(fragin.uv.xy, 0, 1)),
            texFrac);
    } else if (whichTex < 3) {
        tex = lerp(
            tex2D(_MorphTex2, float4(fragin.uv.xy, 0, 1)),
            tex2D(_MorphTex3, float4(fragin.uv.xy, 0, 1)),
            texFrac);
    } else if (whichTex < 4) {
        tex = lerp(
            tex2D(_MorphTex3, float4(fragin.uv.xy, 0, 1)),
            tex2D(_MorphTex4, float4(fragin.uv.xy, 0, 1)),
            texFrac);
    } else {
        tex = lerp(
            tex2D(_MorphTex4, float4(fragin.uv.xy, 0, 1)),
            tex2D(_MorphTex, float4(fragin.uv.xy, 0, 1)),
            texFrac);
    }
*/
/*half4x4 readBoneMapMatrix(int boneIndex, int yOffset) {
    // unused?
    half4x4 ret = CreateMatrixFromCols(
        readHalf4(int2(boneIndex, yOffset + 0)),
        readHalf4(int2(boneIndex, yOffset + 1)),
        readHalf4(int2(boneIndex, yOffset + 2)),
        readHalf4(int2(boneIndex, yOffset + 3))
    );
    return ret;
}*/

half readBlendShapeState(int blendIndex) {
    float adj = sin(10 * _Time.x);
    return _BoneBindTexture.Load(int3(blendIndex-adj, 4, 0)).x;
}

Texture2D<half4> _MeshTex;
sampler2D _MorphTex;

InputBufferElem readMeshData(uint instanceID, uint primitiveID, uint vertexNum) {
    uint pixelNum = 3 * (instanceID + primitiveID * SHADERSKIN_INSTANCES) + vertexNum;
    uint2 elemIndex = uint2((pixelNum % 64), pixelNum / 64);
    uint2 pixelIndex = uint2(6 * elemIndex.x, elemIndex.y + 8);

    InputBufferElem o;
    READ_MESH_DATA(_MeshTex);
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



BONEBLEND_GEOM_TYPE(10)
void boneblend_geom(triangle boneblend_v2g vertin[3], inout BONEBLEND_STREAM_TYPE(boneblend_g2f) stream,
        uint primitiveID: SV_PrimitiveID)
{
    if (vertin[0].uv1.x >= 1. && vertin[0].uv1.y == 0.) {
        float4x4 transformMatrix = unity_ObjectToWorld;
        appendPixelToStream(stream, float2(0, 0), transformMatrix._11_21_31_41.yzwx);
        appendPixelToStream(stream, float2(0, 1), transformMatrix._12_22_32_42.zwxy);
        appendPixelToStream(stream, float2(0, 2), transformMatrix._13_23_33_43.wxyz);
        appendPixelToStream(stream, float2(0, 3), transformMatrix._14_24_34_44.xyzw);
        appendPixelToStream(stream, float2(0, 5), transformMatrix._11_22_33_44.xyzw);

        float4x4 identity = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
        float4x4 oldTransformMatrix = readBindTransform(-1, 0);
        oldTransformMatrix = lerp(oldTransformMatrix, transformMatrix, all(oldTransformMatrix._11_12_13_21 == 0.0) * all(oldTransformMatrix._22_23_31_32 == 0.0));
        float4x4 oldTransformMatrixRA = readBindTransform(-1, 1);
        oldTransformMatrixRA = lerp(oldTransformMatrixRA, transformMatrix, all(oldTransformMatrixRA._11_12_13_21 == 0.0) * all(oldTransformMatrixRA._22_23_31_32 == 0.0));
        float4x4 newTransformMatrix = lerp(oldTransformMatrix, oldTransformMatrixRA, 0.95);

        appendPixelToStream(stream, float2(0, 6 + 0), newTransformMatrix._11_21_31_41.yzwx);
        appendPixelToStream(stream, float2(0, 6 + 1), newTransformMatrix._12_22_32_42.zwxy);
        appendPixelToStream(stream, float2(0, 6 + 2), newTransformMatrix._13_23_33_43.wxyz);
        appendPixelToStream(stream, float2(0, 6 + 3), newTransformMatrix._14_24_34_44.xyzw);
        appendPixelToStream(stream, float2(0, 6 + 5), newTransformMatrix._11_22_33_44.xyzw);
    } else if (vertin[0].uv1.x >= 1.) {
        // blend shape
        float blendValue = vertin[0].bindPose_col3.x;

        appendPixelToStream(stream, float2(vertin[0].uv1.y, 4), float4(blendValue, 0.5, 1, 1.));
    } else {
        float4x4 transformMatrix = CreateMatrixFromVert(vertin[0]);
        float boneId = vertin[0].uv1.y;
        appendPixelToStream(stream, float2(1+boneId, 0), transformMatrix._11_21_31_41.yzwx);
        appendPixelToStream(stream, float2(1+boneId, 1), transformMatrix._12_22_32_42.zwxy);
        appendPixelToStream(stream, float2(1+boneId, 2), transformMatrix._13_23_33_43.wxyz);
        appendPixelToStream(stream, float2(1+boneId, 3), transformMatrix._14_24_34_44.xyzw);
        appendPixelToStream(stream, float2(1+boneId, 5), transformMatrix._11_22_33_44.xyzw);

        float4x4 identity = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
        float4x4 oldTransformMatrix = readBindTransform(boneId, 0);
        oldTransformMatrix = lerp(oldTransformMatrix, transformMatrix, all(oldTransformMatrix._11_12_13_21 == 0.0) * all(oldTransformMatrix._22_23_31_32 == 0.0));
        float4x4 oldTransformMatrixRA = readBindTransform(boneId, 1);
        oldTransformMatrixRA = lerp(oldTransformMatrixRA, transformMatrix, all(oldTransformMatrixRA._11_12_13_21 == 0.0) * all(oldTransformMatrixRA._22_23_31_32 == 0.0));
        float4x4 newTransformMatrix = lerp(oldTransformMatrix, oldTransformMatrixRA, 0.95);

        appendPixelToStream(stream, float2(1+boneId, 6 + 0), newTransformMatrix._11_21_31_41.yzwx);
        appendPixelToStream(stream, float2(1+boneId, 6 + 1), newTransformMatrix._12_22_32_42.zwxy);
        appendPixelToStream(stream, float2(1+boneId, 6 + 2), newTransformMatrix._13_23_33_43.wxyz);
        appendPixelToStream(stream, float2(1+boneId, 6 + 3), newTransformMatrix._14_24_34_44.xyzw);
        appendPixelToStream(stream, float2(1+boneId, 6 + 5), newTransformMatrix._11_22_33_44.xyzw);
    }
}

float4 boneblend_frag(boneblend_g2f fragin) : SV_Target {
    return float4(fragin.color.xyzw);
}

/*
#ifdef UNITY_STEREO_INSTANCING_ENABLED
    #define UNITY_VERTEX_OUTPUT_STEREO_ORIG uint stereoTargetEyeIndex : SV_RenderTargetArrayIndex;
#elif defined(UNITY_STEREO_MULTIVIEW_ENABLED)
    #define UNITY_VERTEX_OUTPUT_STEREO_ORIG float stereoTargetEyeIndex : BLENDWEIGHT0;
#else
    #define UNITY_VERTEX_OUTPUT_STEREO_ORIG
#endif

#undef UNITY_VERTEX_OUTPUT_STEREO
#define UNITY_VERTEX_OUTPUT_STEREO UNITY_VERTEX_OUTPUT_STEREO_ORIG };*/
#if defined(VERT_TYPE) && defined(VERT_FUNCTION) && defined(APPDATA_TYPE)
uniform float4 _BoneCutoff;
#ifndef GEOM_OUT_TYPE
#define GEOM_OUT_TYPE VERT_TYPE
#endif
#ifndef OVERRIDE_GEOM_NUMPT
#define OVERRIDE_GEOM_NUMPT 3
#endif

VERT_TYPE VERT_FUNCTION (APPDATA_TYPE v);
SHADERSKIN_GEOM_TYPE(OVERRIDE_GEOM_NUMPT)
void shaderskin_geom(triangle shaderskin_VertexInput vertin[3], inout TriangleStream<GEOM_OUT_TYPE> tristream,
        uint primitiveID: SV_PrimitiveID, uint instanceID : SV_GSInstanceID) {
    APPDATA_TYPE ad = (APPDATA_TYPE)0;
    uint i;
    #ifdef GEOM_FUNCTION
    VERT_TYPE geomxInput [3];
    geomxInput[0] = (VERT_TYPE)0;
    geomxInput[1] = (VERT_TYPE)0;
    geomxInput[2] = (VERT_TYPE)0;
    #endif
    [unroll]
    for (i = 0; i < 3; i++) {
        InputBufferElem buf = readMeshData(instanceID, primitiveID, i);
        float4x4 boneTransform;
        float4x4 worldTransform;
        /*if (!((buf.boneIndices.x >= _BoneCutoff.x &&
                (_BoneCutoff.y == 0 || buf.boneIndices.x < _BoneCutoff.y)) ||
                ((_BoneCutoff.z > 0 && buf.boneIndices.x >= _BoneCutoff.z) &&
                (_BoneCutoff.w == 0 || buf.boneIndices.x < _BoneCutoff.w)))) {
            return;
        }*/
        if (dot(buf.boneWeights, buf.boneWeights) > 0.0) {
            boneTransform = (float4x4)0;
            float scale = 1.0;
            boneTransform += buf.boneWeights.x * readBindTransform(buf.boneIndices.x, 0);
            boneTransform += buf.boneWeights.y * readBindTransform(buf.boneIndices.y, 0);
            boneTransform += buf.boneWeights.z * readBindTransform(buf.boneIndices.z, 0);
            boneTransform += buf.boneWeights.w * readBindTransform(buf.boneIndices.w, 0);
        } else {
            boneTransform = float4x4(1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1);
        }
        ad.vertex = mul(boneTransform, float4(buf.vertex.xyz, 1.));
        ad.normal = mul((float3x3)boneTransform, buf.normal.xyz);
        ad.tangent = float4(mul((float3x3)boneTransform, buf.tangent.xyz), buf.tangent.w);
        ad.texcoord = float4(buf.uvColor.xy, buf.boneIndices.x, 0);
        ad.texcoord1 = vertin[i].texcoord1;
        ad.texcoord2 = buf.boneWeights;
        ad.texcoord3 = buf.boneIndices;
        ad.color = buf.boneIndices / 255.; /*ad.color = fixed4(0,0,0,0);*/
        #ifdef GEOM_FUNCTION
        geomxInput[i] = VERT_FUNCTION(ad);
        #else
        VERT_TYPE o = VERT_FUNCTION(ad);
        tristream.Append(o);
        #endif
    }
    #ifdef GEOM_FUNCTION
    // Unimplemented: semantics including VFACE, SV_PrimitiveID, SV_GSInstanceID
    #ifdef PRIMITIVEID_MIDDLE
        GEOM_FUNCTION(geomxInput, instanceID + primitiveID * SHADERSKIN_INSTANCES, tristream);
    #else
        GEOM_FUNCTION(geomxInput, tristream);
    #endif
    #endif
}

#ifdef ENABLE_TRAIL
uniform float _FrameOffset;
uniform float _FrameOffsetLerp;
uniform float _TrailOffset;
//uniform float _TrailLength;
[instance(ENABLE_TRAIL)]
[maxvertexcount(OVERRIDE_GEOM_NUMPT)]
void shaderskin_geomTrail(triangle shaderskin_VertexInput vertin[3], inout TriangleStream<GEOM_OUT_TYPE> tristream,
        uint primitiveID: SV_PrimitiveID, uint instanceID : SV_GSInstanceID) {
    if (instanceID > _TrailLength) {
        return;
    }
    APPDATA_TYPE ad = (APPDATA_TYPE)0;
    uint i;
    #ifdef GEOM_FUNCTION
    VERT_TYPE geomxInput [3];
    geomxInput[0] = (VERT_TYPE)0;
    geomxInput[1] = (VERT_TYPE)0;
    geomxInput[2] = (VERT_TYPE)0;
    #endif


    float4x4 worldTransformCur;
    float4x4 worldTransformPrev;
    float4x4 boneTransformCur[3];
    float4x4 boneTransformPrev[3];
    float4x4 defaultTransform = float4x4(1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1);
    InputBufferElem meshBuf[3];
    float fail = 0.0;
    [unroll]
    for (i = 0; i < 3; i++) {
        InputBufferElem buf = readMeshData(0, primitiveID, i);
        meshBuf[i] = buf;
        if (!((buf.boneIndices.x >= _BoneCutoff.x &&
                (_BoneCutoff.y == 0 || buf.boneIndices.x < _BoneCutoff.y)) ||
                ((_BoneCutoff.z > 0 && buf.boneIndices.x >= _BoneCutoff.z) &&
                (_BoneCutoff.w == 0 || buf.boneIndices.x < _BoneCutoff.w)))) {
            fail = 1.0;
        }
        boneTransformCur[i] = buf.boneWeights.x * readBindTransform(buf.boneIndices.x, _FrameOffset - 1);
        boneTransformCur[i] += buf.boneWeights.y * readBindTransform(buf.boneIndices.y, _FrameOffset - 1);
        boneTransformCur[i] += buf.boneWeights.z * readBindTransform(buf.boneIndices.z, _FrameOffset - 1);
        boneTransformCur[i] += buf.boneWeights.w * readBindTransform(buf.boneIndices.w, _FrameOffset - 1);
        boneTransformPrev[i] = buf.boneWeights.x * readBindTransform(buf.boneIndices.x, _FrameOffset);
        boneTransformPrev[i] += buf.boneWeights.y * readBindTransform(buf.boneIndices.y, _FrameOffset);
        boneTransformPrev[i] += buf.boneWeights.z * readBindTransform(buf.boneIndices.z, _FrameOffset);
        boneTransformPrev[i] += buf.boneWeights.w * readBindTransform(buf.boneIndices.w, _FrameOffset);
    }
    if (fail * instanceID > 0.5) {
        //buf.vertex = lerp(buf.vertex, float4(0,0,0,1), saturate(float(instanceID)));
        return;
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
    for (i = 0; i < 3; i++) {
        InputBufferElem buf = meshBuf[i];
        float trailLerp = 1. - _FrameOffsetLerp * (instanceID + _TrailOffset);
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
        ad.vertex = mul(worldTransform, mul(boneTransform, float4(buf.vertex.xyz, 1.)));
        ad.normal = mul((float3x3)boneTransform, buf.normal.xyz);
        ad.tangent = float4(mul((float3x3)boneTransform, buf.tangent.xyz), buf.tangent.w);
        ad.texcoord = float4(buf.uvColor.xy, buf.boneIndices.x, instanceID);
        ad.texcoord1 = vertin[i].texcoord1;
        ad.texcoord2 = buf.boneWeights;
        ad.texcoord3 = buf.boneIndices;
        ad.color = buf.boneIndices / 255.; /*ad.color = fixed4(0,0,0,0);*/
        #ifdef GEOM_FUNCTION
        geomxInput[i] = VERT_FUNCTION(ad);
        #else
        VERT_TYPE o = VERT_FUNCTION(ad);
        tristream.Append(o);
        #endif
        //o.pos = UnityObjectToClipPos(mul(mul(worldTransform, boneTransform), float4(buf.vertex.xyz, 1.)));
        //o.uv = float4(buf.uvColor.xy, buf.boneIndices.x, trailLerp);
    }
    #ifdef GEOM_FUNCTION
    // Unimplemented: semantics including VFACE, SV_PrimitiveID, SV_GSInstanceID
    #ifdef PRIMITIVEID_MIDDLE
        GEOM_FUNCTION(geomxInput, instanceID, tristream);
    #else
        GEOM_FUNCTION(geomxInput, tristream);
    #endif
    #endif
}
#endif
#endif
//ad.color = buf.boneIndices / 255.;
 
#endif
