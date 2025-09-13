import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/Items": {
        target: "http://localhost:5285", // your .NET HTTPS port
        changeOrigin: true,
        secure: false,
      },
    },
  },
});