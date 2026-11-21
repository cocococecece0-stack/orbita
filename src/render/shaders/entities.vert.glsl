#version 300 es
in vec2 aCorner;
in vec4 iPosSizeRot;
in vec4 iColor;
in vec4 iMeta;
uniform vec2 uResolution;
uniform vec2 uShake;
out vec2 vUv;
out vec4 vColor;
out vec4 vMeta;

void main() {
  float c = cos(iPosSizeRot.w);
  float s = sin(iPosSizeRot.w);
  vec2 local = mat2(c, -s, s, c) * (aCorner * iPosSizeRot.z);
  vec2 pos = iPosSizeRot.xy + local + uShake;
  vec2 clip = pos / uResolution * 2.0 - 1.0;
  clip.y = -clip.y;
  gl_Position = vec4(clip, 0.0, 1.0);
  vUv = aCorner;
  vColor = iColor;
  vMeta = iMeta;
}
