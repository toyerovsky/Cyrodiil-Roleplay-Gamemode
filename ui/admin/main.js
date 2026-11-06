import { startPage } from "../shared/start.js";
import App from "./App.vue";
import { createBridge } from "./bridge.js";
import "../shared/click-through.css";
import "../lore/view.css";
import "./admin.css";
import "./players.css";
import "./sanctions.css";
import "./audit.css";
import "./dice.css";
import "./sanctuary.css";
import "./world.css";
import "./items.css";
import "./factions.css";
import "./dialog.css";

startPage(App, createBridge, import.meta.env.DEV ? () => import("./pretend.js") : null);
