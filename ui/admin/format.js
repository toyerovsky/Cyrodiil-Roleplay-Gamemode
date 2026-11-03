// Medium, where the other pages write their dates long (shared/format.js).
export const formatDate = (milliseconds, locale) => new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(milliseconds);

export const formatMoment = (milliseconds, locale) =>
  new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(milliseconds);
