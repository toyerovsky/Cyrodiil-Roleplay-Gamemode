// The board's models are scripts that hand their payload to DICE_MODEL(name, payload): a page served from readym:// loads
// a script where it may not fetch a file. The build writes them to dice/models (scripts/dice-models.mjs).
const waiting = {};

window.DICE_MODEL = (name, payload) => {
  (waiting[name] ?? []).forEach(({ resolve }) => resolve(payload));
  delete waiting[name];
};

export function loadModel(name) {
  return new Promise((resolve, reject) => {
    const queue = (waiting[name] ??= []);
    queue.push({ resolve, reject });
    if (queue.length > 1) return;
    const script = document.createElement("script");
    script.src = `models/${name}.js`;
    script.onerror = () => {
      (waiting[name] ?? []).forEach((each) => each.reject(new Error(`The dice model ${script.src} did not load.`)));
      delete waiting[name];
    };
    document.head.appendChild(script);
  });
}
