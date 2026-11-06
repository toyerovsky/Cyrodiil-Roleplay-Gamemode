import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { extname, join, relative, resolve, sep } from "node:path";
import { PAGES } from "../pages.js";

const DIST = resolve(import.meta.dirname, "../dist");
const PREVIEW_ONLY = ["pretendGame", "__readym"];
const KB = 1024;
// Every player downloads the pages with the mod: past this, something big came in unnoticed (a font's every subset).
const MOST_BYTES = 1088 * KB;
// The dice board apart (vite.config.js puts its files under dice/): three.js, the board, four dice and twelve sets of
// their pictures, about 5.5 MB.
const DICE = "dice";
const DICE_MOST_BYTES = 6144 * KB;

function listFiles(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? listFiles(path) : [path];
  });
}

const all = listFiles(DIST);
const problems = PAGES.filter(({ name }) => !existsSync(join(DIST, name, "index.html"))).map(({ name }) => `${name}/index.html is missing`);
for (const path of all) {
  if (extname(path) === ".woff") problems.push(`${relative(DIST, path)} is a woff copy of a woff2 font`);
  if (![".js", ".html"].includes(extname(path))) continue;
  const content = readFileSync(path, "utf8");
  for (const marker of PREVIEW_ONLY) if (content.includes(marker)) problems.push(`${relative(DIST, path)} carries the browser preview (${marker})`);
}

const isDice = (path) => relative(DIST, path).split(sep)[0] === DICE;
const sizeOf = (paths) => paths.reduce((sum, path) => sum + statSync(path).size, 0);
const bytes = sizeOf(all.filter((path) => !isDice(path)));
const diceBytes = sizeOf(all.filter(isDice));
if (bytes > MOST_BYTES) problems.push(`the pages are ${(bytes / KB).toFixed(0)} KB, more than ${MOST_BYTES / KB} KB`);
if (diceBytes > DICE_MOST_BYTES) problems.push(`the dice board is ${(diceBytes / KB).toFixed(0)} KB, more than ${DICE_MOST_BYTES / KB} KB`);

if (problems.length) {
  console.error(`The built pages are not fit to ship:\n${problems.join("\n")}`);
  process.exit(1);
}

console.log(`dist checked: ${PAGES.length} pages, ${all.length} files, ${(bytes / KB).toFixed(0)} KB and the dice board ${(diceBytes / KB).toFixed(0)} KB`);
