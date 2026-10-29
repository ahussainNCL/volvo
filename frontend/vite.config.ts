import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig({
  plugins: [react()],
  build: { outDir: '../src/Nordlink.Gateway/wwwroot', emptyOutDir: true },
  // Production assets are served by the gateway. No separate public Vite listener.
});
