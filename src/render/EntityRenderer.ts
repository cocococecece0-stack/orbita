import vert from './shaders/entities.vert.glsl?raw';
import frag from './shaders/entities.frag.glsl?raw';

const STRIDE = 12;
const MAX = 512;

/** Инстанс-рендер: один VBO, один draw call на все сущности, ноль аллокаций в кадре. */
export class EntityRenderer {
  private gl: WebGL2RenderingContext;
  private program: WebGLProgram | null = null;
  private vao: WebGLVertexArrayObject | null = null;
  private instances = new Float32Array(MAX * STRIDE);
  private buffer: WebGLBuffer | null = null;
  private uRes: WebGLUniformLocation | null = null;
  private uShake: WebGLUniformLocation | null = null;

  constructor(gl: WebGL2RenderingContext) {
    this.gl = gl;
    this.init();
  }

  private compile(type: number, src: string): WebGLShader {
    const gl = this.gl;
    const sh = gl.createShader(type);
    if (!sh) throw new Error('shader alloc failed');
    gl.shaderSource(sh, src);
    gl.compileShader(sh);
    if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) {
      throw new Error(gl.getShaderInfoLog(sh) ?? 'compile error');
    }
    return sh;
  }

  private init(): void {
    const gl = this.gl;
    const vs = this.compile(gl.VERTEX_SHADER, vert);
    const fs = this.compile(gl.FRAGMENT_SHADER, frag);
    const p = gl.createProgram();
    if (!p) throw new Error('program alloc failed');
    gl.attachShader(p, vs);
    gl.attachShader(p, fs);
    gl.linkProgram(p);
    if (!gl.getProgramParameter(p, gl.LINK_STATUS)) {
      throw new Error(gl.getProgramInfoLog(p) ?? 'link error');
    }
    gl.deleteShader(vs);
    gl.deleteShader(fs);
    this.program = p;
    this.uRes = gl.getUniformLocation(p, 'uResolution');
    this.uShake = gl.getUniformLocation(p, 'uShake');

    const vao = gl.createVertexArray();
    const quad = gl.createBuffer();
    const inst = gl.createBuffer();
    if (!vao || !quad || !inst) throw new Error('buffer alloc failed');
    this.buffer = inst;

    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, quad);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-0.5, -0.5, 0.5, -0.5, -0.5, 0.5, 0.5, 0.5]), gl.STATIC_DRAW);
    const corner = gl.getAttribLocation(p, 'aCorner');
    gl.enableVertexAttribArray(corner);
    gl.vertexAttribPointer(corner, 2, gl.FLOAT, false, 0, 0);

    gl.bindBuffer(gl.ARRAY_BUFFER, inst);
    gl.bufferData(gl.ARRAY_BUFFER, this.instances.byteLength, gl.DYNAMIC_DRAW);

    const attributes: Array<[string, number, number]> = [
      ['iPosSizeRot', 4, 0],
      ['iColor', 4, 16],
      ['iMeta', 4, 32],
    ];
    for (const [name, size, offset] of attributes) {
      const loc = gl.getAttribLocation(p, name);
      gl.enableVertexAttribArray(loc);
      gl.vertexAttribPointer(loc, size, gl.FLOAT, false, STRIDE * 4, offset);
      gl.vertexAttribDivisor(loc, 1);
    }
    gl.bindVertexArray(null);
    this.vao = vao;
  }

  get bufferData(): Float32Array { return this.instances; }
  get capacity(): number { return MAX; }

  draw(count: number, width: number, height: number, shakeX: number, shakeY: number): void {
    if (!count || !this.program || !this.vao) return;
    const gl = this.gl;
    gl.useProgram(this.program);
    gl.bindVertexArray(this.vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, this.buffer);
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, this.instances, 0, count * STRIDE);
    if (this.uRes) gl.uniform2f(this.uRes, width, height);
    if (this.uShake) gl.uniform2f(this.uShake, shakeX, shakeY);
    gl.drawArraysInstanced(gl.TRIANGLE_STRIP, 0, 4, count);
    gl.bindVertexArray(null);
  }
}
