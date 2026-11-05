// The dice's pictures, a set and a kind each, for the tray and the set picker.
const files = import.meta.glob("./models/icons/*.webp", { eager: true, import: "default" });

export const iconOf = (set, kind) => files[`./models/icons/${set}-${kind}.webp`];
