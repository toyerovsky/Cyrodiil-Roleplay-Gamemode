import shop from "../rp/art/icons/items.webp";
import repair from "../rp/art/icons/keys.webp";
import fish from "../shared/fishing/perch.webp";

// The window's tabs in their order: each one's medallion, what keys its rows, and the rows from the FisherView.
export const TABS = [
  { id: "catch", medallion: fish, key: "item", rows: (view) => view.catch },
  { id: "shop", medallion: shop, key: "ware", rows: (view) => view.shop },
  { id: "repair", medallion: repair, key: "item", rows: (view) => view.repairs },
];
