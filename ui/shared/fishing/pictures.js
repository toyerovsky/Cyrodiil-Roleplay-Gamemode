// The painted pictures of the fishing pages: a species' is its name in lower case, a ware's its FisherView icon.
const PICTURES = Object.fromEntries(
  Object.entries(import.meta.glob("./*.webp", { eager: true, import: "default" })).map(([path, url]) => [path.slice(2, -".webp".length), url]),
);

export const BAIT_PICTURES = Object.freeze({ Worms: "worms", Dough: "dough", CrabMeat: "crabmeat", Lure: "spinner" });
export const ROD_PICTURES = Object.freeze({ Basic: "rod", Sturdy: "rod-sturdy", Master: "rod-master" });

export const findPicture = (name) => PICTURES[name] ?? null;
export const findSpeciesPicture = (species) => (species ? findPicture(species.toLowerCase()) : null);
export const listPictures = () => Object.keys(PICTURES);
