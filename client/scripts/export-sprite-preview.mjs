import { mkdir, readFile, writeFile } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'
import { build } from 'vite'
import vue from '@vitejs/plugin-vue'
import { packedSpriteModule } from './pack-sprite.mjs'

const root = fileURLToPath(new URL('../', import.meta.url))
const result = await build({
  configFile: false,
  root,
  base: './',
  plugins: [
    {
      name: 'pack-sprite-preview',
      enforce: 'pre',
      async load(id) {
        if (id.replaceAll('\\', '/').includes('/assets/sprites/') && id.endsWith('.png')) {
          return packedSpriteModule(await readFile(id))
        }
      },
    },
    vue(),
  ],
  resolve: { alias: { '@': resolve(root, 'src') } },
  build: {
    write: false,
    assetsInlineLimit: Number.MAX_SAFE_INTEGER,
    cssCodeSplit: false,
    modulePreload: false,
    rollupOptions: { input: resolve(root, 'sprite-preview.html') },
  },
})
if (!('output' in result)) {
  throw new Error('Expected a single preview build.')
}
const page = result.output.find((file) => file.type === 'asset' && file.fileName === 'sprite-preview.html')
if (!page || page.type !== 'asset') {
  throw new Error('Preview HTML is missing.')
}
let html = String(page.source)
for (const file of result.output) {
  if (file.type === 'chunk') {
    const scriptTag = new RegExp('<script[^>]*src="[^"]*' + file.fileName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '"[^>]*></script>')
    html = html.replace(scriptTag, () => '<script type="module">' + file.code.replaceAll('</script', '<\\/script') + '</script>')
  } else if (file.fileName.endsWith('.css')) {
    const cssTag = /<link[^>]*rel="stylesheet"[^>]*>/
    html = html.replace(cssTag, () => '<style>' + String(file.source) + '</style>')
  }
}
if (/<(?:script|link)[^>]*(?:src|href)="\.\//.test(html)) {
  throw new Error('Preview still contains external build assets.')
}
const destination = resolve(root, 'dist/sprite-preview.html')
await mkdir(dirname(destination), { recursive: true })
await writeFile(destination, html)
console.log(destination)
console.log('Preview bytes: ' + Buffer.byteLength(html))
