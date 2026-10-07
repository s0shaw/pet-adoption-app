const { contextBridge } = require('electron');

const arg = (name) =>
  (process.argv.find((a) => a.startsWith(`--${name}=`)) || '').slice(name.length + 3);

contextBridge.exposeInMainWorld('config', {
  apiUrl: arg('api-url'),
  appToken: arg('app-token'),
});
