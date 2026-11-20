// A picture for each specialization, by the server's id (Knight is knight.webp).
const files = import.meta.glob("../rp/art/specializations/*.webp", { eager: true, import: "default" });

export const pictureOf = (id) => files[`../rp/art/specializations/${id.toLowerCase()}.webp`];
