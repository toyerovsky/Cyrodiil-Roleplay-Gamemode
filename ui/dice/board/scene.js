// The dice board drawn with three.js: the board, the dice in their sets and the throw. The numbers always come from the
// server, the board only plays them (physics.js lands each die on its number). Ported from RpRoll's dice tray.

import {
  ACESFilmicToneMapping,
  Box3,
  CircleGeometry,
  Clock,
  Color,
  DirectionalLight,
  HemisphereLight,
  Mesh,
  MeshBasicMaterial,
  PCFSoftShadowMap,
  PerspectiveCamera,
  PMREMGenerator,
  Quaternion,
  Raycaster,
  RingGeometry,
  Scene,
  ShadowMaterial,
  Source,
  SRGBColorSpace,
  Vector3,
  WebGLRenderer,
} from "three";
import { RoomEnvironment } from "three/examples/jsm/environments/RoomEnvironment.js";
import { GLTFLoader } from "three/examples/jsm/loaders/GLTFLoader.js";
import boardInfo from "../models/board.json";
import d10json from "../models/dice/d10.json";
import d100json from "../models/dice/d100.json";
import d20json from "../models/dice/d20.json";
import d6json from "../models/dice/d6.json";
import { DIE_SCALE, physicalDice, scaleModel, SHOWN } from "./dice.js";
import { loadModel } from "./models.js";
import { bodyOf, finishThrow, planThrow, restHeight, topFace } from "./physics.js";
import { SETS } from "./sets.js";

const MODELS = {
  d6: scaleModel(d6json, DIE_SCALE),
  d10: scaleModel(d10json, DIE_SCALE),
  d20: scaleModel(d20json, DIE_SCALE),
  d100: scaleModel(d100json, DIE_SCALE),
};

// Seconds the dice are lifted before they fly, however fast the server answers.
const WINDUP_MIN = 0.22;
// Seconds a throw waits for the server's numbers before it settles back.
const ANSWER_WAIT = 6;
const SETTLE_SECONDS = 0.35;
const LANDED_SECONDS = 0.6;
const MAX_FRAME = 1 / 20;
const FRAMES_PER_SECOND = 60;
// The board is about 20 units across, 10.3 from its centre to the bark.
const BOARD_REACH = 10.4;
const TILT = (30 * Math.PI) / 180;
const LOOK = new Vector3(0, 0, 0.35);
const UP = new Vector3(0, 1, 0);
const IDLE_GAP = 2.9;
// A roll shown for something else fills its small frame, a little room left above the far rim for the numbers.
const PRESENT_FILL = 0.92;
const PRESENT_DROP = -0.04;
// A board fitted to its own canvas (fit: "board", the gambling table) fills this much of it, centred, bark and all.
const BOARD_FILL = 0.95;
const FIT_STEPS = 4;
const EDGE_POINTS = 48;
// A picked die rises and glows, a gold ring under it on the board.
const PICKED_LIFT = 0.45;
const PICKED_GLOW = 0xc98a22;

const gltf = new GLTFLoader();

function bytes(base64) {
  const binary = atob(base64);
  const out = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) out[i] = binary.charCodeAt(i);
  return out;
}

const parseGlb = (base64) => new Promise((resolve, reject) => gltf.parse(bytes(base64).buffer, "", resolve, reject));

// Decoded as GLTFLoader decodes, so a swapped texture looks exactly like one the loader made.
const decodePicture = (base64) => createImageBitmap(new Blob([bytes(base64)], { type: "image/webp" }), { colorSpaceConversion: "none" });

// Every die in a set of its own, at random: a set repeats only past twelve dice.
function pickRandomSets(count) {
  const pool = SETS.map((set) => set.id);
  for (let i = pool.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [pool[i], pool[j]] = [pool[j], pool[i]];
  }
  return Array.from({ length: count }, (_, i) => pool[i % pool.length]);
}

// canvas: where the board is drawn. tags: an element the numbers over the landed dice go in. sound: the board's sounds.
// stage(): the part of the canvas the board must fit in, as a DOMRect (between the page's panels), or null for all of it. onLanded(counts,
// values, fresh): a throw came to rest. onTimeout(id): the server never answered. fit: "stage" fits the board into stage()
// as the dice page does, "board" fills the canvas with the board alone.
export function createBoard({ canvas, tags, sound, stage, onLanded, onTimeout, boardModel = "board", fit = "stage" }) {
  const renderer = new WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: "high-performance" });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.outputColorSpace = SRGBColorSpace;
  renderer.toneMapping = ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = PCFSoftShadowMap;
  renderer.setClearColor(0x000000, 0);

  const scene = new Scene();
  const environmentGenerator = new PMREMGenerator(renderer);
  const room = new RoomEnvironment();
  const environment = environmentGenerator.fromScene(room, 0.04);
  scene.environment = environment.texture;
  room.dispose();
  environmentGenerator.dispose();
  scene.environmentIntensity = 0.55;

  const camera = new PerspectiveCamera(30, 1, 0.5, 200);

  // Lit from where the board's own picture was lit, so dice and board agree.
  const key = new DirectionalLight(0xfff1dc, 2.4);
  key.position.set(-9, 26, -9).multiplyScalar(0.6);
  key.castShadow = true;
  key.shadow.mapSize.set(2048, 2048);
  Object.assign(key.shadow.camera, { left: -9, right: 9, top: 9, bottom: -9, near: 1, far: 50 });
  key.shadow.bias = -0.0004;
  key.shadow.normalBias = 0.02;
  key.shadow.radius = 4;
  scene.add(key);
  scene.add(new HemisphereLight(0xfff4e2, 0x3a2a1c, 0.55));

  // The board's light is in its picture, so it is drawn unlit: this catches the dice's shadows on it, brown, as black
  // would turn the wood grey.
  const shadowCatcher = new Mesh(new CircleGeometry(1, 96), new ShadowMaterial({ color: 0x2a1406, opacity: 0.55 }));
  shadowCatcher.rotation.x = -Math.PI / 2;
  shadowCatcher.scale.set(boardInfo.bowl_a * 1.02, boardInfo.bowl_b * 1.02, 1);
  shadowCatcher.position.y = 0.012;
  shadowCatcher.receiveShadow = true;
  scene.add(shadowCatcher);

  const templates = {};
  const setTextures = {};
  let present = false;
  let currentSet = "imperial";
  let board = [];
  let boardKey = "";
  // The dice on the board are the player's, in their set, not a roll shown for something else in a set a die.
  let ownDice = true;
  // The dice last asked for: a set still loading for an older ask is not put on the board after them.
  let placing = 0;
  // The dice last asked for, which a new set dresses.
  let wanted = null;
  let tagData = [];
  const anim = { phase: "idle", id: 0, t: 0, counts: null, values: null, plan: null, throwData: null, from: null, to: null, valueAt: 0, dice: null, impactIndex: 0 };
  const probe = { throws: 0, last: null };
  let disposed = false;
  let sharedRequest = 0;
  let picked = 0;
  const raycaster = new Raycaster();
  // The board's outline, top and bottom, which fit: "board" keeps in view.
  let edge = null;
  let ring = null;

  function template(kind) {
    templates[kind] ??= loadModel(`die-${kind}`)
      .then(parseGlb)
      .then((loaded) => {
        let mesh = null;
        loaded.scene.traverse((node) => {
          if (node.isMesh && !mesh) mesh = node;
        });
        return mesh;
      });
    return templates[kind];
  }

  function texturesOf(set) {
    setTextures[set] ??= loadModel(set).then(async (kinds) => {
      const out = {};
      for (const [kind, maps] of Object.entries(kinds)) {
        out[kind] = {};
        for (const [name, base64] of Object.entries(maps)) out[kind][name] = await decodePicture(base64);
      }
      return out;
    });
    return setTextures[set];
  }

  // A die of the kind in the set: the template's mesh and material with the set's pictures in place. A clone shares the
  // template's picture, so each swapped texture takes a Source of its own.
  async function makeDie(kind, set) {
    const [mesh, textures] = await Promise.all([template(kind), texturesOf(set)]);
    const maps = textures[kind];
    const material = mesh.material.clone();
    const swap = (texture, picture) => {
      const copy = texture.clone();
      copy.source = new Source(picture);
      copy.needsUpdate = true;
      return copy;
    };
    material.map = swap(mesh.material.map, maps.color);
    const orm = swap(mesh.material.roughnessMap, maps.orm);
    material.roughnessMap = orm;
    material.metalnessMap = orm;
    material.normalMap = swap(mesh.material.normalMap, maps.nrm);
    if (maps.emit) {
      material.emissiveMap = swap(mesh.material.map, maps.emit);
      material.emissive = new Color(1, 1, 1);
      material.emissiveIntensity = 1;
    } else {
      material.emissiveMap = null;
      material.emissive = new Color(0, 0, 0);
    }
    material.envMapIntensity = 1;
    const die = new Mesh(mesh.geometry, material);
    die.userData = { set, color: maps.color };
    die.castShadow = true;
    die.scale.setScalar(DIE_SCALE);
    return die;
  }

  // At rest with the value on top, its marking upright from the camera, centred at x, z.
  function restingPose(model, value, x, z, yaw = 0) {
    const face = model.faces.find((f) => f.value === value) ?? model.faces.at(-1);
    const q = new Quaternion().setFromUnitVectors(new Vector3(...face.normal), UP);
    const up = new Vector3(...face.up).applyQuaternion(q).setY(0).normalize();
    q.premultiply(new Quaternion().setFromAxisAngle(UP, -Math.atan2(up.x, -up.z) + yaw));
    return { p: new Vector3(x, restHeight(bodyOf(model).points, q), z), q };
  }

  // The dice in a row in the middle of the board, their highest face up.
  const idleLayout = (dice) =>
    dice.map((die, i) => restingPose(MODELS[die.kind], die.value ?? SHOWN[die.kind], (i - (dice.length - 1) / 2) * IDLE_GAP, 0.6, (i % 2 ? 1 : -1) * 0.12));

  function applyPose(die, p, q) {
    die.p = p.clone();
    die.q = q.clone();
    die.mesh.position.copy(p);
    die.mesh.quaternion.copy(q);
    highlight(die, board.indexOf(die));
  }

  function disposeDie(mesh) {
    for (const value of new Set(Object.values(mesh.material))) if (value?.isTexture) value.dispose();
    mesh.material.dispose();
  }

  function highlight(die, index) {
    const selected = Boolean(picked & (1 << index));
    die.mesh.position.y = die.p.y + (selected ? PICKED_LIFT : 0);
    die.mesh.material.emissive.copy(selected ? new Color(PICKED_GLOW) : die.mesh.userData.restEmission);
    die.mesh.material.emissiveIntensity = selected ? 0.9 : 1;
    if (selected && !die.ring) {
      ring ??= { geometry: new RingGeometry(0.78, 1, 48), material: new MeshBasicMaterial({ color: 0xffc94d, transparent: true, opacity: 0.95, depthWrite: false }) };
      die.ring = new Mesh(ring.geometry, ring.material);
      die.ring.rotation.x = -Math.PI / 2;
      die.ring.renderOrder = 1;
      scene.add(die.ring);
    }
    if (!die.ring) return;
    die.ring.visible = selected;
    die.ring.position.set(die.p.x, 0.04, die.p.z);
    die.ring.scale.setScalar(die.body.radius * 1.2);
  }

  // The board holds exactly these dice, each in sets[i] or the player's set. Meshes stay when nothing changed. New dice
  // without poses take the place of the die before them, or their place in the row.
  async function placeDice(dice, poses, sets = null) {
    ownDice = !sets;
    wanted = dice;
    const ask = ++placing;
    const setOf = (i) => (sets ? sets[i] : currentSet);
    const keyNow = dice.map((die, i) => `${setOf(i)}:${die.kind}`).join(",");
    if (keyNow !== boardKey) {
      const meshes = await Promise.all(dice.map((die, i) => makeDie(die.kind, setOf(i))));
      if (ask !== placing || disposed) {
        meshes.forEach(disposeDie);
        return;
      }
      const before = board;
      board.forEach((die) => { scene.remove(die.mesh); disposeDie(die.mesh); if (die.ring) scene.remove(die.ring); });
      board = dice.map((die, i) => ({ ...die, set: setOf(i), model: MODELS[die.kind], body: bodyOf(MODELS[die.kind]), mesh: meshes[i] }));
      board.forEach((die) => {
        die.mesh.userData.restEmission = die.mesh.material.emissive.clone();
        scene.add(die.mesh);
      });
      boardKey = keyNow;
      if (!poses) {
        const row = idleLayout(dice);
        poses = board.map((die, i) => (before[i]?.p && before[i].kind === die.kind ? { p: before[i].p, q: before[i].q } : row[i]));
      }
    }
    if (poses?.length === board.length) board.forEach((die, i) => applyPose(die, poses[i].p, poses[i].q));
    requestRender();
  }

  function placeTags() {
    if (!tagData.length) {
      tags.replaceChildren();
      return;
    }
    const rect = canvas.getBoundingClientRect();
    const host = tags.getBoundingClientRect();
    if (tags.children.length !== tagData.length) {
      tags.replaceChildren(
        ...tagData.map((tag) => {
          const node = document.createElement("div");
          node.className = `dice-tag ${tag.kind}`.trim();
          node.textContent = tag.text;
          return node;
        }),
      );
    }
    tagData.forEach((tag, i) => {
      const members = board.filter((die) => die.group === tag.group);
      if (!members.length) return;
      const at = members.reduce((sum, die) => sum.add(die.p), new Vector3()).multiplyScalar(1 / members.length);
      at.y += members[0].body.radius * 1.15;
      at.project(camera);
      const node = tags.children[i];
      node.style.left = `${rect.left - host.left + ((at.x + 1) / 2) * rect.width}px`;
      node.style.top = `${rect.top - host.top + ((1 - at.y) / 2) * rect.height}px`;
      node.classList.add("dice-tag--on");
    });
  }

  function showTags(groups) {
    tagData = groups;
    tags.replaceChildren();
    placeTags();
  }

  function placeCamera(distance) {
    camera.position.set(0, Math.cos(TILT) * distance, Math.sin(TILT) * distance + LOOK.z);
    camera.lookAt(LOOK.x, LOOK.y - 0.15, LOOK.z + 0.2);
    camera.updateMatrixWorld();
    camera.updateProjectionMatrix();
  }

  // Where the board's outline falls on the canvas, -1 to 1 either way.
  function edgeOnCanvas() {
    const out = { left: Infinity, right: -Infinity, bottom: Infinity, top: -Infinity };
    const point = new Vector3();
    for (const at of edge) {
      point.copy(at).project(camera);
      out.left = Math.min(out.left, point.x);
      out.right = Math.max(out.right, point.x);
      out.bottom = Math.min(out.bottom, point.y);
      out.top = Math.max(out.top, point.y);
    }
    return out;
  }

  // The board alone fills the canvas, centred: the camera keeps its angle and comes as close as the outline allows.
  function fitBoard(w, h) {
    camera.clearViewOffset();
    let distance = 40;
    for (let i = 0; i < FIT_STEPS; i++) {
      placeCamera(distance);
      const at = edgeOnCanvas();
      distance *= Math.max(at.right - at.left, at.top - at.bottom) / (2 * BOARD_FILL);
    }
    placeCamera(distance);
    const at = edgeOnCanvas();
    camera.setViewOffset(w, h, ((at.left + at.right) / 4) * w, -((at.bottom + at.top) / 4) * h, w, h);
    camera.updateProjectionMatrix();
  }

  // Fits the board into the stage, between the page's panels, and centres it there.
  function resize() {
    const w = canvas.clientWidth || window.innerWidth;
    const h = canvas.clientHeight || window.innerHeight;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    if (fit === "board" && edge) {
      fitBoard(w, h);
      placeTags();
      requestRender();
      return;
    }
    const canvasRect = canvas.getBoundingClientRect();
    const room = present ? null : stage();
    const usableW = room ? Math.max(200, room.width) / w : 1;
    const usableH = room ? Math.max(200, room.height) / h : present ? PRESENT_FILL : 1;
    const halfH = Math.max(BOARD_REACH / (camera.aspect * usableW), (BOARD_REACH * Math.cos(TILT) + 0.6) / usableH);
    const distance = halfH / Math.tan((camera.fov * Math.PI) / 360);
    camera.position.set(0, Math.cos(TILT) * distance, Math.sin(TILT) * distance + LOOK.z);
    camera.lookAt(LOOK.x, LOOK.y - 0.15, LOOK.z + 0.2);
    if (room) {
      const offsetX = w / 2 - (room.left - canvasRect.left + room.width / 2);
      const offsetY = h / 2 - (room.top - canvasRect.top + room.height / 2);
      camera.setViewOffset(w, h, offsetX, offsetY, w, h);
    } else if (present) {
      camera.setViewOffset(w, h, 0, h * PRESENT_DROP, w, h);
    } else {
      camera.clearViewOffset();
    }
    camera.updateProjectionMatrix();
    placeTags();
    requestRender();
  }

  // idle, loading (making the dice), windup (asked the server), flight (numbers known), landed, settle-back (refused).
  async function startThrow(id, counts, sets) {
    anim.phase = "loading";
    anim.id = id;
    anim.counts = counts;
    anim.values = null;
    anim.throwData = null;
    showTags([]);
    const dice = physicalDice(counts);
    await placeDice(dice, board.length === dice.length ? null : idleLayout(dice), sets);
    if (anim.id !== id) return;
    anim.from = board.map((die) => ({ p: die.p.clone(), q: die.q.clone() }));
    // The flight does not depend on the numbers: every die lifts to exactly where it will leave from, and the numbers
    // only pick the symmetries once they arrive.
    anim.plan = planThrow(board.map((die) => die.model), boardInfo, (Math.random() * 2 ** 31) | 0);
    anim.t = 0;
    anim.phase = "windup";
    if (anim.values) arrive(id, anim.values);
    requestRender();
  }

  function arrive(id, values) {
    if (anim.id !== id || anim.throwData) return;
    anim.values = values;
    if (anim.phase !== "windup") return;
    anim.valueAt = anim.t;
    anim.dice = physicalDice(anim.counts, values);
    anim.throwData = finishThrow(anim.plan, anim.dice.map((die) => die.value), board.map((die) => die.model));
  }

  function settleBack() {
    anim.phase = "settle-back";
    anim.t = 0;
    anim.from = board.map((die) => ({ p: die.p.clone(), q: die.q.clone() }));
    anim.to = idleLayout(board);
    requestRender();
  }

  function refuse(id) {
    if (anim.id !== id) return;
    settleBack();
  }

  function landed() {
    anim.phase = "landed";
    anim.t = 0;
    probe.throws++;
    probe.last = {
      counts: anim.counts,
      values: anim.values,
      expected: anim.dice.map((die) => die.value),
      landed: board.map((die) => topFace(die.model, die.q).face.value),
      sets: board.map((die) => die.set),
    };
    showTags(
      anim.dice.map((die, i) => ({
        group: i,
        text: String(die.value),
        kind: die.kind === "d20" && anim.dice.length === 1 && die.value === 20 ? "dice-tag--crit" : die.kind === "d20" && anim.dice.length === 1 && die.value === 1 ? "dice-tag--fumble" : "",
      })),
    );
    onLanded(anim.counts, anim.values);
  }

  const turn = new Quaternion();
  const spinAxis = new Vector3(0.3, 1, 0.2).normalize();
  function step(dt) {
    if (anim.phase === "shared") {
      const elapsed = Math.max(0, Date.now() - anim.sharedAt);
      const progress = Math.min(1, elapsed / anim.sharedDuration);
      const { tracks, impacts } = anim.throwData;
      const frame = progress * (Math.max(...tracks.map((track) => track.length)) - 1);
      const physicalTime = frame / FRAMES_PER_SECOND;
      while (anim.impactIndex < impacts.length && impacts[anim.impactIndex].t <= physicalTime) {
        const hit = impacts[anim.impactIndex++];
        sound.hit(hit.strength, hit.wall);
      }
      board.forEach((die, i) => {
        const track = tracks[i];
        const index = Math.min(Math.floor(frame), track.length - 1);
        const a = track[index];
        const b = track[Math.min(index + 1, track.length - 1)];
        applyPose(die, a.p.clone().lerp(b.p, frame % 1), a.q.clone().slerp(b.q, frame % 1));
      });
      if (progress >= 1) {
        anim.phase = "idle";
        probe.last = { landed: board.map((die) => topFace(die.model, die.q).face.value), sets: board.map((die) => die.set) };
        onLanded();
      }
      return;
    }
    anim.t += dt;
    if (anim.phase === "windup") {
      // Lifted to where the throw leaves from and spun, as if shaken in a hand, until the numbers come. Then turned into
      // the flight's first frame, so the dice leave without a jump.
      const lift = Math.min(1, anim.t / WINDUP_MIN);
      const eased = 1 - (1 - lift) ** 3;
      let blend = 0;
      if (anim.throwData) {
        const from = Math.max(WINDUP_MIN * 0.45, anim.valueAt);
        const to = Math.max(WINDUP_MIN, anim.valueAt + 0.12);
        blend = Math.max(0, Math.min(1, (anim.t - from) / (to - from)));
      }
      board.forEach((die, i) => {
        const p = anim.from[i].p.clone().lerp(anim.plan.starts[i].p, eased);
        p.y += Math.sin(anim.t * 11 + i) * 0.05 * lift;
        turn.setFromAxisAngle(spinAxis, anim.t * (18 + i * 2) * eased);
        let q = anim.from[i].q.clone().premultiply(turn);
        if (anim.throwData) q = q.slerp(anim.throwData.tracks[i][0].q, blend * blend * (3 - 2 * blend));
        applyPose(die, p, q);
      });
      if (anim.throwData && blend >= 1) {
        anim.phase = "flight";
        anim.t = 0;
        anim.impactIndex = 0;
      } else if (anim.t > ANSWER_WAIT) {
        const id = anim.id;
        settleBack();
        onTimeout(id);
      }
      return;
    }
    if (anim.phase === "flight") {
      const { tracks, impacts } = anim.throwData;
      while (anim.impactIndex < impacts.length && impacts[anim.impactIndex].t <= anim.t) {
        const hit = impacts[anim.impactIndex++];
        sound.hit(hit.strength, hit.wall);
      }
      const frame = anim.t * FRAMES_PER_SECOND;
      let done = true;
      board.forEach((die, i) => {
        const frames = tracks[i];
        if (frame >= frames.length - 1) {
          const last = frames.at(-1);
          applyPose(die, last.p, last.q);
          return;
        }
        done = false;
        const k = frame - Math.floor(frame);
        const a = frames[Math.floor(frame)];
        const b = frames[Math.floor(frame) + 1];
        applyPose(die, a.p.clone().lerp(b.p, k), a.q.clone().slerp(b.q, k));
      });
      if (done) landed();
      return;
    }
    if (anim.phase === "settle-back") {
      const k = Math.min(1, anim.t / SETTLE_SECONDS);
      const eased = k * k * (3 - 2 * k);
      board.forEach((die, i) => applyPose(die, anim.from[i].p.clone().lerp(anim.to[i].p, eased), anim.from[i].q.clone().slerp(anim.to[i].q, eased)));
      if (k >= 1) anim.phase = "idle";
      return;
    }
    if (anim.phase === "landed" && anim.t > LANDED_SECONDS) anim.phase = "idle";
  }

  // Drawn only while something moves or just changed, so a board at rest costs nothing.
  let needsRender = true;
  let looping = false;
  const clock = new Clock();
  const moving = () => ["shared", "windup", "flight", "settle-back", "landed"].includes(anim.phase);

  function requestRender() {
    if (disposed) return;
    needsRender = true;
    if (looping) return;
    looping = true;
    clock.getDelta();
    requestAnimationFrame(frame);
  }

  function frame() {
    if (disposed) return;
    const dt = Math.min(clock.getDelta(), MAX_FRAME);
    const active = moving();
    if (active) step(dt);
    if (needsRender || active) {
      renderer.render(scene, camera);
      if (active) placeTags();
      needsRender = false;
    }
    if (moving() || needsRender) requestAnimationFrame(frame);
    else looping = false;
  }

  async function start() {
    const loaded = await loadModel(boardModel).then(parseGlb);
    if (disposed) return;
    loaded.scene.traverse((node) => {
      if (node.isMesh) node.material = new MeshBasicMaterial({ map: node.material.map, color: new Color(1, 1, 1) });
    });
    scene.add(loaded.scene);
    const box = new Box3().setFromObject(loaded.scene);
    const rx = (box.max.x - box.min.x) / 2;
    const rz = (box.max.z - box.min.z) / 2;
    const cx = (box.max.x + box.min.x) / 2;
    const cz = (box.max.z + box.min.z) / 2;
    edge = [box.min.y, box.max.y].flatMap((y) => Array.from({ length: EDGE_POINTS }, (_, i) => {
      const angle = (i / EDGE_POINTS) * Math.PI * 2;
      return new Vector3(cx + Math.cos(angle) * rx, y, cz + Math.sin(angle) * rz);
    }));
    resize();
  }

  return {
    probe,
    start,
    resize,
    async playShared(values, set, data, elapsedMs, durationMs, animate) {
      const request = ++sharedRequest;
      const received = Date.now();
      anim.phase = "loading";
      picked = 0;
      showTags([]);
      const dice = physicalDice({ d6: values.length }, values);
      const poses = data.tracks.map((track) => animate ? track[0] : track.at(-1));
      await placeDice(dice, poses, dice.map(() => set));
      if (disposed || request !== sharedRequest) return;
      anim.throwData = data;
      anim.sharedAt = received - elapsedMs;
      anim.sharedDuration = durationMs;
      const physicalTime = elapsedMs / durationMs * data.seconds;
      anim.impactIndex = data.impacts.findIndex((hit) => hit.t > physicalTime);
      if (anim.impactIndex < 0) anim.impactIndex = data.impacts.length;
      anim.phase = animate ? "shared" : "idle";
      if (animate) step(0);
      requestRender();
    },
    select(mask) {
      picked = mask;
      board.forEach(highlight);
      requestRender();
    },
    pick(clientX, clientY) {
      const rect = canvas.getBoundingClientRect();
      raycaster.setFromCamera({ x: (clientX - rect.left) / rect.width * 2 - 1, y: 1 - (clientY - rect.top) / rect.height * 2 }, camera);
      const hit = raycaster.intersectObjects(board.map((die) => die.mesh))[0];
      return hit ? board.findIndex((die) => die.mesh === hit.object) : -1;
    },
    dispose() {
      disposed = true;
      placing++;
      sharedRequest++;
      scene.traverse((node) => {
        if (!node.isMesh) return;
        node.geometry.dispose();
        const materials = Array.isArray(node.material) ? node.material : [node.material];
        for (const material of materials) {
          for (const value of Object.values(material)) if (value?.isTexture) value.dispose();
          material.dispose();
        }
      });
      environment.dispose();
      renderer.dispose();
      renderer.forceContextLoss();
    },
    // A roll shown for something else: a small frame and no panels.
    setPresent(on) {
      present = on;
      resize();
    },
    // The player's set on their dice as they lie: a landed throw stays readable. A roll shown for something else keeps
    // its sets.
    async useSet(set, counts) {
      currentSet = set;
      if (!wanted) await this.showTray(counts);
      else if (ownDice) await placeDice(wanted, null);
    },
    // The dice of the tray, waiting in a row. Only while nothing is thrown.
    async showTray(counts) {
      if (!["idle", "landed"].includes(anim.phase)) return;
      showTags([]);
      const dice = physicalDice(counts);
      await placeDice(dice, idleLayout(dice));
    },
    // sets: a set a die, for a roll shown for something else, or null for the player's own.
    throwDice: (id, counts, sets = null) => startThrow(id, counts, sets),
    arrive,
    refuse,
    pickRandomSets,
    isBusy: () => anim.phase === "loading" || anim.phase === "windup" || anim.phase === "flight",
  };
}
