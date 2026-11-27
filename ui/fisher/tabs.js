import shop from "../rp/art/icons/items.webp";
import repair from "../rp/art/icons/keys.webp";
import records from "../shared/fishing/goldencarp.webp";
import fish from "../shared/fishing/perch.webp";

// The window's tabs in their order: each one's medallion, what keys its rows, and the rows from the FisherView, or for
// the records from the board the page asks for as that tab opens (null until it is answered).
export const TABS = [
  { id: "catch", medallion: fish, key: "item", rows: (view) => view.catch },
  { id: "shop", medallion: shop, key: "ware", rows: (view) => view.shop },
  { id: "repair", medallion: repair, key: "item", rows: (view) => view.repairs },
  { id: "records", medallion: records, key: "species", rows: (view, board) => board?.species ?? [] },
];
