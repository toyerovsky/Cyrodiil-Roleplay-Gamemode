import { WHEEL_PAGE_SIZE } from "../shared/limits.js";

// The wedges that turn the pages, by ids none of the server's wheels use.
export const PREVIOUS = "page-previous";
export const NEXT = "page-next";

// The keys that turn the pages besides the wedges, as a keydown names them.
export const PAGE_KEYS = { q: -1, e: 1, pageup: -1, pagedown: 1 };

export const countPages = (items) => Math.max(1, Math.ceil(items.length / WHEEL_PAGE_SIZE));

export const turnPage = (page, step, pages) => (page + step + pages) % pages;

// A wheel of one page is its items. A longer one shows a page of them between Next, the first wedge, and Previous,
// the last, which the wheel turns half a wedge so they sit either side of twelve. Each wedge carries the key it
// answers to.
export function layPage(items, page, labels) {
  if (items.length <= WHEEL_PAGE_SIZE) return items;

  const start = page * WHEEL_PAGE_SIZE;
  const onPage = items.slice(start, start + WHEEL_PAGE_SIZE).map((item, index) => ({ ...item, key: String(index + 1) }));
  return [
    { id: NEXT, label: labels.next, icon: "next", disabled: false, reason: "", key: "E" },
    ...onPage,
    { id: PREVIOUS, label: labels.previous, icon: "previous", disabled: false, reason: "", key: "Q" },
  ];
}
