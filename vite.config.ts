import { defineConfig } from "vite";
import solidPlugin from "vite-plugin-solid";
import tailwindcssPostcss from "@tailwindcss/postcss";
import autoprefixer from "autoprefixer";
import { resolve } from "path";
import { fileURLToPath } from "url";

export default defineConfig({
  plugins: [solidPlugin()],
  base: "./",
  css: { postcss: { plugins: [tailwindcssPostcss, autoprefixer] } },
  build: {
    outDir: "dist/glazewm",
    target: "esnext",
    rollupOptions: { input: resolve(fileURLToPath(new URL(".", import.meta.url)), "glazewm.html") },
  },
});
