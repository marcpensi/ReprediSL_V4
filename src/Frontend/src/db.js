const DB_NAME = 'REPREDISL';
const DB_VERSION = 2;
const STORE = 'clientes';
const PRODUCTS_STORE = 'productos';
const MAX_CACHE = 300;

function openDb() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);
    request.onupgradeneeded = () => {
      const db = request.result;
      if (!db.objectStoreNames.contains(STORE)) {
        const store = db.createObjectStore(STORE, {keyPath: 'code'});
        store.createIndex('cachedAt', 'cachedAt');
      }
      if (!db.objectStoreNames.contains(PRODUCTS_STORE)) {
        const pStore = db.createObjectStore(PRODUCTS_STORE, {keyPath: 'code'});
        pStore.createIndex('cachedAt', 'cachedAt');
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
    clients.filter(c => c?.code).forEach(c => store.put({...c, cachedAt: now}));
    tx.oncomplete = resolve;
    tx.onerror = () => reject(tx.error);
    tx.onabort = () => reject(tx.error);
  });
  db.close();

  const rows = await allClients();
  if (rows.length > MAX_CACHE) {
    const remove = rows.sort((a,b)=>(a.cachedAt||0)-(b.cachedAt||0)).slice(0, rows.length-MAX_CACHE);
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
  return rows.sort((a,b)=>(b.cachedAt||0)-(a.cachedAt||0)).slice(0, limit);
}

export async function allCachedClients() {
  return allClients();
}

export async function searchCachedClients(text, limit = 30) {
  const q = String(text || '').trim().toLowerCase();
  if (!q) return recentCachedClients(limit);
  const rows = await allClients();
  return rows
    .filter(c => `${c.code || ''} ${c.commercial || ''} ${c.fiscal || ''} ${c.nif || ''}`.toLowerCase().includes(q))
    .sort((a,b)=>(b.cachedAt||0)-(a.cachedAt||0))
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

export async function cacheProducts(products) {
  if (!Array.isArray(products) || products.length === 0) return;
  const db = await openDb();
  const now = Date.now();
  await new Promise((resolve, reject) => {
    const tx = db.transaction(PRODUCTS_STORE, 'readwrite');
    const store = tx.objectStore(PRODUCTS_STORE);
    products.filter(p => p?.code).forEach(p => store.put({...p, cachedAt: now}));
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

