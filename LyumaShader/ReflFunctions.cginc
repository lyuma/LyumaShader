/****
Mirror Reflection clone.
See tweet here for an example of how it looks:
https://twitter.com/Lyuma2d/status/1165107161367629825

First, make a default unity Plane object. Assign this Plane as your avatar's Root Bone. make sure to set the mesh bounds on your avatar larger to encompass the entire distance between the plane and the furthest points such as your head.

Next, if you want your head to show up in the mirror, click the gear menu on your Body mesh Skinned Mesh Renderer and choose 'Head Scaling Reverse : LMTx'

Finally, you need to use a modified avatar shader on your avatar which does the mirror reflecting using a prepass and a second pass; and you must also use a special stencil shader on the Plane (FX/MirrorReflectionAvatar).

Modifications to be made to your chosen shader:

0. Add this code after SubShader {
CGINCLUDE
#include "ReflFunctions.cginc"
ENDCG
1. Ensure the shader passes uv as a 3d or 4d coordinate from vert to frag.
2. Edit the frag() function implementation, and add:

#ifdef CLIP_HOOK
	CLIP_HOOK(i.uv.z);
#endif

This can probably also be modified to use udims or something else.

3. Add or a "depth prepass" which is the same as FORWARD, but has ColorMask 0.
Note that it must also have **Cull Front** not back, because we are flipping the faces in shader.

4. Duplicate the forward pass and make it **Cull Front** and also add this code:
			#define UnityObjectToClipPos(x) reflProcessing(x, v)

5. Repeat this for the ForwardAdd pass.
****/

#include "UnityCG.cginc"

float4x4 CreateMatrixFromCols(float4 c0, float4 c1, float4 c2, float4 c3) {
    return float4x4(c0.x, c1.x, c2.x, c3.x,
                    c0.y, c1.y, c2.y, c3.y,
                    c0.z, c1.z, c2.z, c3.z,
                    c0.w, c1.w, c2.w, c3.w);
}
float4 reflClipPos(float3 vertIn) {
    // -1 -1 1 rotate or 1 -1 1 flip
    return UnityObjectToClipPos(float4(vertIn.xyz * float3(1,-1,1), 1));
}
float4 realReflProcessingFunc(inout float4 v_vertex, inout float3 v_normal, inout float3 v_tangent, float4 v_texcoord, float4 v_texcoord1, float4 v_texcoord2, float4 v_texcoord3);

float4 reflProcessingFunc(inout float4 v_vertex, inout float3 v_normal, inout float4 v_tangent, float4 v_texcoord, float4 v_texcoord1, float4 v_texcoord2, float4 v_texcoord3) {
    float3 v_tangent3 = v_tangent.xyz;
    float4 ret = realReflProcessingFunc(v_vertex, v_normal, v_tangent3, v_texcoord, v_texcoord1, v_texcoord2, v_texcoord3);
    v_tangent.xyz = v_tangent3;
    return ret;
}
float4 reflProcessingFunc(inout float3 v_vertex, inout float3 v_normal, inout float4 v_tangent, float4 v_texcoord, float4 v_texcoord1, float4 v_texcoord2, float4 v_texcoord3) {
    float3 v_tangent3 = v_tangent.xyz;
    float4 v_vertex4 = float4(v_vertex, 1.0);
    float4 ret = realReflProcessingFunc(v_vertex4, v_normal, v_tangent3, v_texcoord, v_texcoord1, v_texcoord2, v_texcoord3);
    v_vertex = v_vertex4.xyz;
    return ret;
}
float4 reflProcessingFunc(inout float3 v_vertex, inout float3 v_normal, inout float3 v_tangent, float4 v_texcoord, float4 v_texcoord1, float4 v_texcoord2, float4 v_texcoord3) {
    float4 v_vertex4 = float4(v_vertex, 1.0);
    float4 ret = realReflProcessingFunc(v_vertex4, v_normal, v_tangent, v_texcoord, v_texcoord1, v_texcoord2, v_texcoord3);
    v_vertex = v_vertex4.xyz;
    return ret;
}
float4 reflProcessingFunc(inout float4 v_vertex, inout float3 v_normal, inout float3 v_tangent, float4 v_texcoord, float4 v_texcoord1, float4 v_texcoord2, float4 v_texcoord3) {
    return realReflProcessingFunc(v_vertex, v_normal, v_tangent, v_texcoord, v_texcoord1, v_texcoord2, v_texcoord3);
}
float4 realReflProcessingFunc(inout float4 v_vertex, inout float3 v_normal, inout float3 v_tangent, float4 v_texcoord, float4 v_texcoord1, float4 v_texcoord2, float4 v_texcoord3) {
    float scale = 1;
    if (all(v_texcoord2.xyz != 0)) {
        if (length(v_vertex) > 25) {
            return float4(2,2,2,1);
        }
        float4 bindPose_col2 = float4(scale * normalize(v_normal.xyz), 0);
        float4 bindPose_col0 = float4(scale * normalize(v_tangent.xyz), 0);
        float4 bindPose_col1 = float4(scale * cross(normalize(v_normal.xyz), normalize(v_tangent.xyz)), 0);
        float4 bindPose_col3 = float4(v_vertex.xyz, 1);
        float4x4 transformMatrix = CreateMatrixFromCols(bindPose_col0, bindPose_col1, bindPose_col2, bindPose_col3);
        v_vertex = mul(transformMatrix, float4(v_texcoord2.xyz, 1));
        v_normal = normalize(mul((float3x3)transformMatrix, v_texcoord3.xyz));
        v_tangent = normalize(mul((float3x3)transformMatrix, float3(v_texcoord.zw, v_texcoord2.w)));
        //, v_texcoord3.w);
        //v_vertex.xyz = v_vertex.xyz + 1 * v_tangent.xyz;// + 0.001 * float3(sin(v_texcoord.x * 10), cos(v_texcoord.y * 10), tan(v_texcoord.x));
    }
    v_normal = -v_normal;
    return reflClipPos(v_vertex);
}
#define reflProcessing(vertIn, v) reflProcessingFunc(v.vertex, v.normal, v.tangent, v.uv, v.uv1, v.uv2, v.uv3)

float det(float2 pa, float2 pq) {
    return pa.x * pq.y - pq.x * pa.y;
}

bool pointInTriangle(float2 v0, float2 v1, float2 v2) {
    // Compute dot products
    float dot00 = dot(v0, v0);
    float dot01 = dot(v0, v1);
    float dot02 = dot(v0, v2);
    float dot11 = dot(v1, v1);
    float dot12 = dot(v1, v2);

    // Compute barycentric coordinates
    float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
    float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
    float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

    // Check if point is in triangle
    return (u >= 0) && (v >= 0) && (u + v < 1);
}

bool SameSide(float2 p1, float2 p2, float2 a, float2 b) {
    float3 cp1 = cross(float3(b-a, 0), float3(p1-a, 0));
    float3 cp2 = cross(float3(b-a, 0), float3(p2-a, 0));
    return dot(cp1, cp2) >= 0;
}

bool PointInQuad(float2 p, float2 a, float2 b, float2 c, float2 d) {
    return (SameSide(p,b,a,c) && SameSide(p,c,b,d) && SameSide(p,d,a,b) && SameSide(p,a,c,d));
}

float4 normalClipPos(inout float3 vertIn, float farClip) {
    float4 ret = UnityObjectToClipPos(float4(vertIn.xyz, 1));
    if (vertIn.y > 0) {
        return ret;
    } else if (farClip < 0) {
        //vertIn = float3(0,0,0);
        return ret;
    }
    float4 planeClip = UnityObjectToClipPos(float4(0, 0, 0, 1));
    // technically need to be multiplied by 1.5 to match exact plane bounds.
    // better to be a little conservative here...
    float4 clip1 = UnityObjectToClipPos(float4(-.5, 0, -.5, 1));
    float4 clip2 = UnityObjectToClipPos(float4(.5, 0, -.5, 1));
    float4 clip3 = UnityObjectToClipPos(float4(-.5, 0, .5, 1));
    float4 clip4 = UnityObjectToClipPos(float4(.5, 0, .5, 1));
    float2 origin1 = clip1.xy / clip1.w;
    float2 origin2 = clip4.xy / clip4.w;
    if (farClip < 0.5) {
        if (dot(cross(float3(clip2.xy / clip2.w - clip1.xy / clip1.w, 0), float3(clip3.xy / clip3.w - clip1.xy / clip1.w, 0)), float3(0,0,-1)) < 0) {
            ret.z = vertIn.y < 0 ? 1000 * ret.w : ret.z;//(1-farClip) * ret.w : ret.z;
        }
        if (!PointInQuad(ret.xy/ret.w, clip1.xy/clip1.w, clip2.xy/clip2.w, clip3.xy/clip3.w, clip4.xy/clip4.w)) {
            ret.z = vertIn.y < 0 ? 1000 * ret.w : ret.z;//(1-farClip) * ret.w : ret.z;
        }
    }
    return ret;
}
float4 normalClipPos(inout float4 vertIn, float farClip) {
    return normalClipPos(vertIn.xyz, farClip);
}
