// The window arrives as JSON: {path, offered: [{id, path, mastered, trained, majors, chosen}], chosen, gold, price,
// nextChoiceAt, caps}, the specializations this mentor teaches (path null: the registrar, who teaches all of them), the
// character's own specialization of any path (null before the first), the gold carried, what the next choice costs and, while a change must wait, when it may
// be made (Unix milliseconds). A choice is answered with a line in the player's language and whether it went through.
export function createBridge(readym) {
  return {
    onLocale: (handler) => readym.on("Locale", handler),
    onState: (handler) => readym.on("State", (json) => handler(JSON.parse(json))),
    onAnswer: (handler) => readym.on("Answer", handler),
    onClosed: (handler) => readym.on("Closed", handler),
    signalReady: () => readym.sendMentorReady(),
    choose: (specialization) => readym.sendMentorChoose(specialization),
    close: () => readym.sendMentorClose(),
  };
}
