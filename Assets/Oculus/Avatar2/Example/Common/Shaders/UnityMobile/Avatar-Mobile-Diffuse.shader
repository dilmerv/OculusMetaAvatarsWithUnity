// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

// Simplified Diffuse shader. Differences from regular Diffuse one:
// - no Main Color
// - fully supports only 1 directional light. Other lights can affect it, but it will be per-vertex/SH.

Shader "Avatar/Mobile/Diffuse" {
Properties {
    _MainTex ("Base (RGB)", 2D) = "white" {}
}
SubShader {
    Tags { "RenderType"="Opaque" }
    LOD 150

CGPROGRAM
#pragma surface surf Lambert vertex:vert nolightmap noforwardadd

    // the following lines are essential for adding GPU skinning, along with modification of the vertex shader below
    #pragma multi_compile ___ OVR_VERTEX_FETCH_TEXTURE OVR_VERTEX_FETCH_TEXTURE_UNORM
    #pragma multi_compile __ OVR_VERTEX_HAS_TANGENTS
    #pragma target 3.5 // necessary for use of SV_VertexID
    #include "../AvatarCustom.cginc"

sampler2D _MainTex;

struct Input {
    float2 uv_MainTex;
};

void vert(inout OvrDefaultAppdata v) {
  OvrInitializeDefaultAppdataAndPopulateWithVertexData(v);
}

void surf (Input IN, inout SurfaceOutput o) {
    fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
    o.Albedo = c.rgb;
    o.Alpha = c.a;
}
ENDCG
}

Fallback "Mobile/Diffuse"
}
