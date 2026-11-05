import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

// The dice board's models as scripts calling DICE_MODEL(name, payload) (dice/board/models.js): the board itself, one die
// of each kind with its shape and material, and each set's pictures alone. Written into dice/models by the build, served
// from the same path while previewing.
const MODELS = fileURLToPath(new URL("../dice/models/", import.meta.url));
const SETS = ["altmer", "argonian", "ayleid", "bosmer", "breton", "daedric", "dunmer", "imperial", "khajiit", "nord", "orc", "redguard"];
const KINDS = ["d6", "d10", "d20", "d100"];
// Whose models carry each kind's shape and material.
const TEMPLATE_SET = "imperial";
const PICTURES = ["color", "orm", "nrm", "emit"];
const REQUIRED = ["color", "orm", "nrm"];
const SERVED = /^\/dice\/models\/([\w-]+)\.js$/;

const script = (name, payload) => `DICE_MODEL(${JSON.stringify(name)},${JSON.stringify(payload)});\n`;
const readBase64 = (file) => readFileSync(MODELS + file).toString("base64");

// A GLB is a JSON chunk and one binary chunk, its pictures byte ranges of the binary one.
function readPictures(file) {
  const glb = readFileSync(MODELS + file);
  const jsonLength = glb.readUInt32LE(12);
  const json = JSON.parse(glb.subarray(20, 20 + jsonLength).toString("utf8"));
  const binary = 20 + jsonLength + 8;
  const pictures = {};
  for (const image of json.images ?? []) {
    const view = json.bufferViews[image.bufferView];
    const start = binary + (view.byteOffset ?? 0);
    pictures[image.name.split("_").pop()] = glb.subarray(start, start + view.byteLength).toString("base64");
  }
  return pictures;
}

function readSet(set) {
  const kinds = {};
  for (const kind of KINDS) {
    const pictures = readPictures(`dice/${set}-${kind}.glb`);
    const missing = REQUIRED.filter((name) => !pictures[name]);
    if (missing.length) throw new Error(`dice/${set}-${kind}.glb has no ${missing.join(", ")} picture.`);
    kinds[kind] = Object.fromEntries(PICTURES.filter((name) => pictures[name]).map((name) => [name, pictures[name]]));
  }
  return kinds;
}

function buildScripts() {
  const scripts = new Map([["board", script("board", readBase64("board.glb"))]]);
  for (const kind of KINDS) scripts.set(`die-${kind}`, script(`die-${kind}`, readBase64(`dice/${TEMPLATE_SET}-${kind}.glb`)));
  for (const set of SETS) scripts.set(set, script(set, readSet(set)));
  return scripts;
}

export function diceModels() {
  let scripts = null;
  const read = () => (scripts ??= buildScripts());
  return {
    name: "dice-models",
    configureServer(server) {
      server.middlewares.use((request, response, next) => {
        const name = SERVED.exec(request.url.split("?")[0])?.[1];
        if (!name || !read().has(name)) return next();
        response.setHeader("Content-Type", "text/javascript");
        response.end(read().get(name));
      });
    },
    generateBundle() {
      for (const [name, source] of read()) this.emitFile({ type: "asset", fileName: `dice/models/${name}.js`, source });
    },
  };
}
