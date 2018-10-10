// Hides a mesh by Rokk
Shader "LyumaShader/HiddenToonTransparentUnlit" { Properties {
        _MainTex("Main Tex UNUSED", 2D) = "white" {}
        _Color("Color Tint", Color) = (1,1,1,1)
} SubShader { Pass { ZWrite Off ColorMask Off } } }
