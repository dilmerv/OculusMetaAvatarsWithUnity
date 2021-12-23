#ifndef AVATAR_TEXTURED_ALBEDO_CGINC
#define AVATAR_TEXTURED_ALBEDO_CGINC

half3 TexturedAlbedo(half4 mainTex, half3 color) {
  // Alpha channel of mainTex is a mask, 0 means tint by using _Color, 1 means no _Color
  return lerp(mainTex.rgb * color.rgb, mainTex.rgb, mainTex.a);
}

#endif
