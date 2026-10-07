const { app, BrowserWindow, dialog } = require('electron');
const { spawn } = require('child_process');
const crypto = require('crypto');
const fs = require('fs');
const net = require('net');
const path = require('path');

const token = crypto.randomBytes(24).toString('hex');
let api = null;
let quitting = false;
let ready = false;

function freePort() {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const { port } = server.address();
      server.close(() => resolve(port));
    });
  });
}

async function waitHealthy(baseUrl) {
  for (let i = 0; i < 150; i++) {
    if (api.exitCode !== null) throw new Error(`API başlatılamadı (kod ${api.exitCode}).`);
    try {
      if ((await fetch(`${baseUrl}/health`)).ok) return;
    } catch {
      // API henüz hazır değil
    }
    await new Promise((resolve) => setTimeout(resolve, 200));
  }
  throw new Error('API 30 saniye içinde başlamadı.');
}

function startApi(baseUrl) {
  const dll = path.join(__dirname, '..', 'api', 'out', 'PetAdoption.Api.dll');
  if (!fs.existsSync(dll)) {
    throw new Error('API derlenmemiş. "npm start" ile başlatın.');
  }
  const dbDir = path.join(app.getPath('appData'), 'PetAdoption');
  fs.mkdirSync(dbDir, { recursive: true });

  api = spawn('dotnet', [dll], {
    cwd: path.dirname(dll),
    env: {
      ...process.env,
      ASPNETCORE_URLS: baseUrl,
      AppToken: token,
      DbPath: path.join(dbDir, 'petadoption.db'),
    },
    stdio: 'inherit',
  });
  api.on('exit', (code) => {
    if (quitting || !ready) return;
    dialog.showErrorBox('Hata', `API beklenmedik şekilde kapandı (kod ${code}).`);
    app.quit();
  });
}

function openWindow(baseUrl) {
  const win = new BrowserWindow({
    width: 1100,
    height: 800,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      additionalArguments: [`--api-url=${baseUrl}`, `--app-token=${token}`],
    },
  });
  win.removeMenu();
  win.webContents.setWindowOpenHandler(() => ({ action: 'deny' }));
  win.webContents.on('will-navigate', (event) => event.preventDefault());
  win.loadFile(path.join(__dirname, 'index.html'));
}

app.whenReady().then(async () => {
  try {
    const port = await freePort();
    const baseUrl = `http://127.0.0.1:${port}`;
    startApi(baseUrl);
    await waitHealthy(baseUrl);
    ready = true;
    openWindow(baseUrl);
  } catch (err) {
    dialog.showErrorBox('Başlatılamadı', err.message);
    app.quit();
  }
});

app.on('before-quit', () => {
  quitting = true;
  if (api) api.kill();
});

app.on('window-all-closed', () => app.quit());
