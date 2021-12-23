#ifndef OVR_OUTPUT_BUFFER_COMPUTE_INCLUDED
#define OVR_OUTPUT_BUFFER_COMPUTE_INCLUDED

#include "OvrDecodeUtils.cginc"

//////////////////////////////////////////////////////
// Output
//////////////////////////////////////////////////////

void StoreVertexNormal(
  inout RWByteAddressBuffer output_buffer,
  int output_buffer_start_address,
  in float3 normal,
  int output_index)
{
  static const int STRIDE = 4; // 1 32-bit uint for 10_10_10_2

  // Normalize on store
  const int address = mad(output_index, STRIDE, output_buffer_start_address);
  output_buffer.Store(address, PackSnorm4x10_10_10_2(float4(normalize(normal.xyz), 0.0)));
}

void StoreVertexTangent(
  inout RWByteAddressBuffer output_buffer,
  int output_buffer_start_address,
  in float4 tangent,
  int output_index)
{
  static const int STRIDE = 4; // 1 32-bit uint for 10_10_10_2

  // Normalize on store
  const int address = mad(output_index, STRIDE, output_buffer_start_address);
  output_buffer.Store(address, PackSnorm4x10_10_10_2(float4(normalize(tangent.xyz), tangent.w)));
}

void StoreVertexPositionFloat4x32(
  inout RWByteAddressBuffer output_buffer,
  int output_buffer_start_address,
  in float4 position,
  int output_index)
{
  static const int POS_STRIDE = 4 * 4; // 4 32-bit uints for 4 32-bit floats
  const int address = mad(output_index, POS_STRIDE, output_buffer_start_address);

  output_buffer.Store4(address, asuint(position));
}

void StoreVertexPositionHalf4x16(
  inout RWByteAddressBuffer output_buffer,
  int output_buffer_start_address,
  in float4 position,
  int output_index)
{
  static const int STRIDE = 4 * 2; // 2 32-bit uints for 4 16-bit halfs
  const int address = mad(output_index, STRIDE, output_buffer_start_address);

  output_buffer.Store2(address, uint2(PackHalf2x16(position.xy), PackHalf2x16(position.zw)));
}

void StoreVertexPositionUnorm4x16(
  inout RWByteAddressBuffer output_buffer,
  int output_buffer_start_address,
  in float4 position,
  in float3 position_bias,
  in float3 position_scale,
  int output_index)
{
  static const int STRIDE = 4 * 2; // 2 32-bit uints for 4 16-bit unorms
  const int address = mad(output_index, STRIDE, output_buffer_start_address);

  // Normalize to 0 -> 1 but given the bias and scale
  // ASSUMPTION: Assuming the position_bias and position_scale will be large enough
  // to place in the range 0 -> 1
  float4 normalized = float4((position.xyz - position_bias) / position_scale, position.w);

  output_buffer.Store2(address, uint2(PackUnorm2x16(normalized.xy), PackUnorm2x16(normalized.zw)));
}

void StoreVertexPositionUnorm4x8(
  inout RWByteAddressBuffer output_buffer,
  int output_buffer_start_address,
  in float4 position,
  in float3 position_offset,
  in float3 position_scale,
  int output_index)
{
  static const int STRIDE = 4; // 1 32-bit uints for 4 8-bit unorms
  const int address = mad(output_index, STRIDE, output_buffer_start_address);

  // Normalize to 0 -> 1 but given the offset and scale
  // ASSUMPTION: Assuming the position_offset and position_scale will be large enough
  // to place in the range 0 -> 1
  const float4 normalized = float4((position.xyz - position_offset) / position_scale, position.w);

  output_buffer.Store(address, PackUnorm4x8(normalized));
}

int CalculatePositionOutputIndex(int vertex_output_index)
{
  return vertex_output_index;
}

int CalculateDoubleBufferedPositionOutputIndex(int vertex_output_index, bool write_to_second_slice)
{
  // * 2 due to double buffering, then maybe +1 if writing to second slice
  return 2 * vertex_output_index + (write_to_second_slice ? 1 : 0);
}

int CalculateNormalOutputIndex(int vertex_output_index, bool has_tangents)
{
  // *2 if interleaving tangent
  return (has_tangents ? 2 : 1) * vertex_output_index;
}

int CalculateTangentOutputIndex(int vertex_output_index)
{
  // * 2 due to interleaved normals + tangent, then +1 for the tangent
  return 2 * vertex_output_index + 1;
}

int CalculateDoubleBufferedNormalOutputIndex(int vertex_output_index, bool has_tangents, bool write_to_second_slice)
{
  // * 2 due to double buffering, then an additional *2 if interleaving tangent, then maybe +1 if writing to second slice
  return (has_tangents ? 4 : 2) * vertex_output_index + (write_to_second_slice ? 1 : 0);
}

uint CalculateDoubleBufferedTangentOutputIndex(int vertex_output_index, bool write_to_second_slice)
{
  // * 4 due to double buffering and interleaved normals + tangent, then maybe +3 if writing to second slice
  return 4 * vertex_output_index + (write_to_second_slice ? 3 : 2);
}

void StoreVertexPosition(
  inout RWByteAddressBuffer output_buffer,
  int output_buffer_start_address,
  int format,
  float3 position_bias,
  float3 position_scale,
  in float4 position,
  int output_index)
{
  [branch] switch(format)
  {
    case OVR_FORMAT_FLOAT_32:
      StoreVertexPositionFloat4x32(
        output_buffer,
        output_buffer_start_address,
        position,
        output_index);
    break;
    case OVR_FORMAT_HALF_16:
      StoreVertexPositionHalf4x16(
        output_buffer,
        output_buffer_start_address,
        position,
        output_index);
    break;
    case OVR_FORMAT_UNORM_16:
      StoreVertexPositionUnorm4x16(
        output_buffer,
        output_buffer_start_address,
        position,
        position_bias,
        position_scale,
        output_index);
    break;
    case OVR_FORMAT_UNORM_8:
      StoreVertexPositionUnorm4x8(
        output_buffer,
        output_buffer_start_address,
        position,
        position_bias,
        position_scale,
        output_index);
    break;
    default:
      break;
  }
}

// Compiler should hopefully optimize out any potential branches due to static const bool values,
// and otherwise, branches should be based on uniform parameters passed in which
// should make their just the branch and not cause diverging branches across workgroups
// Compiler should also optimize out unused parameters
void StoreVertexOutput(
  inout RWByteAddressBuffer position_output_buffer,
  inout RWByteAddressBuffer output_buffer,
  int position_output_buffer_start_address,
  int output_buffer_start_address,
  float3 position_bias,
  float3 position_scale,
  int position_format,
  in float4 position,
  in float3 normal,
  in float4 tangent,
  int vertex_output_index,
  bool has_tangents,
  bool double_buffer,
  bool write_to_second_slice)
{
  const int pos_output_index =
    double_buffer ?
      CalculateDoubleBufferedPositionOutputIndex(vertex_output_index, write_to_second_slice) :
      CalculatePositionOutputIndex(vertex_output_index);
  const int norm_output_index = double_buffer ?
    CalculateDoubleBufferedNormalOutputIndex(vertex_output_index, has_tangents, write_to_second_slice) :
    CalculateNormalOutputIndex(vertex_output_index, has_tangents);

  StoreVertexPosition(
    position_output_buffer,
    position_output_buffer_start_address,
    position_format,
    position_bias,
    position_scale,
    position,
    pos_output_index);

  StoreVertexNormal(
    output_buffer,
    output_buffer_start_address,
    normal,
    norm_output_index);

  if (has_tangents) {
    const int tangent_output_index = double_buffer ?
      CalculateDoubleBufferedTangentOutputIndex(vertex_output_index, write_to_second_slice) :
      CalculateTangentOutputIndex(vertex_output_index);

    StoreVertexTangent(output_buffer, output_buffer_start_address, tangent, tangent_output_index);
  }
}

#endif
