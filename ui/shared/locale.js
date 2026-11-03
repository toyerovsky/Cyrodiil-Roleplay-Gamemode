import { computed, ref } from "vue";

const BASE_LOCALE = "en";

// Only the pages' own groups (ui.*) travel with the pages: the server speaks the rest.
const files = import.meta.glob("../../locales/*/ui.*.json", { eager: true, import: "default" });

const byLocale = {};
for (const [path, texts] of Object.entries(files)) {
  const locale = path.split("/").at(-2);
  byLocale[locale] = { ...byLocale[locale], ...texts };
}

export function pickTexts(locale, prefix) {
  const merged = { ...byLocale[BASE_LOCALE], ...byLocale[locale] };
  return Object.fromEntries(
    Object.entries(merged)
      .filter(([key]) => key.startsWith(prefix))
      .map(([key, text]) => [key.slice(prefix.length), text]),
  );
}

export function usePageTexts(bridge, page) {
  const locale = ref(BASE_LOCALE);
  bridge.onLocale((value) => {
    locale.value = value;
    document.documentElement.lang = value;
  });
  return { locale, t: computed(() => pickTexts(locale.value, `ui.${page}.`)) };
}

// One pass, so a value is never read as a placeholder.
export function fill(text, args = {}) {
  return text.replace(/\{([A-Za-z_][A-Za-z0-9_]*)\}/g, (match, name) => (name in args ? String(args[name]) : match));
}
