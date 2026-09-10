import { CONFIG } from './config';

const { DB_NAME, DB_VERSION, MAX_CACHE_CLIENTES } = CONFIG;
const STORE = 'clientes';
const PRODUCTS_STORE = 'productos';
const VENDEDOR_STORE = 'vendedor';
const HISTORIAL_STORE = 'historial_compras';
const PEDIDOS_STORE = 'pedidos_locales';

function openDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE)) {
        const store = db.createObjectStore(STORE, { keyPath: 'code' });
        store.createIndex('cachedAt', 'cachedAt');
      }
      if (!db.objectStoreNames.contains(PRODUCTS_STORE)) {
        const pStore = db.createObjectStore(PRODUCTS_STORE, { keyPath: 'code' });
        pStore.createIndex('cachedAt', 'cachedAt');
      }
      if (!db.objectStoreNames.contains(VENDEDOR_STORE)) {
        db.createObjectStore(VENDEDOR_STORE, { keyPath: 'id' });
      }
      if (!db.objectStoreNames.contains(HISTORIAL_STORE)) {
        const hStore = db.createObjectStore(HISTORIAL_STORE, { keyPath: 'id', autoIncrement: true });
        hStore.createIndex('clienteCode', 'clienteCode');
        hStore.createIndex('productoCode', 'productoCode');
      }
      if (!db.objectStoreNames.contains(PEDIDOS_STORE)) {
        const pStore = db.createObjectStore(PEDIDOS_STORE, { keyPath: 'id_local', autoIncrement: true });
        pStore.createIndex('estado', 'estado');
        pStore.createIndex('fecha', 'fecha');
        pStore.createIndex('vendedorId', 'vendedorId');
      }
    };
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

function requestToPromise(request) {
  return new Promise((resolve, reject) => {
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

// --- Clientes ---
async function allClients() {
  const db = await openDb();
  try {
    const tx = db.transaction(STORE, 'readonly');
    return await requestToPromise(tx.objectStore(STORE).getAll());
  } finally {
    db.close();
  }
}

export async function cacheClients(clients) {
  if (!Array.isArray(clients) || clients.length === 0) return;
  const db = await openDb();
  const now = Date.now();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    const store = tx.objectStore(STORE);
    clients.filter(c => c?.code).forEach(c => store.put({ ...c, cachedAt: now }));
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  });
  db.close();

  const rows = await allClients();
  if (rows.length > MAX_CACHE_CLIENTES) {
    const remove = rows.sort((a, b) => (a.cachedAt || 0) - (b.cachedAt || 0)).slice(0, rows.length - MAX_CACHE_CLIENTES);
    const db2 = await openDb();
    await new Promise((resolve, reject) => {
      const tx = db2.transaction(STORE, 'readwrite');
      const store = tx.objectStore(STORE);
      remove.forEach(c => store.delete(c.code));
      tx.oncomplete = resolve;
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error);
    });
    db2.close();
  }
}

export async function cacheClient(client) {
  if (!client?.code) return;
  await cacheClients([client]);
}

export async function recentCachedClients(limit = 30) {
  const rows = await allClients();
  return rows.sort((a, b) => (b.cachedAt || 0) - (a.cachedAt || 0)).slice(0, limit);
}

export async function allCachedClients() { return allClients(); }

export async function searchCachedClients(text, limit = 30) {
  const q = String(text || '').trim().toLowerCase();
  if (!q) return recentCachedClients(limit);
  const rows = await allClients();
  return rows
    .filter(c => `${c.code || ''} ${c.commercial || ''} ${c.fiscal || ''} ${c.nif || ''}`.toLowerCase().includes(q))
    .sort((a, b) => (b.cachedAt || 0) - (a.cachedAt || 0))
    .slice(0, limit);
}

export async function clearClientCache() {
  const db = await openDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).clear();
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  });
  db.close();
}

// --- Productos ---
export async function cacheProducts(products) {
  if (!Array.isArray(products) || products.length === 0) return;
  const db = await openDb();
  const now = Date.now();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(PRODUCTS_STORE, 'readwrite');
    const store = tx.objectStore(PRODUCTS_STORE);
    products.filter(p => p?.code).forEach(p => store.put({ ...p, cachedAt: now }));
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  });
  db.close();
}

export async function allCachedProducts() {
  const db = await openDb();
  try {
    const tx = db.transaction(PRODUCTS_STORE, 'readonly');
    return await requestToPromise(tx.objectStore(PRODUCTS_STORE).getAll());
  } catch (err) {
    console.error('Error leyendo productos de IndexedDB:', err);
    return [];
  } finally {
    db.close();
  }
}

export async function clearProductCache() {
  const db = await openDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(PRODUCTS_STORE, 'readwrite');
    tx.objectStore(PRODUCTS_STORE).clear();
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  });
  db.close();
}

// --- Vendedor + PIN ---
async function hashPin(pin) {
  const encoder = new TextEncoder();
  const data = encoder.encode(pin);
  const hashBuffer = await crypto.subtle.digest('SHA-256', data);
  const hashArray = Array.from(new Uint8Array(hashBuffer));
  return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
}

export async function setVendedorActivo(vendedor, pinPlano = '') {
  const db = await openDb();
  const pinHash = pinPlano ? await hashPin(pinPlano) : null;
  await new Promise((resolve, reject) => {
    const tx = db.transaction(VENDEDOR_STORE, 'readwrite');
    tx.objectStore(VENDEDOR_STORE).put({ id: 'activo', ...vendedor, pinHash, lastSync: Date.now() });
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
  });
  db.close();
}

export async function getVendedorActivo() {
  const db = await openDb();
  try {
    const tx = db.transaction(VENDEDOR_STORE, 'readonly');
    return await requestToPromise(tx.objectStore(VENDEDOR_STORE).get('activo'));
  } finally {
    db.close();
  }
}

export async function verifyPin(pinPlano) {
  const vendedor = await getVendedorActivo();
  if (!vendedor || !vendedor.pinHash) return true;
  const inputHash = await hashPin(pinPlano);
  return inputHash === vendedor.pinHash;
}

export async function clearVendedorActivo() {
  const db = await openDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(VENDEDOR_STORE, 'readwrite');
    tx.objectStore(VENDEDOR_STORE).delete('activo');
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
  });
  db.close();
}

// --- Historial de compras ---
export async function cacheHistorialCompras(clienteCode, registros) {
  if (!clienteCode || !Array.isArray(registros)) return;
  const db = await openDb();

  await new Promise((resolve, reject) => {
    const tx = db.transaction(HISTORIAL_STORE, 'readwrite');
    const store = tx.objectStore(HISTORIAL_STORE);
    const index = store.index('clienteCode');

    const getRequest = index.getAllKeys(clienteCode);
    getRequest.onsuccess = () => {
      const keys = getRequest.result;
      keys.forEach(key => store.delete(key));

      const now = Date.now();
      registros.forEach(r => {
        store.put({
          id: crypto.randomUUID(),
          clienteCode: r.clienteCode,
          productoCode: r.productoCode,
          nombreProducto: r.nombreProducto,
          cantidad: Number(r.cantidad || 0),
          precio: Number(r.precio || 0),
          fecha: r.fecha || now,
          cachedAt: now
        });
      });
      resolve();
    };
    getRequest.onerror = () => reject(getRequest.error);
  });
  db.close();
}

export async function getHistorialByCliente(clienteCode) {
  const db = await openDb();
  try {
    const tx = db.transaction(HISTORIAL_STORE, 'readonly');
    const index = tx.objectStore(HISTORIAL_STORE).index('clienteCode');
    const resultados = await requestToPromise(index.getAll(clienteCode));
    return resultados.sort((a, b) => (b.fecha || 0) - (a.fecha || 0));
  } finally {
    db.close();
  }
}

// --- Pedidos locales ---
export async function savePedidoLocal(pedido) {
  const db = await openDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(PEDIDOS_STORE, 'readwrite');
    tx.objectStore(PEDIDOS_STORE).put(pedido);
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
  });
  db.close();
}

export async function getPedidosByVendedor(vendedorId) {
  const db = await openDb();
  try {
    const tx = db.transaction(PEDIDOS_STORE, 'readonly');
    const index = tx.objectStore(PEDIDOS_STORE).index('vendedorId');
    const pedidos = await requestToPromise(index.getAll(vendedorId));
    return pedidos.sort((a, b) => (b.fecha || 0) - (a.fecha || 0));
  } finally {
    db.close();
  }
}

export async function marcarPedidoEnviado(idLocal, datosServidor) {
  const db = await openDb();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(PEDIDOS_STORE, 'readwrite');
    const store = tx.objectStore(PEDIDOS_STORE);
    const request = store.get(idLocal);
    request.onsuccess = () => {
      const pedido = request.result;
      if (pedido) {
        pedido.estado = 'enviado';
        pedido.id_servidor = datosServidor?.id || null;
        pedido.fecha_envio = Date.now();
        store.put(pedido);
      }
      resolve();
    };
    request.onerror = () => reject(request.error);
  });
  db.close();
}