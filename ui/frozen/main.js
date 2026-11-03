import { startPage } from "../shared/start.js";
import App from "./App.vue";
import { createBridge } from "./bridge.js";
import "../shared/click-through.css";
import "./frozen.css";

startPage(App, createBridge, import.meta.env.DEV ? () => import("./pretend.js") : null);
