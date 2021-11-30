// Hides a mesh by Rokk
Shader "LyumaShader/HiddenToonTransparentUnlit" { Properties {
        _MainTex("Main Tex UNUSED", 2D) = "white" {}
        _Color("Color Tint", Color) = (1,1,1,1)
} SubShader {
Tags { "Queue"="Geometry" "IgnoreProjector"="True" "VRCFallback"="Hidden" }
Pass { ZWrite Off ColorMask Off
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
float4 vert() : SV_Position { return 1; }
float4 frag() : SV_Target { return 0; }
ENDCG
} } }
