import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 3000,
    // Vite's dev server rejects requests whose Host header it doesn't
    // recognize by default (DNS-rebinding protection) — without this, a
    // request arriving through a tunnel (a hostname like
    // *.trycloudflare.com the dev server has never seen) gets blocked
    // outright. `true` disables the check rather than hardcoding a specific
    // tunnel hostname, since a fresh cloudflared quick tunnel gets a new
    // random one every time it (re)starts.
    allowedHosts: true,
    // Docker Desktop's bind mount for ./frontend/src (see docker-compose.yml)
    // doesn't propagate native filesystem change events from the Windows
    // host into the Linux container — chokidar's default inotify-based
    // watcher never fires, so edits sit un-picked-up until the container is
    // recreated. Verified live: editing a component while the dev server was
    // running produced zero HMR log output and the browser kept serving the
    // pre-edit bundle indefinitely. Polling sidesteps this by having
    // chokidar stat() the files on an interval instead of waiting on OS
    // events. 300ms keeps edit-to-HMR latency low without busy-looping.
    watch: {
      usePolling: true,
      interval: 300,
    },
    // Forwards /api/* to the backend container over the internal Docker
    // network, so the browser only ever talks to whatever origin actually
    // served the page (localhost:3001, or a Cloudflare tunnel URL) — no
    // second absolute backend URL baked into the bundle. Fixes a real
    // failure mode: with VITE_API_URL hardcoded to a tunnel URL, browsing
    // via localhost still routed every API call through that tunnel, so a
    // flaky/expired quick tunnel broke local access too, even though
    // nothing was actually wrong with the backend on this machine.
    proxy: {
      '/api': {
        target: 'http://backend:5000',
        changeOrigin: true,
      },
    },
  },
})
