Shader "Avatar/Horizon"
{
    Properties
    {
      [NoScaleOffset]
      [HideIfKeyword(_SHADER_TYPE_SOLID_COLOR, _SHADER_TYPE_HAIR)] // Hide main tex if solid color or hair
      _MainTex ("Main Texture", 2D) = "white" {}

      _Color ("Color", Color) = (1, 1, 1, 1)

      [ShowIfKeyword(_SHADER_TYPE_HAIR, _SHADER_TYPE_SKIN)]
      _SecondaryColor ("Secondary Color", Color) = (1, 1, 1, 1)

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _TertiaryColor ("Tertiary Color", Color) = (1, 1, 1, 0)

      [NoScaleOffset]
      _PropertiesMap("Properties Map", 2D) = "white" {}

      [NoScaleOffset]
      [ShowIfKeyword(_SHADER_TYPE_SKIN, _SHADER_TYPE_HAIR)] // Skin and hair have effects map
      _EffectsMap("Effects Map", 2D) = "black" {}

      _EyeGlintFactor("Eye Glint Factor", Range(1, 20)) = 10.0    // Amplifys the size of the spec highlight known as the eye glint

      // The following three values diverge from the PBR standard and are mostly decide upon by art direction.
      // They are valid within the range [0.0.1.0], but if they are never changed from these defaults, it's best to optimize them out for production.
      _MinDiffuse("Minimum Diffuse Bias", Range(0, 1)) = 0.5    // Note: if we don't ever change this value from 0.5 at production time we should remove it
      _AmbientOcclusionEffect("Occlusion effect on Ambient Light", Range(0, 1)) = 1.0    // Note: if we don't ever change this value from 1.0 at production time we should remove it
      _DirectOcclusionEffect("Occlusion effect on Direct Light", Range(0, 1)) = 1.0    // Note: if we don't ever change this value from 1.0 at production time we should remove it

      // Hair properties

      // Eye Properties

      // TODO* Once Head C is on permanently, remove some properties
      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, false, USE_HEAD_C, true)]
      _LeftEyeUp("Left Eye Up", Float) = 0

      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, false, USE_HEAD_C, true)]
      _LeftEyeRight("Left Eye Right", Float) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, false, USE_HEAD_C, true)]
      _RightEyeUp("Right Eye Up", Float) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, false, USE_HEAD_C, true)]
      _RightEyeRight("Right Eye Right", Float) = 0

      // (A || B) && !C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, false)]
      _UVScale("UV Scale", Range(0, 10)) = 1

      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, USE_HEAD_C, false)]
      _LeftEyeTX("Left Eye X Interpolation", Range(-1, 1)) = 0

      [HideIfKeyword(_SHADER_TYPE_LEFT_EYE, USE_HEAD_C, false)]
      _LeftEyeTY("Left Eye Y Interpolation", Range(-1, 1)) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, USE_HEAD_C, false)]
      _RightEyeTX("Right Eye X Interpolation", Range(-1, 1)) = 0

      [HideIfKeyword(_SHADER_TYPE_RIGHT_EYE, USE_HEAD_C, false)]
      _RightEyeTY("Right Eye Y Interpolation", Range(-1, 1)) = 0

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeXMin("Eye Min X", Float) = 0

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeXMid("Eye Mid X", Float) = 0.5

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeXMax("Eye Max X", Float) = 1

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeYMin("Eye Min Y", Float) = 0

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeYMid("Eye Mid Y", Float) = 0.5

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeYMax("Eye Max Y", Float) = 1

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _PupilScale("Pupil Scale", Range(0, 1)) = 0.5

      // (A || B) && C
      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _IrisScale("Iris Scale", Range(0, 1)) = 0.5

      [ShowIfKeywordAnd(_SHADER_TYPE_LEFT_EYE, _SHADER_TYPE_RIGHT_EYE, true, USE_HEAD_C, true)]
      _EyeUVScale("Eye Scale", Range(0, 3)) = 0.5

      // Skin Properties
      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _Distortion("Translucency Distortion", Range(0, 1)) = 0.5

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _TranslucencyPower("Translucency Power", Range(0, 1)) = 1.0

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _TranslucencyScale("Translucency Scale", Range(0, 2)) = 1.0

      [ShowIfKeyword(_SHADER_TYPE_SKIN)]
      _BacklightScale("Backlight Scale", Range(0, 1)) = 0.2

      [Header (Desaturation Mode Properties)]
      _DesatAmount("AFK Desaturate", Range(0,1)) = 0
      _DesatTint("Desaturation Tint", Color) = (0.255, 0.314, 0.502, 1)
      _DesatLerp("Desaturation Fade", Range(0, 1)) = 0

      // DEBUG_MODES: Uncomment to use Debug modes
      // [KeywordEnum(None, Diffuse, Specular, Indirect_Diffuse, Indirect_Specular, Backlight, Translucency, Vertex Color, UVs, World Normal, World Position, SH)] _Render_Debug("Debug Render", Float) = 0

      [Header (Lighting Systems)]
      [KeywordEnum(Unity, Vertex GI)] _Lighting_System("Lighting System", Float) = 0

      [Header (Shader Type)]
      [KeywordEnum(Solid Color, Textured, Skin, Hair, Left Eye, Right Eye, SubMesh)] _Shader_Type("Shader Type", Float) = 0

      // Will set "USE_HEAD_C" shader keyword when set.
      [Toggle(USE_HEAD_C)] _HeadC ("Use Head C Eye Props?", Float) = 0
    }

    SubShader
    {
        Tags{
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // the following 3 are essential for adding GPU skinning, along with modification of the vertex shader below
            #pragma multi_compile ___ OVR_VERTEX_FETCH_TEXTURE OVR_VERTEX_FETCH_TEXTURE_UNORM
            #pragma multi_compile __ OVR_VERTEX_HAS_TANGENTS
            #pragma target 3.5 // necessary for use of SV_VertexID
            #include "../AvatarCustom.cginc"

            #pragma multi_compile __ LIGHTMAP_ON
            #pragma multi_compile __ LIGHTPROBE_SH
            #pragma multi_compile __ DIRECTIONAL_LIGHT
            #pragma multi_compile __ SHADOWMAP_STATIC_VSM

            #pragma multi_compile __ DESAT
            #pragma multi_compile __ DEBUG_TINT

            #pragma shader_feature __ USE_HEAD_C

            // DEBUG_MODES: Uncomment to use Debug modes
            // #pragma multi_compile __ _RENDER_DEBUG_DIFFUSE _RENDER_DEBUG_SPECULAR _RENDER_DEBUG_INDIRECT_DIFFUSE _RENDER_DEBUG_INDIRECT_SPECULAR _RENDER_DEBUG_BACKLIGHT _RENDER_DEBUG_TRANSLUCENCY _RENDER_DEBUG_VERTEX_COLOR _RENDER_DEBUG_UVS _RENDER_DEBUG_WORLD_NORMAL _RENDER_DEBUG_WORLD_POSITION _RENDER_DEBUG_SH

            #pragma shader_feature _LIGHTING_SYSTEM_UNITY _LIGHTING_SYSTEM_VERTEX_GI
            #pragma shader_feature _SHADER_TYPE_SOLID_COLOR _SHADER_TYPE_TEXTURED _SHADER_TYPE_SKIN _SHADER_TYPE_HAIR _SHADER_TYPE_LEFT_EYE _SHADER_TYPE_RIGHT_EYE _SHADER_TYPE_SUBMESH 

            #include "AvatarCommon/AvatarShaderFramework.cginc"

            // Define some variables for holding main tex and effects maps samples (if needed)
            // through all functions
            half4 mainTexSample;
            half4 effectsMapSample;
            float2 mainTexUV;

            #if defined(_SHADER_TYPE_SOLID_COLOR)
                #include "AvatarSolidColor/AvatarSolidColorProperties.cginc"
                #include "AvatarSolidColor/AvatarSolidColorAlbedo.cginc"

                half3 GetAlbedo() {
                    return SolidColorAlbedo(GetPrimaryColor().rgb);
                }

            #elif defined(_SHADER_TYPE_SUBMESH) || defined(_SHADER_TYPE_TEXTURED)
                #include "AvatarTextured/AvatarTexturedProperties.cginc"
                #include "AvatarTextured/AvatarTexturedAlbedo.cginc"

                #define SAMPLE_MAIN_TEX 1

                half3 GetAlbedo() {
                    return TexturedAlbedo(mainTexSample, GetPrimaryColor().rgb);
                }

            #elif defined(_SHADER_TYPE_SKIN)
                #include "AvatarSkin/AvatarSkinProperties.cginc"
                #include "AvatarSkin/AvatarSkinAlbedo.cginc"
                #include "AvatarSkin/AvatarSkinSurfaceFunctions.cginc"
                #include "AvatarSkin/AvatarSkinLighting.cginc"

                #define SAMPLE_MAIN_TEX 1
                #define SAMPLE_EFFECTS_MAP 1

                half3 GetAlbedo() {
                    return SkinAlbedo(mainTexSample, GetTertiaryColor(), effectsMapSample.b);
                }

            #elif defined(_SHADER_TYPE_HAIR)
                #include "AvatarHair/AvatarHairProperties.cginc"
                #include "AvatarHair/AvatarHairAlbedo.cginc"

                #define SAMPLE_EFFECTS_MAP 1

                half3 GetAlbedo() {
                    return HairAlbedo(effectsMapSample, GetPrimaryColor().rgb, GetSecondaryColor().rgb);
                }

            #elif defined(_SHADER_TYPE_LEFT_EYE) || defined(_SHADER_TYPE_RIGHT_EYE)
                #if defined(USE_HEAD_C)
                    #include "AvatarEye/AvatarEyePropertiesHeadC.cginc"
                    #include "AvatarEye/AvatarEyeAlbedoHeadC.cginc"
                    #include "AvatarEye/AvatarEyeInterpolatorsHeadC.cginc"

                    // Used when calculating main texture coordinates in pixel shader
                    #define CALCULATE_MAIN_TEX_COORDS 1


                    // Calculate per pixel texture coordinates
                    float2 GetMainTexCoords(float2 inputUV) {
                        #if defined(_SHADER_TYPE_LEFT_EYE)
                            return GetNormalizedUVForLeftEye(inputUV);
                         #else
                            return GetNormalizedUVForRightEye(inputUV);
                         #endif
                    }

                    half3 GetAlbedo() {
                        return EyeAlbedo(mainTexSample, GetPrimaryColor().rgb, mainTexUV);
                    }

                #else
                    #include "AvatarEye/AvatarEyeProperties.cginc"
                    #include "AvatarEye/AvatarEyeAlbedo.cginc"
                    #include "AvatarEye/AvatarEyeInterpolators.cginc"


                    half3 GetAlbedo() {
                        return EyeAlbedo(mainTexSample, GetPrimaryColor().rgb);
                    }
                #endif

                #define SAMPLE_MAIN_TEX 1

            #endif

            #if defined(_LIGHTING_SYSTEM_UNITY) || defined(_LIGHTING_SYSTEM_VERTEX_GI)
                #include "AvatarCommon/AvatarCommonSurfaceFields.cginc"
            #if defined(_SHADER_TYPE_SUBMESH)
                #include "AvatarSubmesh/AvatarSubmeshLighting.cginc"
            #else
                #include "AvatarCommon/AvatarCommonLighting.cginc"
            #endif

                ///////////////////////////////
                // Surface Related Functions //
                ///////////////////////////////

                struct AvatarComponentSurfaceOutput {
                    AVATAR_COMMON_SURFACE_FIELDS

                    // Skin component needs more fields
                    #if defined(_SHADER_TYPE_SKIN)
                        SURFACE_ADDITIONAL_FIELDS_SKIN
                    #endif

                    #if defined(_SHADER_TYPE_SUBMESH)
                        float SubMeshType;
                    #endif
                };

                void Surf(v2f IN, inout AvatarComponentSurfaceOutput o) {

                    // All shader types have properties map
                    half4 props = SamplePropertiesMap(IN.propertiesMapUV);

                    #if defined(SAMPLE_MAIN_TEX)
                        #if defined(CALCULATE_MAIN_TEX_COORDS)
                            mainTexUV = GetMainTexCoords(IN.uv);
                            mainTexSample = SampleMainTex(mainTexUV);
                        #else
                            mainTexUV = IN.uv;
                            mainTexSample = SampleMainTex(IN.uv);
                        #endif
                    #endif

                    #if defined(SAMPLE_EFFECTS_MAP)
                        effectsMapSample = SampleEffectsMap(IN.effectsMapUV);
                    #endif

                    SET_AVATAR_SHADER_SURFACE_COMMON_FIELDS(
                        o,
                        GetAlbedo(),
                        props.g, // Roughness in green channel
                        props.b, // Metallic in blue channel
                        1.0, // Alpha of 1 for now
                        props.r, // Occluson in red channel
                        OffsetAndScaleMinimumDiffuse(_MinDiffuse)) // convert min diffuse to [-1,1]

                    #if defined(_SHADER_TYPE_SUBMESH)
                        o.SubMeshType = IN.color.a;
                    #endif

                    // Skin has some additional fields that need populating
                    #if defined(_SHADER_TYPE_SKIN)
                        half backlightScale = _BacklightScale * (1.0 - effectsMapSample.g); /* Backlight scale i.e. "Should this pixel use backlight" in green channel of effects map */
                        o.Thickness = effectsMapSample.r; /* Thickness stored in red channel of effects map */
                        o.BacklightScale = backlightScale;
                        o.TranslucencyColor = GetPrimaryColor();
                        o.BacklightColor = GetSecondaryColor();
                    #endif
                }

                // Surf function has different signatures depending on lighting system
                #if defined(_LIGHTING_SYSTEM_VERTEX_GI)
                    void Surf(v2f IN, inout AvatarComponentSurfaceOutput o, vgi_frag_tmp tmp) {
                        SET_AVATAR_SHADER_SURFACE_NORMAL_FIELD(o, tmp.normal);
                        Surf(IN, o);
                     }
                #endif

                ///////////////////////
                // Lighting Function //
                ///////////////////////

                half4 LightingAvatarComponent(AvatarComponentSurfaceOutput s, half3 viewDir, AvatarShaderGlobalIllumination gi) {
                    AVATAR_SHADER_DECLARE_COMMON_LIGHTING_PARAMS(albedo, normal, perceptualRoughness, perceptualSmoothness, metallic, alpha, minDiffuse, s)

#if defined(_SHADER_TYPE_SUBMESH)
                    float subMeshType = s.SubMeshType;
#endif

                    half4 c = 0.;

                    #if defined(_SHADER_TYPE_SKIN)
                        c.rgb = AvatarSkinLighting(
                            albedo,
                            normal,
                            viewDir,
                            minDiffuse,
                            perceptualRoughness,
                            perceptualSmoothness,
                            metallic,
                            s.Thickness,
                            s.BacklightScale,
                            s.TranslucencyColor,
                            s.BacklightColor,
                            s.Occlusion,
                            gi);
                    #elif defined(_SHADER_TYPE_SUBMESH)
                        c.rgb = AvatarLightingSubmesh(
                            albedo,
                            normal,
                            subMeshType,
                            viewDir,
                            minDiffuse,
                            perceptualRoughness,
                            perceptualSmoothness,
                            metallic,
                            directOcclusion,
                            gi);
                    #else
                        // Common PBS lighting
                        c.rgb = AvatarLightingCommon(
                            albedo,
                            normal,
                            viewDir,
                            minDiffuse,
                            perceptualRoughness,
                            perceptualSmoothness,
                            metallic,
                            directOcclusion,
                            gi);
                    #endif

                    #if defined(DESAT)
                        c.rgb = Desat(c.rgb);
                    #endif

                    #if defined(DEBUG_TINT)
                        c.rgb *= _DebugTint.rgb;
                    #endif

                    c.a = alpha;
                    return c;
                }
            #endif

            //////////////////
            // Interpolator //
            //////////////////
            void Interpolate(float2 texcoord, inout v2f o) {
                // _MainTex texture coordinates computations (if applicable)
                #if defined(_SHADER_TYPE_LEFT_EYE) && !defined(USE_HEAD_C)
                    o.uv.xy = GetNormalizedUVForLeftEye(texcoord);
                    o.propertiesMapUV.xy = texcoord; // properties map and main tex have same UVs
                #elif defined(_SHADER_TYPE_RIGHT_EYE) && !defined(USE_HEAD_C)
                    o.uv.xy = GetNormalizedUVForRightEye(texcoord);
                    o.propertiesMapUV.xy = texcoord; // properties map and main tex have same UVs
                #endif
            }

            #if defined(_LIGHTING_SYSTEM_VERTEX_GI)
                // "Generic" interpolation function that fits the function
                // prototype to be valled from the vertex shader for vertex GI lighting system
                void Interpolate(appdata v, vgi_vert_tmp tmp, inout v2f o) {
                    // Call specific interpolation function for this shader
                    Interpolate(v.uv, o);
                }
            #else
                // "Generic" interpolation function that fits the function
                // prototype to be valled from the vertex shader for non-vertex GI lighting system
                void Interpolate(appdata v, OvrVertexData vertexData, inout v2f o) {
                    // Call specific interpolation function for this shader
                    Interpolate(v.uv, o);
                }
            #endif

            //////////////////////////////
            // Vert and Frag Generation //
            //////////////////////////////
            #if defined(_LIGHTING_SYSTEM_UNITY) || defined(_LIGHTING_SYSTEM_VERTEX_GI)
                GENERATE_AVATAR_SHADER_VERT_FRAG(
                    vert,
                    frag,
                    Interpolate,
                    Surf,
                    AvatarComponentSurfaceOutput,
                    LightingAvatarComponent)
            #endif

            ENDCG
        }
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // the following 3 are essential for adding GPU skinning, along with modification of the vertex shader below
            #pragma multi_compile ___ OVR_VERTEX_FETCH_TEXTURE OVR_VERTEX_FETCH_TEXTURE_UNORM
            #pragma multi_compile __ OVR_VERTEX_HAS_TANGENTS
            #pragma target 3.5 // necessary for use of SV_VertexID
            #include "../AvatarCustom.cginc"

            #pragma multi_compile __ LIGHTMAP_ON
            #pragma multi_compile __ LIGHTPROBE_SH
            #pragma multi_compile __ DIRECTIONAL_LIGHT
            #pragma multi_compile __ SHADOWMAP_STATIC_VSM

            #pragma multi_compile __ DESAT
            #pragma multi_compile __ DEBUG_TINT

            #pragma shader_feature __ USE_HEAD_C

            // DEBUG_MODES: Uncomment to use Debug modes
            // #pragma multi_compile __ _RENDER_DEBUG_DIFFUSE _RENDER_DEBUG_SPECULAR _RENDER_DEBUG_INDIRECT_DIFFUSE _RENDER_DEBUG_INDIRECT_SPECULAR _RENDER_DEBUG_BACKLIGHT _RENDER_DEBUG_TRANSLUCENCY _RENDER_DEBUG_VERTEX_COLOR _RENDER_DEBUG_UVS _RENDER_DEBUG_WORLD_NORMAL _RENDER_DEBUG_WORLD_POSITION _RENDER_DEBUG_SH

            #pragma shader_feature _LIGHTING_SYSTEM_UNITY _LIGHTING_SYSTEM_VERTEX_GI
            #pragma shader_feature _SHADER_TYPE_SOLID_COLOR _SHADER_TYPE_TEXTURED _SHADER_TYPE_SKIN _SHADER_TYPE_HAIR _SHADER_TYPE_LEFT_EYE _SHADER_TYPE_RIGHT_EYE _SHADER_TYPE_SUBMESH

            #include "AvatarCommon/AvatarShaderFramework.cginc"

            // Define some variables for holding main tex and effects maps samples (if needed)
            // through all functions
            half4 mainTexSample;
            half4 effectsMapSample;
            float2 mainTexUV;

            #if defined(_SHADER_TYPE_SOLID_COLOR)
                #include "AvatarSolidColor/AvatarSolidColorProperties.cginc"
                #include "AvatarSolidColor/AvatarSolidColorAlbedo.cginc"

                half3 GetAlbedo() {
                    return SolidColorAlbedo(GetPrimaryColor().rgb);
                }

            #elif defined(_SHADER_TYPE_SUBMESH) || defined(_SHADER_TYPE_TEXTURED)
                #include "AvatarTextured/AvatarTexturedProperties.cginc"
                #include "AvatarTextured/AvatarTexturedAlbedo.cginc"

                #define SAMPLE_MAIN_TEX 1

                half3 GetAlbedo() {
                    return TexturedAlbedo(mainTexSample, GetPrimaryColor().rgb);
                }

            #elif defined(_SHADER_TYPE_SKIN)
                #include "AvatarSkin/AvatarSkinProperties.cginc"
                #include "AvatarSkin/AvatarSkinAlbedo.cginc"
                #include "AvatarSkin/AvatarSkinSurfaceFunctions.cginc"
                #include "AvatarSkin/AvatarSkinLighting.cginc"

                #define SAMPLE_MAIN_TEX 1
                #define SAMPLE_EFFECTS_MAP 1

                half3 GetAlbedo() {
                    return SkinAlbedo(mainTexSample, GetTertiaryColor(), effectsMapSample.b);
                }

            #elif defined(_SHADER_TYPE_HAIR)
                #include "AvatarHair/AvatarHairProperties.cginc"
                #include "AvatarHair/AvatarHairAlbedo.cginc"

                #define SAMPLE_EFFECTS_MAP 1

                half3 GetAlbedo() {
                    return HairAlbedo(effectsMapSample, GetPrimaryColor().rgb, GetSecondaryColor().rgb);
                }

            #elif defined(_SHADER_TYPE_LEFT_EYE) || defined(_SHADER_TYPE_RIGHT_EYE)
                #if defined(USE_HEAD_C)
                    #include "AvatarEye/AvatarEyePropertiesHeadC.cginc"
                    #include "AvatarEye/AvatarEyeAlbedoHeadC.cginc"
                    #include "AvatarEye/AvatarEyeInterpolatorsHeadC.cginc"

                    // Used when calculating main texture coordinates in pixel shader
                    #define CALCULATE_MAIN_TEX_COORDS 1


                    // Calculate per pixel texture coordinates
                    float2 GetMainTexCoords(float2 inputUV) {
                        #if defined(_SHADER_TYPE_LEFT_EYE)
                            return GetNormalizedUVForLeftEye(inputUV);
                         #else
                            return GetNormalizedUVForRightEye(inputUV);
                         #endif
                    }

                    half3 GetAlbedo() {
                        return EyeAlbedo(mainTexSample, GetPrimaryColor().rgb, mainTexUV);
                    }

                #else
                    #include "AvatarEye/AvatarEyeProperties.cginc"
                    #include "AvatarEye/AvatarEyeAlbedo.cginc"
                    #include "AvatarEye/AvatarEyeInterpolators.cginc"


                    half3 GetAlbedo() {
                        return EyeAlbedo(mainTexSample, GetPrimaryColor().rgb);
                    }
                #endif

                #define SAMPLE_MAIN_TEX 1


            #endif

            #if defined(_LIGHTING_SYSTEM_UNITY) || defined(_LIGHTING_SYSTEM_VERTEX_GI)
                #include "AvatarCommon/AvatarCommonSurfaceFields.cginc"
            #if defined(_SHADER_TYPE_SUBMESH)
                #include "AvatarSubmesh/AvatarSubmeshLighting.cginc"
            #else
                #include "AvatarCommon/AvatarCommonLighting.cginc"
            #endif

                ///////////////////////////////
                // Surface Related Functions //
                ///////////////////////////////

                struct AvatarComponentSurfaceOutput {
                    AVATAR_COMMON_SURFACE_FIELDS

                    // Skin component needs more fields
                    #if defined(_SHADER_TYPE_SKIN)
                        SURFACE_ADDITIONAL_FIELDS_SKIN
                    #endif

                    #if defined(_SHADER_TYPE_SUBMESH)
                        float SubMeshType;
                    #endif
                };

                void Surf(v2f IN, inout AvatarComponentSurfaceOutput o) {

                    // All shader types have properties map
                    half4 props = SamplePropertiesMap(IN.propertiesMapUV);

                    #if defined(SAMPLE_MAIN_TEX)
                        #if defined(CALCULATE_MAIN_TEX_COORDS)
                            mainTexUV = GetMainTexCoords(IN.uv);
                            mainTexSample = SampleMainTex(mainTexUV);
                        #else
                            mainTexUV = IN.uv;
                            mainTexSample = SampleMainTex(IN.uv);
                        #endif
                    #endif

                    #if defined(SAMPLE_EFFECTS_MAP)
                        effectsMapSample = SampleEffectsMap(IN.effectsMapUV);
                    #endif

                    SET_AVATAR_SHADER_SURFACE_COMMON_FIELDS(
                        o,
                        GetAlbedo(),
                        props.g, // Roughness in green channel
                        props.b, // Metallic in blue channel
                        1.0, // Alpha of 1 for now
                        props.r, // Occluson in red channel
                        OffsetAndScaleMinimumDiffuse(_MinDiffuse)) // convert min diffuse to [-1,1]

                    #if defined(_SHADER_TYPE_SUBMESH)
                        o.SubMeshType = IN.color.a;
                    #endif

                    // Skin has some additional fields that need populating
                    #if defined(_SHADER_TYPE_SKIN)
                        half backlightScale = _BacklightScale * (1.0 - effectsMapSample.g); /* Backlight scale i.e. "Should this pixel use backlight" in green channel of effects map */
                        o.Thickness = effectsMapSample.r; /* Thickness stored in red channel of effects map */
                        o.BacklightScale = backlightScale;
                        o.TranslucencyColor = GetPrimaryColor();
                        o.BacklightColor = GetSecondaryColor();
                    #endif
                }

                // Surf function has different signatures depending on lighting system
                #if defined(_LIGHTING_SYSTEM_VERTEX_GI)
                    void Surf(v2f IN, inout AvatarComponentSurfaceOutput o, vgi_frag_tmp tmp) {
                        SET_AVATAR_SHADER_SURFACE_NORMAL_FIELD(o, tmp.normal);
                        Surf(IN, o);
                     }
                #endif

                ///////////////////////
                // Lighting Function //
                ///////////////////////

                half4 LightingAvatarComponent(AvatarComponentSurfaceOutput s, half3 viewDir, AvatarShaderGlobalIllumination gi) {
                    AVATAR_SHADER_DECLARE_COMMON_LIGHTING_PARAMS(albedo, normal, perceptualRoughness, perceptualSmoothness, metallic, alpha, minDiffuse, s)

#if defined(_SHADER_TYPE_SUBMESH)
                    float subMeshType = s.SubMeshType;
#endif

                    half4 c = 0.;

                    #if defined(_SHADER_TYPE_SKIN)
                        c.rgb = AvatarSkinLighting(
                            albedo,
                            normal,
                            viewDir,
                            minDiffuse,
                            perceptualRoughness,
                            perceptualSmoothness,
                            metallic,
                            s.Thickness,
                            s.BacklightScale,
                            s.TranslucencyColor,
                            s.BacklightColor,
                            s.Occlusion,
                            gi);
                    #elif defined(_SHADER_TYPE_SUBMESH)
                        c.rgb = AvatarLightingSubmesh(
                            albedo,
                            normal,
                            subMeshType,
                            viewDir,
                            minDiffuse,
                            perceptualRoughness,
                            perceptualSmoothness,
                            metallic,
                            directOcclusion,
                            gi);
                    #else
                        // Common PBS lighting
                        c.rgb = AvatarLightingCommon(
                            albedo,
                            normal,
                            viewDir,
                            minDiffuse,
                            perceptualRoughness,
                            perceptualSmoothness,
                            metallic,
                            directOcclusion,
                            gi);
                    #endif

                    #if defined(DESAT)
                        c.rgb = Desat(c.rgb);
                    #endif

                    #if defined(DEBUG_TINT)
                        c.rgb *= _DebugTint.rgb;
                    #endif

                    c.a = alpha;
                    return c;
                }
            #endif

            //////////////////
            // Interpolator //
            //////////////////

            void Interpolate(float2 texcoord, inout v2f o) {
                // _MainTex texture coordinates computations (if applicable)
                #if defined(_SHADER_TYPE_LEFT_EYE) && !defined(USE_HEAD_C)
                    o.uv.xy = GetNormalizedUVForLeftEye(texcoord);
                    o.propertiesMapUV.xy = texcoord; // properties map and main tex have same UVs
                #elif defined(_SHADER_TYPE_RIGHT_EYE) && !defined(USE_HEAD_C)
                    o.uv.xy = GetNormalizedUVForRightEye(texcoord);
                    o.propertiesMapUV.xy = texcoord; // properties map and main tex have same UVs
                #endif
            }

            #if defined(_LIGHTING_SYSTEM_VERTEX_GI)
                // "Generic" interpolation function that fits the function
                // prototype to be valled from the vertex shader for vertex GI lighting system
                void Interpolate(appdata v, vgi_vert_tmp tmp, inout v2f o) {
                    // Call specific interpolation function for this shader
                    Interpolate(v.uv, o);
                }
            #else
                // "Generic" interpolation function that fits the function
                // prototype to be valled from the vertex shader for non-vertex GI lighting system
                void Interpolate(appdata v, OvrVertexData vertexData, inout v2f o) {
                    // Call specific interpolation function for this shader
                    Interpolate(v.uv, o);
                }
            #endif

            //////////////////////////////
            // Vert and Frag Generation //
            //////////////////////////////
            #if defined(_LIGHTING_SYSTEM_UNITY) || defined(_LIGHTING_SYSTEM_VERTEX_GI)
                GENERATE_AVATAR_SHADER_VERT_FRAG(
                    vert,
                    frag,
                    Interpolate,
                    Surf,
                    AvatarComponentSurfaceOutput,
                    LightingAvatarComponent)
            #endif

            ENDCG
        }
    }

}
