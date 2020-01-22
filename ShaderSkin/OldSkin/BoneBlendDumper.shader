Shader "LyumaShader/ShaderSkin/BoneBlendDumper" 
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
                "Queue"="Geometry-1"
                "RenderType"="Opaque"
        }
        CGINCLUDE
        #include "ShaderSkinBase.cginc"
        ENDCG
        Pass
        {
            Cull Off
            ZTest Always
            ZWrite Off // On to cover the screen intentionally???
            ColorMask RGBA
            Blend One Zero, One Zero
            BlendOp Add, Add

            CGPROGRAM
            #pragma vertex boneblend_vert
            #pragma geometry boneblend_geom
            #pragma fragment boneblend_frag
            #pragma only_renderers d3d9 d3d11 glcore gles 
            #pragma target 4.6
            ENDCG
        }
        GrabPass { "_BoneBindTexture" }
    }
}
