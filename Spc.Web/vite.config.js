import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  base: '/spc-simulator/',
  plugins: [vue()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'https://spc-api-ap7g.onrender.com',
        changeOrigin: true
      }
    }
  }
})
