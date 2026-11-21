import vertexSource from './shaders/fullscreen.vert.glsl?raw';
import planetSource from './shaders/planet.frag.glsl?raw';
import { readTheme, type ThemeSnapshot, type Vec3 } from '../theme/theme';
import type { GameLoop } from '../core/GameLoop';

export interface SafeInsets {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

export interface PlanetRenderState {
  time: number;
  pulse: number;
  crack: number;
  shake: number;
}

const UNIFORM_NAMES = [
  'uCenter', 'uRadius', 'uTime', 'uPulse', 'uCrack', 'uShake',
  'uGrain', 'uFxIntensity', 'uAccent', 'uDanger', 'uVoid',
] as const;

type UniformName = (typeof UNIFORM_NAMES)[number];

/** Минимальный WebGL2-рендер: фуллскрин-треугольник + шейдер планеты. */
export class Renderer {
  private readonly canvas: HTMLCanvasElement;
  private gl: WebGL2RenderingContext;
  private program: WebGLProgram | null = null;
  private vao: WebGLVertexArrayObject | null = null;
  private uniforms: Partial<Record<UniformName, WebGLUniformLocation | null>> = {};

  private theme: ThemeSnapshot = readTheme();
  private loop: GameLoop | null = null;

  private dpr = 1;
  private cssWidth = 1;
  private cssHeight = 1;
  private insets: SafeInsets = { top: 0, right: 0, bottom: 0, left: 0 };
  private contextLost = false;
  private view = { width: 1, height: 1 };

  constructor(canvas: HTMLCanvasElement) {
    this.canvas = canvas;
    const gl = canvas.getContext('webgl2', {
      alpha: true,
      antialias: false,
      depth: false,
      stencil: false,
      premultipliedAlpha: false,
      powerPreference: 'high-performance',
    });
    if (!gl) throw new Error('WebGL2 недоступен на этом устройстве');
    this.gl = gl;

    canvas.addEventListener('webglcontextlost', (event) => {
      event.preventDefault();
      this.contextLost = true;
    });
    canvas.addEventListener('webglcontextrestored', () => {
      this.initGL();
      this.contextLost = false;
    });

    this.initGL();
  }

  attach(loop: GameLoop): void {
    this.loop = loop;
  }

  /** Доступ к GL для инстанс-рендера сущностей. */
  getGl(): WebGL2RenderingContext {
    return this.gl;
  }

  /** Текущий размер framebuffer в физических пикселях. */
  getView(): { width: number; height: number } {
    this.view.width = this.canvas.width;
    this.view.height = this.canvas.height;
    return this.view;
  }

  getLoop(): GameLoop | null {
    return this.loop;
  }

  refreshTheme(): void {
    this.theme = readTheme();
  }

  resize(cssWidth: number, cssHeight: number, insets: SafeInsets): void {
    this.cssWidth = Math.max(1, cssWidth);
    this.cssHeight = Math.max(1, cssHeight);
    this.insets = insets;
    this.dpr = Math.min(window.devicePixelRatio || 1, 3);

    const w = Math.max(1, Math.floor(this.cssWidth * this.dpr));
    const h = Math.max(1, Math.floor(this.cssHeight * this.dpr));
    if (this.canvas.width !== w || this.canvas.height !== h) {
      this.canvas.width = w;
      this.canvas.height = h;
    }
    this.gl.viewport(0, 0, w, h);
  }

  /** Кадр. Вызывается из GameLoop.onRender(alpha, elapsed). */
  draw(state: PlanetRenderState): void {
    if (this.contextLost || !this.program || !this.vao) return;

    const gl = this.gl;
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);

    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    gl.useProgram(this.program);
    gl.bindVertexArray(this.vao);

    const innerW = this.cssWidth - this.insets.left - this.insets.right;
    const innerH = this.cssHeight - this.insets.top - this.insets.bottom;

    const cxCss = this.insets.left + innerW * (0.5 + this.theme.offsetX);
    const cyCss = this.insets.top + innerH * (0.5 + this.theme.offsetY);

    // Флип Y: CSS top-left -> GL bottom-left.
    const cx = cxCss * this.dpr;
    const cy = (this.cssHeight - cyCss) * this.dpr;
    const radius = Math.min(innerW, innerH) * this.theme.targetRadius * this.dpr;

    this.f2('uCenter', cx, cy);
    this.f1('uRadius', radius);
    this.f1('uTime', state.time);
    this.f1('uPulse', state.pulse);
    this.f1('uCrack', state.crack);
    this.f1('uShake', state.shake * this.dpr);
    this.f1('uGrain', this.theme.grain);
    this.f1('uFxIntensity', this.theme.fxIntensity);
    this.v3('uAccent', this.theme.accent);
    this.v3('uDanger', this.theme.danger);
    this.v3('uVoid', this.theme.voidColor);

    gl.drawArrays(gl.TRIANGLES, 0, 3);
    gl.bindVertexArray(null);
  }

  private initGL(): void {
    const gl = this.gl;
    const vs = this.compile(gl.VERTEX_SHADER, vertexSource);
    const fs = this.compile(gl.FRAGMENT_SHADER, planetSource);
    const program = gl.createProgram();
    if (!program) throw new Error('Не удалось создать WebGL program');
    gl.attachShader(program, vs);
    gl.attachShader(program, fs);
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
      const log = gl.getProgramInfoLog(program) ?? 'unknown link error';
      gl.deleteProgram(program);
      throw new Error(`Линковка шейдера не удалась: ${log}`);
    }
    gl.deleteShader(vs);
    gl.deleteShader(fs);
    this.program = program;

    this.uniforms = {};
    for (const name of UNIFORM_NAMES) {
      this.uniforms[name] = gl.getUniformLocation(program, name);
    }

    const vao = gl.createVertexArray();
    const buffer = gl.createBuffer();
    if (!vao || !buffer) throw new Error('Не удалось создать VAO/VBO');
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(
      gl.ARRAY_BUFFER,
      new Float32Array([-1, -1, 3, -1, -1, 3]),
      gl.STATIC_DRAW,
    );
    const loc = gl.getAttribLocation(program, 'aPos');
    gl.enableVertexAttribArray(loc);
    gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);
    gl.bindVertexArray(null);
    this.vao = vao;
  }

  private compile(type: number, source: string): WebGLShader {
    const gl = this.gl;
    const shader = gl.createShader(type);
    if (!shader) throw new Error('Не удалось создать шейдер');
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
      const log = gl.getShaderInfoLog(shader) ?? 'unknown compile error';
      gl.deleteShader(shader);
      throw new Error(`Компиляция шейдера не удалась: ${log}`);
    }
    return shader;
  }

  private f1(name: UniformName, value: number): void {
    const loc = this.uniforms[name];
    if (loc) this.gl.uniform1f(loc, value);
  }

  private f2(name: UniformName, x: number, y: number): void {
    const loc = this.uniforms[name];
    if (loc) this.gl.uniform2f(loc, x, y);
  }

  private v3(name: UniformName, v: Vec3): void {
    const loc = this.uniforms[name];
    if (loc) this.gl.uniform3f(loc, v[0], v[1], v[2]);
  }
}
