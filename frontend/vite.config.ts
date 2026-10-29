import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
export default defineConfig({
  plugins: [react()],
  build: { outDir: process.env.VITE_NORDLINK_HOSTING === 'cloud' ? '../src/Nordlink.Cloud/wwwroot' : '../src/Nordlink.Gateway/wwwroot', emptyOutDir: true },
  // Production assets are served by the gateway. No separate public Vite listener.
});
