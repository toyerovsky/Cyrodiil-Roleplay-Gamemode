// A held key is one press: its repeats do nothing on a page. Typing and the caret in a field still repeat, but a held
// Enter would send again what the server just refused.
export function isHeldPress(event) {
  if (!event.repeat) return false;
  const tag = event.target?.tagName;
  const typing = (tag === "INPUT" || tag === "TEXTAREA") && event.key !== "Enter" && event.key !== "Escape";
  return !typing;
}

// The keys of an input method still putting a character together (a dead key, an IME) are not the page's.
export const isComposing = (event) => event.isComposing || event.keyCode === 229;

// The game hands a page the Windows key code alone, with no scan code, so there a key's `code` is empty and only its
// `keyCode` tells it. A browser gives both.
const LETTER_A = 65;
const LETTER_Z = 90;
const CODES = {
  13: "Enter",
  27: "Escape",
  33: "PageUp",
  34: "PageDown",
  37: "ArrowLeft",
  38: "ArrowUp",
  39: "ArrowRight",
  40: "ArrowDown",
};

export function readCode(event) {
  if (event.code) return event.code;
  const keyCode = event.keyCode;
  if (keyCode >= LETTER_A && keyCode <= LETTER_Z) return `Key${String.fromCharCode(keyCode)}`;
  return CODES[keyCode] ?? "";
}
