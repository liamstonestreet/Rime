import { currentWidget } from 'zebar';

// Install folder of this widget pack (helper .exe files and scripts live there), derived from the
// widget's absolute HTML path (<pack>\dist\glazewm\glazewm.html), so nothing depends on the user
// name or install location. Zebar 3.x fills htmlPath at runtime but not configPath, despite its types.
let dir: string | undefined;
const parent = (path: string) => path.slice(0, Math.max(path.lastIndexOf('\\'), path.lastIndexOf('/')));
export function packDir() {
  if (!dir) {
    const widget = currentWidget();
    dir = widget.configPath ? parent(widget.configPath) : parent(parent(parent(widget.htmlPath)));
  }
  return dir;
}

// Windows path of a file in the pack folder (for Zebar shellExec, whose privileges match it).
export function packFile(name: string) {
  return `${packDir()}\\${name}`;
}

// The same path for a GlazeWM `shell-exec` command: forward slashes, quoted if it has spaces.
export function packCommand(name: string) {
  const path = packFile(name).replace(/\\/g, '/');
  return /\s/.test(path) ? `"${path}"` : path;
}
