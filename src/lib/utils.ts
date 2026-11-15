import clsx, { ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";
import { btopPath } from "./install-config.generated";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function openBtop(glazewm?: { runCommand: (cmd: string) => Promise<any> } | null) {
  if (!glazewm) return;
  // Shared by the CPU and memory widgets. The installer records where btop4win is
  // (install.ps1 -BtopPath); without it, open Task Manager instead.
  if (!btopPath) return glazewm.runCommand('shell-exec taskmgr');
  // Run from its own directory so btop4win finds its configuration, themes, and companion DLLs.
  const exe = btopPath.replace(/\\/g, '/'), dir = exe.slice(0, exe.lastIndexOf('/'));
  const quote = (path: string) => (/\s/.test(path) ? `"${path}"` : path);
  return glazewm.runCommand(`shell-exec wt -d ${quote(dir)} ${quote(exe)}`);
}
