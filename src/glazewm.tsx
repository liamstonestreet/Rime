/* @refresh reload */
import "./index.css";
import "./frost-bar.css";
import { render } from "solid-js/web";
import { onCleanup, onMount } from "solid-js";
import NordBar from "./components/nord-bar";
import { ProvidersProvider } from "./lib/providers-context";
import { watchRiceWidgets } from "./lib/rice-widgets";

// Widget visibility, layout and appearance come from the control center (Rice tab) over its bridge.
function App() {
  onMount(() => onCleanup(watchRiceWidgets()));
  return <ProvidersProvider><NordBar /></ProvidersProvider>;
}

render(() => <App />, document.getElementById("root")!);
