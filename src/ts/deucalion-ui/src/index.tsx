/* @refresh reload */
import { render } from "solid-js/web";

import "./stores/tweaks-store"; // initializes theme/font effects

import { App } from "./app";
import { connectSSE } from "./stores/sse";
import "./styles/index.css";

const root = document.getElementById("root");
if (!root) throw new Error("Failed to find the root element");

render(() => <App />, root);
connectSSE();
