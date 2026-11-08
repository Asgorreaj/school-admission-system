import { defineConfig } from "vite"; // vite config
import react from "@vitejs/plugin-react"; // react plugin

export default defineConfig({ // config
  plugins: [react()], // react enable
  server: { proxy: { "/api": { target: "http://localhost:8080", rewrite: (p) => p.replace(/^\/api/, "") } } }, // dev server: /api -> backend
});