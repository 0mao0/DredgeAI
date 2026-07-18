import { spawn } from 'child_process'
import { fileURLToPath } from 'url'
import path from 'path'
import fs from 'fs'
import os from 'os'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const node = process.execPath

function findPnpmCjs() {
  const home = os.homedir()
  const candidates = [
    path.join(home, 'AppData', 'Roaming', 'npm', 'node_modules', 'pnpm', 'bin', 'pnpm.cjs'),
    path.join(__dirname, 'node_modules', 'pnpm', 'bin', 'pnpm.cjs'),
    path.join(home, '.npm', 'pnpm', 'bin', 'pnpm.cjs'),
  ]
  for (const c of candidates) {
    if (fs.existsSync(c)) return c
  }
  return null
}

const pnpmCjs = findPnpmCjs()
if (!pnpmCjs) {
  console.error('Cannot find pnpm.cjs. Try running: npm install -g pnpm')
  process.exit(1)
}

function start(label, ...args) {
  const child = spawn(node, [pnpmCjs, ...args], {
    stdio: 'inherit',
    shell: false,
    cwd: __dirname,
  })
  child.on('error', (err) => console.error(`[${label}] ${err.message}`))
  child.on('exit', (code) => {
    if (code !== 0 && code !== null) console.log(`[${label}] exited with code ${code}`)
  })
  return child
}

const user = start('user', '--filter', 'user-web', 'dev')
const admin = start('admin', '--filter', 'admin-web', 'dev')

console.log('')
console.log('  User:  http://localhost:5373')
console.log('  Admin: http://localhost:5374')
console.log('  Press Ctrl+C to stop both')
console.log('')

function cleanup() {
  user.kill()
  admin.kill()
  process.exit(0)
}
process.on('SIGINT', cleanup)
process.on('SIGTERM', cleanup)
