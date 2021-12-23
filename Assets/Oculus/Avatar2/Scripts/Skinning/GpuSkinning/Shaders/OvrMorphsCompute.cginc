#ifndef OVR_MORPHS_COMPUTE_INCLUDED
#define OVR_MORPHS_COMPUTE_INCLUDED

#include "OvrDecodeUtils.cginc"

float GetMorphTargetWeight(
  in ByteAddressBuffer data_buffer,
  uint morph_target_weights_start_address,
  uint morph_target_index)
{
  static const uint STRIDE = 4u; // single 32-bit float
  const int address = mad(STRIDE, morph_target_index, morph_target_weights_start_address);
  return UnpackFloat1x32(data_buffer.Load(address));
}

// Yes, macros. Was too much copy and paste otherwise

// ASSUMPTION: Assumes some variable names to limit number of parameters to the macro
// so, if stuff fails to compile, it might be do to bad assumed variable names
#define OVR_APPLY_RECTANGULAR_MORPHS_BODY(unpack_func) \
  // Loop over all morph targets, applying the weighted deltas for the attribute to \
  // a running sum \
  for (uint i = 0; i < num_morphs; i++) { \
    const float weight = GetMorphTargetWeight( \
      dynamic_data_buffer, \
      morph_target_weights_start_address, \
      i); \
\
    // Add weighted deltas for this vertex for this morph target \
    pos_sum += pos_range * weight * unpack_func(static_data_buffer, address); \
\
    address += attribute_row_stride; \
    norm_sum += norm_range * weight * unpack_func(static_data_buffer, address); \
\
    if (has_tangents) { \
      address += attribute_stride; \
      tan_sum += tan_range * weight * unpack_func(static_data_buffer, address); \
    } \
\
    // advance address to the position for the vertex in the next morph target \
    address += attribute_row_stride; \
  }

// Compiler should hopefully optimize out any potential branches due to static const bool values.
// Compiler should also optimize out unused parameters
void ApplyRectangularMorphs(
    in ByteAddressBuffer static_data_buffer,
    in ByteAddressBuffer dynamic_data_buffer,
    int morph_target_deltas_start_address,
    int morph_target_weights_start_address,
    int num_morphs,
    int num_morphed_vertices,
    int morph_target_deltas_format,
    inout float4 position,
    float3 pos_range,
    inout float3 normal,
    float3 norm_range,
    inout float4 tangent,
    float3 tan_range,
    int vertex_index,
    bool has_tangents)
{
  static const int STRIDE_32 = 4 * 4; // In memory as 4 component vectors for alignment purposes (needed?)
  static const int STRIDE_16 = 2 * 4;
  static const int STRIDE_10_10_10_2 = 4;
  static const int STRIDE_8 = 4;

  // ASSUMPTION: Data for a given morph target is arranged
  // with all position deltas, then all normal deltas
  int address = 0;
  int attribute_row_stride = 0;

  float3 pos_sum = 0.0;
  float3 norm_sum = 0.0;
  float3 tan_sum = 0.0;

  [branch] switch(morph_target_deltas_format)
  {
    case OVR_FORMAT_FLOAT_32:
      address = mad(STRIDE_32, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_32;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(UnpackFloat3x32);
    break;
    case OVR_FORMAT_HALF_16:
      address = mad(STRIDE_16, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_16;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(UnpackHalf3x16);
    break;
    case OVR_FORMAT_UNORM_16:
      address = mad(STRIDE_16, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_16;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(UnpackUnorm3x16);
    break;
    case OVR_FORMAT_SNORM_10_10_10_2:
      address = mad(STRIDE_10_10_10_2, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_10_10_10_2;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(UnpackVector_10_10_10_2);
    break;
    case OVR_FORMAT_UNORM_8:
      address = mad(STRIDE_8, vertex_index, morph_target_deltas_start_address);
      attribute_row_stride = num_morphed_vertices * STRIDE_8;
      OVR_APPLY_RECTANGULAR_MORPHS_BODY(UnpackUnorm3x8);
    break;
    default:
      // error?
    break;
  }

  position.xyz += pos_sum;
  normal += norm_sum;
  if (has_tangents) {
    tangent.xyz += tan_sum;
  }
}

#endif
