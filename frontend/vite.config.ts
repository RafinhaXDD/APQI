/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

// Where the dev server forwards API calls. Docker Compose sets it to the api service.
const apiTarget = process.env.API_PROXY_TARGET ?? 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['icon.svg'],
      manifest: {
        name: 'AQPI',
        short_name: 'AQPI',
        description: 'Troque livros com quem está perto de você.',
        lang: 'pt-BR',
        start_url: '/',
        scope: '/',
        display: 'standalone',
        theme_color: '#1f4037',
        background_color: '#f7f4ed',
        icons: [{ src: 'icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' }],
      },
      workbox: {
        // Cache the app shell only, never API responses (ADR-11).
        globPatterns: ['**/*.{js,css,html,svg,woff2}'],
        navigateFallbackDenylist: [/^\/api\//, /^\/hubs\//, /^\/health/],
        runtimeCaching: [],
      },
    }),
  ],
  server: {
    host: true,
    port: 5173,
    strictPort: true,
    // Same origin for the SPA and API so the refresh cookie can be SameSite=Strict (ADR-12).
    proxy: {
      '/api': { target: apiTarget, changeOrigin: true },
      '/hubs': { target: apiTarget, changeOrigin: true, ws: true },
      '/health': { target: apiTarget, changeOrigin: true },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    restoreMocks: true,
  },
})
