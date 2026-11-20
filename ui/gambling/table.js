import board from "../dice/models/board.json";
import d6 from "../dice/models/dice/d6.json";
import { DIE_SCALE, scaleModel } from "../dice/board/dice.js";
import { finishPartialThrow, planPartialThrow } from "../dice/board/physics.js";

const MODEL = scaleModel(d6, DIE_SCALE);
export const viewerSeats = (seat) => [seat, 1 - seat];
export const boardForSeat = (seat) => seat === 0 ? "board" : "board-negative";
export const seatName = (match, seat) => seat === match.seat ? match.you : match.opponent;
export const seatEvents = (match, seat) => match.throws.filter((event) => event.seat === seat && event.round === match.round && event.replay === match.replay);

export function createSeatPlanner() {
  const cache = new Map();
  return (events) => {
    let previous;
    for (const event of events) {
      if (!cache.has(event.id)) {
        const values = event.kind === "Initial" ? [...event.values] : [...previous.values];
        event.indices.forEach((index, i) => { values[index] = event.values[i]; });
        const models = values.map(() => MODEL);
        const poses = previous?.data.tracks.map((track) => track.at(-1)) ?? [];
        const plan = planPartialThrow(models, board, event.seed, event.indices, poses);
        cache.set(event.id, { id: event.id, values, set: event.set, data: finishPartialThrow(plan, event.values, models, poses) });
      }
      previous = cache.get(event.id);
    }
    return previous;
  };
}
