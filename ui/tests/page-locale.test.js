import { describe, expect, it } from "vitest";
import { pickTexts, usePageTexts } from "../shared/locale.js";

describe("a page's language", () => {
  it("starts in English and follows the language the game names", () => {
    let tell;
    const { locale, t } = usePageTexts({ onLocale: (handler) => (tell = handler) }, "creator");
    expect(t.value.title).toBe("New character");

    tell("pl");
    expect(locale.value).toBe("pl");
    expect(document.documentElement.lang).toBe("pl");
    expect(t.value.title).toBe(pickTexts("pl", "ui.creator.").title);
    expect(t.value.title).not.toBe("New character");
  });
});
