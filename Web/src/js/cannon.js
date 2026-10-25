// The game's own cannon, drawn live in the browser (#949): the meshes Tools/WindingCheck --export-cannon reads out of the
// game's CannonRig (cannon.bin) and the figures that join them (cannon.json), so the barrel, the carriage, the omni wheels
// and their rollers are the game's and none of their numbers is copied here. Raw WebGL2, no library: one program for the
// lit parts, the glass drawn last over them, a ball now and then out of the muzzle.
//
// Matrices are the game's: row vectors, the translation in the fourth row, sixteen numbers in row order. Uploaded as they are,
// WebGL reads them transposed, which is exactly what a column-vector shader needs, so `mul(a, b)` here means what a * b
// means in the game: a first, then b.
//
// It runs only while it is on screen and the tab is shown, at most two device pixels a CSS pixel, and under
// prefers-reduced-motion it draws one frame and stops. Without WebGL2 the canvas is left to its poster.

const canvas = document.querySelector("canvas.cannon-canvas");
const gl = canvas && canvas.getContext("webgl2", { antialias: true, alpha: true, premultipliedAlpha: true });
if (canvas && gl) start(canvas, gl).catch((e) => console.warn("[cannon]", e));

async function start(canvas, gl) {
  const base = canvas.dataset.src || "/cannon/";
  const [meta, bin] = await Promise.all([
    fetch(base + "cannon.json").then((r) => r.json()),
    fetch(base + "cannon.bin").then((r) => r.arrayBuffer()),
  ]);
  const f = meta.figures;

  // --- The program
  const program = link(gl, `#version 300 es
layout(location = 0) in vec4 aPos;
layout(location = 1) in vec4 aNormal;
uniform mat4 uModel, uViewProj;
uniform float uScale;
out vec3 vWorld;
out vec3 vNormal;
void main() {
  vec4 world = uModel * vec4(aPos.xyz * uScale, 1.0);
  vWorld = world.xyz;
  vNormal = mat3(uModel) * aNormal.xyz;
  gl_Position = uViewProj * world;
}`, `#version 300 es
precision highp float;
in vec3 vWorld;
in vec3 vNormal;
uniform vec3 uColor, uEmissive, uEye, uShadowAt;
uniform float uSky, uAlpha, uFade;
out vec4 outColor;

// The light: a warm key from up and to the left, a cool fill from the other side, the sky over it all and a floor under it,
// in the page's own violet
const vec3 KEY_DIR = normalize(vec3(-0.55, 0.75, 0.45));
const vec3 KEY = vec3(1.15, 1.03, 0.9);
const vec3 FILL_DIR = normalize(vec3(0.7, 0.35, -0.6));
const vec3 FILL = vec3(0.22, 0.27, 0.5);
const vec3 SKY = vec3(0.16, 0.13, 0.25);
const vec3 FLOOR = vec3(0.04, 0.025, 0.06);

vec3 aces(vec3 x) { return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0); }

void main() {
  vec3 n = normalize(vNormal);
  if (!gl_FrontFacing) n = -n;
  vec3 v = normalize(uEye - vWorld);
  float key = max(dot(n, KEY_DIR), 0.0), fill = max(dot(n, FILL_DIR), 0.0);
  vec3 ambient = mix(FLOOR, SKY, n.y * 0.5 + 0.5) * 0.6;
  vec3 lit = uColor * (ambient + KEY * key + FILL * fill * 0.6);

  // Polished where the game polishes: a highlight off each light and the sky's reflection, by the part's own sky figure
  float fresnel = 0.04 + 0.96 * pow(1.0 - max(dot(n, v), 0.0), 5.0);
  vec3 h = normalize(KEY_DIR + v);
  vec3 spec = KEY * pow(max(dot(n, h), 0.0), 60.0) * key * (0.25 + uSky);
  vec3 r = reflect(-v, n);
  vec3 env = mix(FLOOR, SKY * 0.9, smoothstep(-0.2, 0.6, r.y));
  lit += spec + env * uSky * (0.35 + 0.65 * fresnel);
  lit += uEmissive;

  // The ground alone (uFade, its radius): darker under the gun, and fading out to the page at its rim
  float alpha = uAlpha;
  if (uFade > 0.0) {
    lit *= mix(0.35, 1.0, smoothstep(0.6, 3.6, distance(vWorld.xz, uShadowAt.xz)));
    alpha *= 1.0 - smoothstep(uFade * 0.45, uFade, length(vWorld.xz));
  }

  vec3 c = pow(aces(lit), vec3(1.0 / 2.2));
  outColor = vec4(c * alpha, alpha);
}`);
  const u = uniforms(gl, program, ["uModel", "uViewProj", "uScale", "uColor", "uEmissive", "uEye", "uSky", "uAlpha", "uFade", "uShadowAt"]);

  // --- The meshes: one vertex buffer and one index buffer for every part, each part's attributes pointed at its own first vertex
  const vbo = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
  gl.bufferData(gl.ARRAY_BUFFER, new Uint8Array(bin, 0, meta.indexOffset), gl.STATIC_DRAW);
  const ibo = gl.createBuffer();
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ibo);
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint8Array(bin, meta.indexOffset), gl.STATIC_DRAW);

  const parts = {};
  for (const [name, p] of Object.entries(meta.parts)) {
    const vao = gl.createVertexArray();
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ibo);
    const at = p.vertexStart * meta.vertexStride;
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 4, gl.SHORT, true, meta.vertexStride, at);
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 4, gl.BYTE, true, meta.vertexStride, at + 8);
    parts[name] = { vao, count: p.indexCount, offset: p.indexStart * 2, material: meta.materials[name], scale: meta.positionScale };
  }
  parts.ball = sphere(gl, f.ballRadius, 24, 16);
  const GROUND_RADIUS = 11, GLASS_THINNING = 0.45;
  parts.ground = disc(gl, GROUND_RADIUS, 72);
  gl.bindVertexArray(null);

  // --- The motion: round a circle, now driving along it on the wheels, now crabbing across it on the rollers (the reason
  // a gun has omni wheels), the barrel breathing up and down and firing every few seconds
  const RADIUS = 4.2, SPEED = 1.5, FIRE_EVERY = 3.4, SHOT_SPEED = 13, GRAVITY = 9.81, SHOT_LIFE = 2.2;
  const pivotHeight = f.axleDrop + f.wheelRadius;
  const ballColors = meta.balls.map((c) => c.map((x) => Math.pow(x, 2.2)));
  const deal = () => Math.floor(Math.random() * ballColors.length);
  const state = { s: 0, roll: 0, spin: 0, recoil: 0, sinceShot: 1.2, queue: Array.from({ length: f.magazine }, deal), shots: [] };

  function step(dt, t) {
    // How far round, and the facing against the path: square to it (driving) or along the radius (crabbing), easing between
    state.s += SPEED * dt;
    const theta = state.s / RADIUS;
    const crab = 0.5 - 0.5 * Math.cos(t * 0.45);
    const along = [Math.cos(theta), 0, -Math.sin(theta)];
    const facing = Math.PI * 0.5 * crab;
    const heading = rotateY(along, facing);
    state.roll -= SPEED * Math.cos(facing) * dt / f.wheelRadius;
    state.spin -= SPEED * Math.sin(facing) * dt / f.rollerRadius;

    state.recoil = Math.max(0, state.recoil - f.recoilDecay * dt);
    state.sinceShot += dt;
    const elevation = 0.42 + 0.16 * Math.sin(t * 0.7);
    const aim = norm(add(scale(heading, Math.cos(elevation)), [0, Math.sin(elevation), 0]));

    const ground = [RADIUS * Math.sin(theta), 0, RADIUS * Math.cos(theta)];
    const kick = state.recoil * state.recoil;
    const shove = f.carriageRecoilBack * 4 * kick * (1 - kick);
    const pivot = add(add(ground, [0, pivotHeight, 0]), scale(heading, -shove));
    const carriage = world(pivot, heading, [0, 1, 0]);
    const barrel = world(add(pivot, scale(aim, -f.recoilBack * kick)), aim, [0, 1, 0]);

    if (state.sinceShot >= FIRE_EVERY) {
      state.sinceShot = 0;
      state.recoil = 1;
      const muzzle = add(pivot, scale(aim, f.muzzle));
      //The front round leaves, the queue moves up and a fresh one is dealt at the back, as the magazine does
      state.shots.push({ p: muzzle, v: scale(aim, SHOT_SPEED), age: 0, color: ballColors[state.queue.shift()] });
      state.queue.push(deal());
      //Heard, where the page has sound on (#950, js/sound.js): from where the gun is across the canvas
      window.dispatchEvent(new CustomEvent("bs3d:shot", { detail: { x: Math.max(-1, Math.min(1, muzzle[0] / RADIUS)) } }));
    }
    for (const shot of state.shots) {
      shot.age += dt;
      shot.v[1] -= GRAVITY * dt;
      shot.p = add(shot.p, scale(shot.v, dt));
    }
    state.shots = state.shots.filter((s) => s.age < SHOT_LIFE);
    return { carriage, barrel, pivot };
  }

  // --- A frame
  const eye = [0, 9.2, 21.5], target = [0, 1.2, 0];
  function draw(t, pose) {
    resize(canvas, gl);
    gl.viewport(0, 0, canvas.width, canvas.height);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
    gl.enable(gl.DEPTH_TEST);
    gl.disable(gl.BLEND);
    // The game winds its meshes clockwise seen from outside (its CLAUDE.md's "Triangle winding"): those are the front faces
    // here, so gl_FrontFacing is true outside and the normal is turned only for an inner face (the bore) seen through
    gl.frontFace(gl.CW);
    gl.useProgram(program);

    const swing = 0.32 * Math.sin(t * 0.11);
    const lens = rotateY(eye, swing);
    const viewProj = mul(lookAt(lens, target, [0, 1, 0]), perspective(0.55, canvas.width / canvas.height, 0.5, 80));
    gl.uniformMatrix4fv(u.uViewProj, false, viewProj);
    gl.uniform3fv(u.uEye, lens);

    // The stone it drives on, shaded under the gun and fading into the page
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
    gl.uniform1f(u.uFade, GROUND_RADIUS);
    gl.uniform3fv(u.uShadowAt, pose.pivot);
    drawPart(parts.ground, IDENTITY, [0.05, 0.038, 0.065], 0.04, 1, [0, 0, 0]);
    gl.uniform1f(u.uFade, 0);
    gl.disable(gl.BLEND);

    // The carriage, the wheels at their place on the axle, rolled, and the rollers on their seats, spun
    drawPart(parts.carriage, pose.carriage);
    for (const side of [-1, 1]) {
      const wheel = rotX(state.roll);
      wheel[12] = side * f.wheelTrack;
      wheel[13] = -f.axleDrop;
      const wheelWorld = mul(wheel, pose.carriage);
      drawPart(parts.wheel, wheelWorld);
      const spin = rotX(state.spin);
      for (const seat of f.rollerSeats) drawPart(parts.roller, mul(mul(spin, seat), wheelWorld));
    }

    // The barrel, and its muzzle collar in the colour of the round about to fire, as the game lights it
    drawPart(parts.barrel, pose.barrel);
    const next = ballColors[state.queue[0]];
    const glow = 0.8 + 1.6 * state.recoil;
    drawPart(parts.collar, mul(rotX(Math.PI / 2), pose.barrel), null, null, 1, next.map((c) => c * glow));

    // The balls in the air
    for (const shot of state.shots) {
      const m = translation(shot.p);
      const fade = Math.min(1, (SHOT_LIFE - shot.age) / 0.4);
      drawPart(parts.ball, m, shot.color, 0.6, 1, shot.color.map((c) => c * 0.08 * fade));
    }

    // The loaded queue on the bore's axis behind the glass, the front round in the collar's colour and the rest behind it,
    // as the game's magazine holds them
    for (let i = 0; i < f.magazine; i++) {
      const color = ballColors[state.queue[i]];
      const seat = mul(translation([0, 0, -(f.pivotToFrontBall - i * f.magazineSpacing)]), pose.barrel);
      drawPart(parts.ball, seat, color, 0.6, 1, [0, 0, 0]);
    }

    // And last, the glazed window over the loading slot, translucent
    gl.enable(gl.BLEND);
    gl.blendFunc(gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
    gl.depthMask(false);
    // Thinner than the game's 0.84: the game's glass is lit by reflections this program does not draw, and at its own alpha
    // the pane here hid the queue the game shows through it
    drawPart(parts.glass, pose.barrel, null, null, parts.glass.material.alpha * GLASS_THINNING);
    gl.depthMask(true);
  }

  function drawPart(part, model, color, sky, alpha, emissive) {
    const m = part.material || {};
    gl.bindVertexArray(part.vao);
    gl.uniformMatrix4fv(u.uModel, false, model);
    gl.uniform1f(u.uScale, part.scale || 1);
    gl.uniform3fv(u.uColor, color || m.color || [0.5, 0.5, 0.5]);
    gl.uniform1f(u.uSky, sky ?? m.sky ?? 0.2);
    gl.uniform1f(u.uAlpha, alpha ?? m.alpha ?? 1);
    gl.uniform3fv(u.uEmissive, emissive || [0, 0, 0]);
    gl.drawElements(gl.TRIANGLES, part.count, part.type || gl.UNSIGNED_SHORT, part.offset);
  }

  // --- The clock: only while it is seen
  // ?cannon-t=<seconds> draws the pose at that moment and stops: how a page without a display (a headless capture) is checked
  const pinned = Number(new URLSearchParams(location.search).get("cannon-t"));
  const still = matchMedia("(prefers-reduced-motion: reduce)").matches || pinned > 0;
  let visible = true, last = 0, time = 4, running = false;
  if (pinned > 0) for (let t = 0; t < pinned; t += 1 / 60) step(1 / 60, (time = t));
  const frame = (now) => {
    running = false;
    if (!visible || document.hidden) return;
    const dt = last ? Math.min((now - last) / 1000, 0.05) : 0;
    last = now;
    time += dt;
    draw(time, step(dt, time));
    if (!still) loop();
  };
  const loop = () => { if (!running) { running = true; requestAnimationFrame(frame); } };
  new IntersectionObserver((entries) => {
    visible = entries.some((e) => e.isIntersecting);
    last = 0;
    if (visible) loop();
  }).observe(canvas);
  document.addEventListener("visibilitychange", () => { last = 0; if (!document.hidden) loop(); });
  canvas.classList.add("live");
  draw(time, step(0, time));
  if (!still) loop();
}

// --- Geometry the page makes itself: the ball and the ground
function sphere(gl, radius, slices, stacks) {
  const pos = [], idx = [];
  for (let j = 0; j <= stacks; j++) {
    const v = j / stacks * Math.PI;
    for (let i = 0; i <= slices; i++) {
      const w = i / slices * Math.PI * 2;
      const n = [Math.sin(v) * Math.cos(w), Math.cos(v), Math.sin(v) * Math.sin(w)];
      pos.push(n[0] * radius, n[1] * radius, n[2] * radius, n[0], n[1], n[2]);
    }
  }
  for (let j = 0; j < stacks; j++)
    for (let i = 0; i < slices; i++) {
      const a = j * (slices + 1) + i, b = a + slices + 1;
      idx.push(a, b, a + 1, a + 1, b, b + 1);
    }
  return floatMesh(gl, pos, idx);
}

function disc(gl, radius, segments) {
  const pos = [0, 0, 0, 0, 1, 0], idx = [];
  for (let i = 0; i <= segments; i++) {
    const a = i / segments * Math.PI * 2;
    pos.push(Math.cos(a) * radius, 0, Math.sin(a) * radius, 0, 1, 0);
    if (i > 0) idx.push(0, i, i + 1);
  }
  return floatMesh(gl, pos, idx);
}

function floatMesh(gl, pos, idx) {
  const vao = gl.createVertexArray();
  gl.bindVertexArray(vao);
  const vbo = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(pos), gl.STATIC_DRAW);
  gl.enableVertexAttribArray(0);
  gl.vertexAttribPointer(0, 3, gl.FLOAT, false, 24, 0);
  gl.enableVertexAttribArray(1);
  gl.vertexAttribPointer(1, 3, gl.FLOAT, false, 24, 12);
  const ibo = gl.createBuffer();
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ibo);
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint16Array(idx), gl.STATIC_DRAW);
  gl.bindVertexArray(null);
  return { vao, count: idx.length, offset: 0, scale: 1 };
}

// --- WebGL plumbing
function link(gl, vs, fs) {
  const program = gl.createProgram();
  for (const [type, src] of [[gl.VERTEX_SHADER, vs], [gl.FRAGMENT_SHADER, fs]]) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, src);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(shader));
    gl.attachShader(program, shader);
  }
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
  return program;
}

function uniforms(gl, program, names) {
  const out = {};
  for (const n of names) out[n] = gl.getUniformLocation(program, n);
  return out;
}

function resize(canvas, gl) {
  const ratio = Math.min(window.devicePixelRatio || 1, 2);
  const w = Math.max(1, Math.round(canvas.clientWidth * ratio)), h = Math.max(1, Math.round(canvas.clientHeight * ratio));
  if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; }
}

// --- The game's matrices (row vectors, translation in the fourth row, row order)
const IDENTITY = new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1]);

function mul(a, b) {
  const r = new Float32Array(16);
  for (let i = 0; i < 4; i++)
    for (let j = 0; j < 4; j++)
      r[i * 4 + j] = a[i * 4] * b[j] + a[i * 4 + 1] * b[4 + j] + a[i * 4 + 2] * b[8 + j] + a[i * 4 + 3] * b[12 + j];
  return r;
}

function rotX(t) {
  const c = Math.cos(t), s = Math.sin(t);
  return new Float32Array([1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1]);
}

function translation(p) { return new Float32Array([1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, p[0], p[1], p[2], 1]); }

// Matrix.CreateWorld: the right, the up, the backward, the position
function world(p, forward, up) {
  const z = norm(forward);
  const x = norm(cross(z, up));
  const y = norm(cross(x, z));
  return new Float32Array([x[0], x[1], x[2], 0, y[0], y[1], y[2], 0, -z[0], -z[1], -z[2], 0, p[0], p[1], p[2], 1]);
}

// Matrix.CreateLookAt
function lookAt(eye, target, up) {
  const z = norm(sub(eye, target));
  const x = norm(cross(up, z));
  const y = cross(z, x);
  return new Float32Array([x[0], y[0], z[0], 0, x[1], y[1], z[1], 0, x[2], y[2], z[2], 0, -dot(x, eye), -dot(y, eye), -dot(z, eye), 1]);
}

// A right-handed perspective with WebGL's -1..1 depth
function perspective(fov, aspect, near, far) {
  const f = 1 / Math.tan(fov / 2);
  return new Float32Array([f / aspect, 0, 0, 0, 0, f, 0, 0, 0, 0, (far + near) / (near - far), -1, 0, 0, 2 * far * near / (near - far), 0]);
}

function rotateY(v, a) { const c = Math.cos(a), s = Math.sin(a); return [v[0] * c + v[2] * s, v[1], -v[0] * s + v[2] * c]; }
function add(a, b) { return [a[0] + b[0], a[1] + b[1], a[2] + b[2]]; }
function sub(a, b) { return [a[0] - b[0], a[1] - b[1], a[2] - b[2]]; }
function scale(a, s) { return [a[0] * s, a[1] * s, a[2] * s]; }
function dot(a, b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
function cross(a, b) { return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]; }
function norm(a) { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; }
