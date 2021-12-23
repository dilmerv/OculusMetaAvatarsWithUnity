// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

#ifndef AVATAR_CUSTOM_INCLUDED
#define AVATAR_CUSTOM_INCLUDED

#include "UnityCG.cginc"

// TODO*: Documentation here

#include "AvatarCustomTypes.cginc"

//-------------------------------------------------------------------------------------

#if defined(OVR_VERTEX_FETCH_TEXTURE) || defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)
  sampler3D u_AttributeTexture;   // NOTE: This texture can be visualized in the Unity editor, just expand in inspector and manually change "Dimension" to "3D" on top line

  // int4 u_AttributeTexelRect;
  int u_AttributeTexelX;
  int u_AttributeTexelY;
  int u_AttributeTexelW;
  int u_AttributeTexelH;
  float u_AttributeTexelSlice;

  // float3 u_AttributeTexInvSize;
  float u_AttributeTexInvSizeW;
  float u_AttributeTexInvSizeH;
  float u_AttributeTexInvSizeD;

  #if defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)
    float2 u_AttributeScaleBias;
  #endif

  float3 ovrGetAttributeTexCoord(int attributeRowOffset, uint vertIndex) {
    // Compute texture coordinate in the attribute texture

    // Compute which row in the texel rect
    // the vertex index is
    int row = vertIndex / u_AttributeTexelW;
    int column = vertIndex % u_AttributeTexelW;

    #if defined(OVR_VERTEX_HAS_TANGENTS)
      row = row * 3;
    #else
      row = row * 2;
    #endif

    // Calculate texel centers
    column = u_AttributeTexelX + column;
    row = u_AttributeTexelY + row + attributeRowOffset;

    float3 coord = float3(float(column), float(row), u_AttributeTexelSlice);
    float3 invSize = float3(u_AttributeTexInvSizeW, u_AttributeTexInvSizeH, u_AttributeTexInvSizeD);

    // Compute texture coordinate for texel center
    return (2.0 * coord + 1.0) * 0.5 * invSize;
  }

  float3 ovrGetPositionTexCoord(uint vid) {
    return ovrGetAttributeTexCoord(0,vid);
  }

  float3 ovrGetNormalTexCoord(uint vid) {
    return ovrGetAttributeTexCoord(1, vid);
  }

  float3 ovrGetTangentTexCoord(uint vid) {
    return ovrGetAttributeTexCoord(2, vid);
  }

  //-------------------------------------------------------------------------------------
  // Avatar Vertex fetch setup

  float4 OvrGetVertexPositionFromTexture(uint vid) {
    float4 pos = tex3Dlod(u_AttributeTexture, float4(ovrGetPositionTexCoord(vid), 0));
    #if defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)
      pos = pos * u_AttributeScaleBias.x + u_AttributeScaleBias.y;
    #endif
    return pos;
  }

  float4 OvrGetVertexNormalFromTexture(uint vid) {
    float4 norm = tex3Dlod(u_AttributeTexture, float4(ovrGetNormalTexCoord(vid), 0));
    #if defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)
      norm = norm * u_AttributeScaleBias.x + u_AttributeScaleBias.y;
    #endif
    return norm;
  }

  float4 OvrGetVertexTangentFromTexture(uint vid) {
    float4 tan = tex3Dlod(u_AttributeTexture, float4(ovrGetTangentTexCoord(vid), 0));
    #if defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)
      tan = tan * u_AttributeScaleBias.x + u_AttributeScaleBias.y;
    #endif
    return tan;
  }

#endif // defined(OVR_VERTEX_FETCH_TEXTURE) || defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)

// NOTE: Some of these functions might just result into no-ops and be compiled out (hopefully),
// but these functions are examples on how to set up similar functions that might not be no-ops
// in the future

// First, define a function which takes explicit data types, then define a macro which expands
// an arbitrary vertex structure definition into the function parameters
#if defined(OVR_VERTEX_FETCH_TEXTURE) || defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)

  OvrVertexData OvrCreateVertexData(uint vertexId) {
    OvrVertexData vertData;

    vertData.vertexId = vertexId;
    vertData.position = OvrGetVertexPositionFromTexture(vertexId);
    vertData.normal = OvrGetVertexNormalFromTexture(vertexId);

    #if defined(OVR_VERTEX_HAS_TANGENTS)
      vertData.tangent = OvrGetVertexTangentFromTexture(vertexId);
    #endif

    return vertData;
  }

  #define OVR_CREATE_VERTEX_DATA(v) OvrCreateVertexData(OVR_GET_VERTEX_VERT_ID_FIELD(v))
#else // defined(OVR_VERTEX_FETCH_TEXTURE) || defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)
  OvrVertexData OvrCreateVertexData(float4 position, float3 normal) {
    OvrVertexData vertData;

    vertData.position = position;
    vertData.normal = normal;

    return vertData;
  }

  #if defined(OVR_VERTEX_HAS_TANGENTS)
    OvrVertexData OvrCreateVertexData(float4 position, float3 normal, float4 tangent) {
      OvrVertexData vertData = OvrCreateVertexData(position, normal);

      vertData.tangent = tangent;

      return vertData;
    }

    // Define OVR_CREATE_VERTEX_DATA for a vertex input struct
    // that contains position, normal, and tangent
    #define OVR_CREATE_VERTEX_DATA(v) OvrCreateVertexData(OVR_GET_VERTEX_POSITION_FIELD(v), OVR_GET_VERTEX_NORMAL_FIELD(v), OVR_GET_VERTEX_TANGENT_FIELD(v))

  #else // defined(OVR_VERTEX_HAS_TANGENTS)

    // Define OVR_CREATE_VERTEX_DATA for a vertex input struct
    // that contains position and normal
    #define OVR_CREATE_VERTEX_DATA(v) OvrCreateVertexData(OVR_GET_VERTEX_POSITION_FIELD(v), OVR_GET_VERTEX_NORMAL_FIELD(v))

  #endif // defined(OVR_VERTEX_HAS_TANGENTS)
#endif // defined(OVR_VERTEX_FETCH_TEXTURE) || defined(OVR_VERTEX_FETCH_TEXTURE_UNORM)


// Initialization for "required fields" in the vertex input struct for the vertex shader.
// Written as a macro to be expandable in future
#define OVR_INITIALIZE_VERTEX_FIELDS(v)

// Initializes the fields for a defined default vertex structure
void OvrInitializeDefaultAppdata(inout OvrDefaultAppdata v) {
  OVR_INITIALIZE_VERTEX_FIELDS(v);
  UNITY_SETUP_INSTANCE_ID(v);
}

// Initializes the fields for a defined default vertex structure
// and creates the OvrVertexData for the vertex as well as overrides
// applicable fields in OvrDefaultAppdata with fields from OvrVertexData.
// Mainly useful in surface shader vertex functions.
OvrVertexData OvrInitializeDefaultAppdataAndPopulateWithVertexData(inout OvrDefaultAppdata v) {
  OvrInitializeDefaultAppdata(v);
  OvrVertexData vertexData = OVR_CREATE_VERTEX_DATA(v);

  OVR_SET_VERTEX_POSITION_FIELD(v, vertexData.position);
  OVR_SET_VERTEX_NORMAL_FIELD(v, vertexData.normal);

  #if defined(OVR_VERTEX_HAS_TANGENTS)
    OVR_SET_VERTEX_TANGENT_FIELD(v, vertexData.tangent);
  #endif

  return vertexData;
}

#endif // AVATAR_CUSTOM_INCLUDED
