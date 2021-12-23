#ifndef OVR_DECODE_UTILS_INCLUDED
#define OVR_DECODE_UTILS_INCLUDED

static const int OVR_FORMAT_FLOAT_32 = 0;
static const int OVR_FORMAT_HALF_16  = 1;
static const int OVR_FORMAT_UNORM_16 = 2;
static const int OVR_FORMAT_UINT_16 = 3;
static const int OVR_FORMAT_SNORM_10_10_10_2 = 4;
static const int OVR_FORMAT_UNORM_8 = 5;
static const int OVR_FORMAT_UINT_8 = 6;

int bitfieldExtract10(int value, int offset) {
  value = value >> offset;
  value &= 0x03ff;
  if ((value & 0x0200) != 0) {
    value |= 0xfffffc00;
  }
  return value;
}

uint BitfieldExtract(uint data, uint offset, uint numBits)
{
  const uint mask = (1u << numBits) - 1u;
  return (data >> offset) & mask;
}

// With sign extension
int BitfieldExtract(int data, uint offset, uint numBits)
{
  int  shifted = data >> offset;      // Sign-extending (arithmetic) shift
  int  signBit = shifted & (1u << (numBits - 1u));
  uint mask    = (1u << numBits) - 1u;

  return -signBit | (shifted & mask); // Use 2-complement for negation to replicate the sign bit
}

uint BitfieldInsert(uint base, uint insert, uint offset, uint numBits)
{
  uint mask = ~(0xffffffffu << numBits) << offset;
  mask = ~mask;
  base = base & mask;
  return base | (insert << offset);
}

// Unpack 1x float (32 bit) from a single 32 bit uint
float UnpackFloat1x32(uint u) {
  // Just re-interpret as an float
  return asfloat(u);
}

// Pack 1x float (32 bit) into a single 32 bit uint
uint PackFloat1x32(float val) {
  // Just re-interpret as a uint
  return asuint(val);
}

// Unpack 2x "half floats" (16 bit) from a single 32 bit uint
float2 UnpackHalf2x16(uint u) {
  const uint y = (u >> 16) & 0xffffu;
  const uint x = u & 0xFFFFu;

  return float2(f16tof32(x), f16tof32(y));
}

// Pack 2x "half floats" (16 bit) from a single 32 bit uint
uint PackHalf2x16(float2 halfs) {
  const uint x = f32tof16(halfs.x);
  const uint y = f32tof16(halfs.y);

  return x + (y << 16);
}

// Unpack 2 16-bit unsigned integers
uint2 UnpackUint2x16(uint packed_values) {
  uint y = (packed_values >> 16) & 0xffffu;
  uint x = packed_values & 0xffffu;

  return uint2(x, y);
}

// Pack 2 16-bit unsigned integers into a single 32-bit uint
uint PackUint2x16(uint2 vals) {
  return vals.x + (vals.y << 16);
}

// Unpack UNorm [0, 1] 2 16 bit entries (packed in a single 32 bit uint)
float2 UnpackUnorm2x16(uint packed_values) {
  uint2 non_normalized = UnpackUint2x16(packed_values);

  // Convert from 0 -> 65535 to 0 -> 1
  const float inv = 1.0 / 65535.0;

  return float2(non_normalized.x * inv, non_normalized.y * inv);
}

// Pack 2x unsigned normalized values (16 bit) into a single 32 bit uint
uint PackUnorm2x16(float2 unorms) {
  // Convert from 0 -> 1 to 0 -> 65535
  const float factor = 65535.0;
  const uint x = round(saturate(unorms.x) * factor);
  const uint y = round(saturate(unorms.y) * factor);

  return PackUint2x16(uint2(x, y));
}

// Unpack 4 8-bit unsigned integers from a single 32-bit uint
uint4 UnpackUint4x8(uint four_packed_values) {
  uint w = (four_packed_values >> 24) & 0xffu;
  uint z = (four_packed_values >> 16) & 0xffu;
  uint y = (four_packed_values >> 8) & 0xffu;
  uint x = four_packed_values & 0xffu;

  return uint4(x, y, z, w);
}

// Pack 4 8-bit unsigned integers into a single 32-bit uint
uint PackUint4x8(uint4 vals) {
  return vals.x + (vals.y << 8) + (vals.z << 16) + (vals.w << 24);
}

// Unpack UNorm [0, 1] 4 bytes (as a 32 bit uint)
float4 UnpackUnorm4x8(uint four_packed_values) {
  uint4 non_normalized = UnpackUint4x8(four_packed_values);

  // Convert from 0 -> 255 to 0 -> 1
  const float inv255 = 1.0 / 255.0;

  return float4(
    non_normalized.x * inv255,
    non_normalized.y * inv255,
    non_normalized.z * inv255,
    non_normalized.w * inv255);
}

uint PackUnorm4x8(float4 unorms) {
  const float factor = 255.0;
  const uint x = round(saturate(unorms.x) * factor);
  const uint y = round(saturate(unorms.y) * factor);
  const uint z = round(saturate(unorms.z) * factor);
  const uint w = round(saturate(unorms.w) * factor);

  return PackUint4x8(uint4(x, y, z, w));
}

float4 UnpackSnorm4x10_10_10_2(int four_packed_values) {
  int4 unpackedInt;
  unpackedInt.x = BitfieldExtract(four_packed_values, 0, 10);
  unpackedInt.y = BitfieldExtract(four_packed_values, 10, 10);
  unpackedInt.z = BitfieldExtract(four_packed_values, 20, 10);
  unpackedInt.w = BitfieldExtract(four_packed_values, 30, 2);

  // xyz is -511-511 w is -1-1
  float4 unpacked = float4(unpackedInt);
  // convert all to -1-1
  unpacked.xyz *= 1.0/511.0;

  return unpacked;
}

uint PackSnorm4x10_10_10_2(float4 snorms) {
  static const float3 range = 511.0;
  float4 scaled = 0.0;
  scaled.xyz = snorms.xyz * range; // Convert from -1.0 -> 1.0 to -511.0 -> 511.0
  scaled.xyz = clamp(scaled.xyz, -range, range);
  scaled.xyz = round(scaled.xyz); // Round to nearest int
  scaled.w = clamp(scaled.w, -1.0, 1.0);
  scaled.w = round(scaled.w);

  // now convert from 16 bit to 10 bits, and pack into 32 bits
  int4 integers = int4(scaled);
  uint result = 0;
  result = BitfieldInsert(result, uint(integers.x), 0, 10);
  result = BitfieldInsert(result, uint(integers.y), 10, 10);
  result = BitfieldInsert(result, uint(integers.z), 20, 10);
  result = BitfieldInsert(result, uint(integers.w), 30, 2);

  return result;
}

// Takes 4 "raw, packed" bytes in a 10/10/10/2 format as a signed 32 bit integer (4 bytes).
// The 2 bits is used as a "bonus scale".
// Returns a 3 component (x,y,z) float vector
float3 UnpackVector_10_10_10_2(int packed_value) {
  // bonus scale is still a unorm, if I convert it to an snorm, I lose one value.
  // that does mean I can't use the hardware to convert this format though, it has
  // to be unpacked by hand. If you do have hardware 10_10_10_2 conversion, it may
  // be better to just sample twice? once as unorm, once as snorm.
  uint bonusScaleIndex = uint(packed_value >> 30 & 0x03);

  const float bonus_scale_lookup[4] = {1.0f, 0.5f, 0.25f, 0.125f};
  const float bonus_scale = bonus_scale_lookup[bonusScaleIndex];

  int3 unpackedInt;
  unpackedInt.x = bitfieldExtract10(packed_value, 0);
  unpackedInt.y = bitfieldExtract10(packed_value, 10);
  unpackedInt.z = bitfieldExtract10(packed_value, 20);

  float3 unpacked = float3(unpackedInt);
  // convert all to -1 to 1
  const float inv511 = 1.0 / 511.0;
  unpacked *= float3(inv511, inv511, inv511);

  unpacked = unpacked * bonus_scale;

  return unpacked;
}

float3 UnpackVector_10_10_10_2(in ByteAddressBuffer data_buffer, int address) {
  return UnpackVector_10_10_10_2(data_buffer.Load(address));
}

float4 UnpackSnorm4x10_10_10_2(in ByteAddressBuffer data_buffer, int address) {
  const int packed_value = data_buffer.Load(address);
  return UnpackSnorm4x10_10_10_2(packed_value);
}

float3 UnpackSnorm3x10_10_10_2(in ByteAddressBuffer data_buffer, int address) {
  return UnpackSnorm4x10_10_10_2(data_buffer, address).xyz;
}

// 3x 32 bit uint -> 3x 32 bit float
float3 UnpackFloat3x32(in ByteAddressBuffer data_buffer, int address) {
  const uint3 packed_data = data_buffer.Load3(address);
  return asfloat(packed_data);
}

// 4x 32 bit uint -> 4x 32 bit float
float4 UnpackFloat4x32(in ByteAddressBuffer data_buffer, int address) {
  const uint4 packed_data = data_buffer.Load4(address);
  return asfloat(packed_data);
}

// 16x 32 bit uint -> 32 bit float4x4
float4x4 UnpackFloat16x32(in ByteAddressBuffer data_buffer, int address) {
  float4 r0 = UnpackFloat4x32(data_buffer, address);
  float4 r1 = UnpackFloat4x32(data_buffer, address + 16);
  float4 r2 = UnpackFloat4x32(data_buffer, address + 32);
  float4 r3 = UnpackFloat4x32(data_buffer, address + 48);

  return float4x4(r0, r1, r2, r3);
}

// 2x 32 bit uint -> 3x 16 bit "half floats"
float3 UnpackHalf3x16(in ByteAddressBuffer data_buffer, int address) {
  uint2 packed_data = data_buffer.Load2(address);
  float2 xy = UnpackHalf2x16(packed_data.x);
  float z = UnpackHalf2x16(packed_data.y).x;

  return float3(xy, z);
}

// 2x 32 bit uint -> 4x 16 bit "half floats"
float4 UnpackHalf4x16(in ByteAddressBuffer data_buffer, int address) {
  uint2 packed_data = data_buffer.Load2(address);
  float2 xy = UnpackHalf2x16(packed_data.x);
  float2 zw = UnpackHalf2x16(packed_data.y);

  return float4(xy, zw);
}

// 2x 32 bit uint -> 3x 16-bit unsigned int
uint3 UnpackUint3x16(in ByteAddressBuffer data_buffer, int address) {
  uint2 packed_data = data_buffer.Load2(address);
  float2 xy = UnpackUint2x16(packed_data.x);
  float z = UnpackUint2x16(packed_data.y).x;

  return float3(xy, z);
}

// 2x 32 bit uint -> 4x 16-bit unsigned int
uint4 UnpackUint4x16(in ByteAddressBuffer data_buffer, int address) {
  uint2 packed_data = data_buffer.Load2(address);
  float2 xy = UnpackUint2x16(packed_data.x);
  float2 zw = UnpackUint2x16(packed_data.y);

  return float4(xy, zw);
}

// 2x 32-bit uint -> 3x 16-bit unsigned normalized
float3 UnpackUnorm3x16(in ByteAddressBuffer data_buffer, int address) {
  uint2 packed_data = data_buffer.Load2(address);
  float2 xy = UnpackUnorm2x16(packed_data.x);
  float z = UnpackUnorm2x16(packed_data.y).x;

  return float3(xy, z);
}

// 2x 32-bit uint -> 4x 16-bit unsigned normalized
float4 UnpackUnorm4x16(in ByteAddressBuffer data_buffer, int address) {
  uint2 packed_data = data_buffer.Load2(address);
  float2 xy = UnpackUnorm2x16(packed_data.x);
  float2 zw = UnpackUnorm2x16(packed_data.y);

  return float4(xy, zw);
}

// 1x 32-bit uint -> 3x 8-bit unsigned int
uint3 UnpackUint3x8(in ByteAddressBuffer data_buffer, int address) {
  return UnpackUint4x8(data_buffer.Load(address)).xyz;
}

// 1x 32 bit uint -> 4x 8 bit unsigned normalized
float4 UnpackUint4x8(in ByteAddressBuffer data_buffer, int address) {
  return UnpackUint4x8(data_buffer.Load(address));
}


// 1x 32-bit uint -> 3x 8-bit unsigned normalized
float3 UnpackUnorm3x8(in ByteAddressBuffer data_buffer, int address) {
  return UnpackUnorm4x8(data_buffer.Load(address)).xyz;
}

// 1x 32-bit uint -> 4x 8-bit unsigned normalized
float4 UnpackUnorm4x8(in ByteAddressBuffer data_buffer, int address) {
  return UnpackUnorm4x8(data_buffer.Load(address));
}

#endif
