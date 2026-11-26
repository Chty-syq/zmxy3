const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('workbench', {
  action: (name, value) => ipcRenderer.invoke('shell:action', name, value),
  onState: handler => ipcRenderer.on('shell:state', (_event, state) => handler(state)),
  bounds: rect => ipcRenderer.send('shell:bounds', rect)
});
