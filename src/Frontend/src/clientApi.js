import { CONFIG } from './config.js';

export const API_URL = (import.meta.env?.VITE_API_URL || 'http://127.0.0.1:3000').replace(/\/$/, '');

// --- Utilidades ---
function apiValue(row, ...names) {
  const keys = Object.keys(row || {});
  for (const name of names) {
    if (row?.[name] !== undefined && row?.[name] !== null) return row[name];
    const key = keys.find(k => k.toLowerCase() === String(name).toLowerCase());
    if (key && row[key] !== undefined && row[key] !== null) return row[key];
  }
  return '';
}

function pickColumn(row, fieldKey) {
  const candidates = CONFIG.HISTORIAL_COLUMN_MAP[fieldKey] || [];
  return apiValue(row, ...candidates);
}

function normalizeHistorial(row) {
  const clienteCode  = String(pickColumn(row, 'clienteCode') ?? '').trim();
  const productoCode = String(pickColumn(row, 'productoCode') ?? '').trim();
  const nombre       = String(pickColumn(row, 'nombre') ?? '').trim();
  const cantidad     = Number(pickColumn(row, 'cantidad') || 0);
  const precio       = Number(pickColumn(row, 'precio') || 0);
  const fecha        = pickColumn(row, 'fecha');

  if (!clienteCode || !productoCode) return null;

  return {
    clienteCode,
    productoCode,
    nombreProducto: nombre || `Producto ${productoCode}`,
    cantidad,
    precio: isNaN(precio) ? 0 : precio,
    fecha: fecha ? new Date(fecha).getTime() : Date.now()
  };
}

function escapePostgrestLike(value) {
  return String(value).replace(/[(),]/g, ' ').replace(/\*/g, '').trim();
}

// --- Clientes ---
export function normalizeClient(row) {
  const code = String(apiValue(row, 'id_cliente', 'codigo', 'code', 'id') ?? '').trim();
  const commercial = String(apiValue(row, 'nombre_comercial', 'nombrecomercial', 'commercial', 'nombre') ?? '').trim();
  const fiscal = String(apiValue(row, 'nombre', 'razonsocial', 'razon_social', 'fiscal') ?? '').trim();
  const deliveryAddress = String(apiValue(row, 'direccionenvio', 'direccion_envio', 'delivery_address', 'street2') ?? '').trim();
  const deliveryPostal = String(apiValue(row, 'cpostalenvio', 'cpostal_envio', 'zip_envio', 'delivery_zip') ?? '').trim();
  const deliveryCity = String(apiValue(row, 'poblacionenvio', 'poblacion_envio', 'city_envio', 'delivery_city') ?? '').trim();
  const deliveryProvince = String(apiValue(row, 'provinciaenvio', 'provincia_envio', 'state_envio', 'delivery_state') ?? '').trim();

  return {
    code,
    commercial: commercial || fiscal || `Cliente ${code}`,
    fiscal: fiscal || commercial,
    nif: String(apiValue(row, 'nif', 'cif') ?? ''),
    phone: String(apiValue(row, 'telefono', 'phone', 'tel') ?? ''),
    mobile: String(apiValue(row, 'movil', 'mobile', 'telefono_movil') ?? ''),
    email: String(apiValue(row, 'email', 'correo', 'correo_electronico') ?? ''),
    website: String(apiValue(row, 'web', 'website', 'paginaweb', 'pagina_web') ?? ''),
    fiscalAddress: {
      address: String(apiValue(row, 'street', 'direccion', 'address') ?? ''),
      postal: String(apiValue(row, 'codigo_postal', 'zip', 'cpostal', 'postal') ?? ''),
      city: String(apiValue(row, 'city', 'poblacion') ?? ''),
      province: String(apiValue(row, 'state', 'provincia') ?? '')
    },
    deliveryAddress: {
      address: deliveryAddress || String(apiValue(row, 'street', 'direccion', 'address') ?? ''),
      postal: deliveryPostal || String(apiValue(row, 'codigo_postal', 'zip', 'cpostal', 'postal') ?? ''),
      city: deliveryCity || String(apiValue(row, 'city', 'poblacion') ?? ''),
      province: deliveryProvince || String(apiValue(row, 'state', 'provincia') ?? '')
    },
    bank: String(apiValue(row, 'nombre_banco', 'banco', 'bank') ?? ''),
    iban: String(apiValue(row, 'cuenta_bancaria', 'iban') ?? ''),
    paymentMethodId: apiValue(row, 'id_formapago', 'id_forma_pago'),
    paymentMethod: String(apiValue(row, 'nombre_forma_pago', 'nomformapago', 'nom_forma_pago') ?? ''),
    industryId: apiValue(row, 'id_industria'),
    industry: String(apiValue(row, 'nombre_industria') ?? ''),
    sellerId: apiValue(row, 'id_vendedor'),
    sellerName: String(apiValue(row, 'nombre_vendedor') ?? ''),
    lastUser: 'Servidor',
    lastUpdate: new Date().toLocaleString('es-ES'),
    lastSync: new Date().toLocaleString('es-ES')
  };
}

export async function loadInitialClientsApi({ signal } = {}) {
  const url = `${API_URL}/clientes?order=id_cliente.asc&limit=${CONFIG.CLIENTES_LIMIT}`;
  const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
  const data = await response.json();
  if (!Array.isArray(data)) throw new Error('La API no ha devuelto una lista de clientes');
  return data.map(normalizeClient).filter(c => c.code);
}

export async function searchClientsApi(text, { signal } = {}) {
  const term = escapePostgrestLike(text);
  if (term.length < 2) return [];

  const filters = [
    `nombre.ilike.*${term}*`,
    `nombre_comercial.ilike.*${term}*`,
    `nif.ilike.*${term}*`
  ];
  if (/^\d+$/.test(term)) filters.push(`id_cliente.eq.${Number(term)}`);

  const orFilter = encodeURIComponent(`(${filters.join(',')})`);
  const url = `${API_URL}/clientes?or=${orFilter}&limit=${CONFIG.CLIENTES_LIMIT}`;
  const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);

  const data = await response.json();
  if (!Array.isArray(data)) throw new Error('La API no ha devuelto una lista de clientes');
  return data.map(normalizeClient).filter(c => c.code);
}

export async function syncCachedClientsApi(codes, { signal } = {}) {
  const numericCodes = [...new Set((codes || [])
    .map(code => String(code ?? '').trim())
    .filter(code => /^\d+$/.test(code))
  )];
  if (!numericCodes.length) return [];

  const BATCH = 50;
  const result = [];
  for (let i = 0; i < numericCodes.length; i += BATCH) {
    const batch = numericCodes.slice(i, i + BATCH);
    const url = `${API_URL}/clientes?id_cliente=in.(${batch.join(',')})`;
    const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
    if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
    const data = await response.json();
    if (!Array.isArray(data)) throw new Error('La API no ha devuelto una lista de clientes');
    result.push(...data.map(normalizeClient).filter(c => c.code));
  }
  return result;
}

// --- Productos ---
export function normalizeProduct(row) {
  const code = String(apiValue(row, 'codigo', 'id_producto', 'code', 'id') ?? '').trim();
  const name = String(apiValue(row, 'descripcion', 'nombre', 'name') ?? '').trim();
  const priceVal = apiValue(row, 'precio_venta', 'precio', 'price');
  const price = (priceVal !== '' && priceVal !== null && priceVal !== undefined && !isNaN(Number(priceVal))) ? Number(priceVal) : null;
  const boxVal = apiValue(row, 'unidades_caja', 'caja', 'box');
  const box = (boxVal !== '' && boxVal !== null && boxVal !== undefined && !isNaN(Number(boxVal)) && Number(boxVal) > 0) ? Number(boxVal) : null;
  const stockVal = apiValue(row, 'existencias', 'stock');
  const stock = (stockVal !== '' && stockVal !== null && stockVal !== undefined && !isNaN(Number(stockVal))) ? Number(stockVal) : null;
  const tarifaId = apiValue(row, 'id_tarifa', 'tarifa');

  return {
    code,
    name: name || (code ? `Producto ${code}` : ''),
    price,
    box,
    stock,
    tarifaId: tarifaId ? Number(tarifaId) : null
  };
}

export async function loadProductsApi(tarifaId, { signal } = {}) {
  const table = CONFIG.CATALOGO_TABLE || 'catalogo';
  const tarifaQuery = tarifaId != null ? `id_tarifa=eq.${encodeURIComponent(tarifaId)}&` : '';
  const url = `${API_URL}/${table}?${tarifaQuery}order=codigo.asc&limit=${CONFIG.PRODUCTOS_LIMIT}`;
  const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
  const data = await response.json();
  if (!Array.isArray(data)) throw new Error('La API no ha devuelto una lista de productos');
  return data.map(normalizeProduct).filter(p => p.code);
}

export async function fetchSingleProductApi(code, tarifaId, { signal } = {}) {
  if (!code) return null;
  const table = CONFIG.CATALOGO_TABLE || 'catalogo';
  const tarifaQuery = tarifaId != null ? `&id_tarifa=eq.${encodeURIComponent(tarifaId)}` : '';
  const url = `${API_URL}/${table}?codigo=eq.${encodeURIComponent(code)}${tarifaQuery}&limit=1`;
  const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
  const data = await response.json();
  if (!Array.isArray(data) || data.length === 0) return null;
  return normalizeProduct(data[0]);
}

// --- Tarifas ---
export async function loadTarifasApi({ signal } = {}) {
  const url = `${API_URL}/${CONFIG.TARIFAS_TABLE || 'tarifas'}?order=id_tarifa.asc`;
  const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
  const data = await response.json();
  if (!Array.isArray(data)) return [];
  return data.map(r => ({
    id: Number(r.id_tarifa),
    name: String(r.nombre_tarifa || '').trim()
  })).filter(t => !isNaN(t.id));
}

// --- Vendedores ---
export async function loadVendedoresApi({ signal } = {}) {
  const url = `${API_URL}/${CONFIG.VENDEDORES_TABLE || 'vendedores'}?order=id_vendedor.asc`;
  const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
  const data = await response.json();
  if (!Array.isArray(data)) return [];
  return data.map(r => {
    const code = Number(r.id_vendedor);
    const name = String(r.nombre_vendedor || '').trim();
    return {
      code,
      name,
      series: code === 13 ? 'VD' : (name.substring(0, 2).toUpperCase() || 'VD')
    };
  }).filter(v => !isNaN(v.code));
}

// --- Historial de ventas ---
export async function loadHistorialVentasApi(clienteCode, { signal } = {}) {
  if (!clienteCode) return [];

  const url = `${API_URL}/${CONFIG.HISTORIAL_TABLE}?` +
    `id_cliente=eq.${encodeURIComponent(clienteCode)}&` +
    `order=fecha.desc&limit=${CONFIG.HISTORIAL_LIMIT}`;

  const response = await fetch(url, {
    headers: { Accept: 'application/json' },
    signal
  });

  if (!response.ok) throw new Error(`HTTP ${response.status} al cargar historial`);

  const data = await response.json();
  if (!Array.isArray(data)) return [];

  const normalized = data.map(normalizeHistorial).filter(Boolean);
  // Mantener solo la compra más reciente por producto
  const byProduct = new Map();
  for (const item of normalized) {
    if (!byProduct.has(item.productoCode)) {
      byProduct.set(item.productoCode, item);
    }
  }
  return Array.from(byProduct.values());
}

// --- Numeración de pedidos por serie ---
export async function getMaxOrderNumberApi(serie, { signal } = {}) {
  if (!serie) return 0;
  const cleanSerie = String(serie).trim().toUpperCase();
  try {
    const fetchMaxFromTable = async (tableName) => {
      try {
        const url = `${API_URL}/${tableName}?serie=eq.${encodeURIComponent(cleanSerie)}&select=numero_pedido&order=numero_pedido.desc&limit=1`;
        const res = await fetch(url, { headers: { Accept: 'application/json' }, signal });
        if (!res.ok) return 0;
        const data = await res.json();
        if (Array.isArray(data) && data.length > 0 && data[0]?.numero_pedido != null) {
          return Number(data[0].numero_pedido) || 0;
        }
        return 0;
      } catch {
        return 0;
      }
    };

    const [maxNuevos, maxPedidos] = await Promise.all([
      fetchMaxFromTable('pedidos_nuevos'),
      fetchMaxFromTable('pedidos')
    ]);

    return Math.max(maxNuevos, maxPedidos);
  } catch (err) {
    console.warn('Error consultando número máximo para serie:', cleanSerie, err);
    return 0;
  }
}

// --- Creación de pedidos ---
export async function createOrderApi(orderPayload, { signal } = {}) {
  const url = `${API_URL}/${CONFIG.PEDIDOS_TABLE || 'pedidos_nuevos'}`;
  const response = await fetch(url, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Prefer': 'return=representation'
    },
    body: JSON.stringify(orderPayload),
    signal
  });
  if (!response.ok) {
    const errBody = await response.text().catch(() => '');
    throw new Error(`HTTP ${response.status} ${response.statusText}: ${errBody}`);
  }
  const data = await response.json();
  return Array.isArray(data) ? data[0] : data;
}

// --- Consulta de pedidos (histórico y sincronizados) ---
export async function loadOrdersApi(sellerId, { signal } = {}) {
  try {
    const url = `${API_URL}/${CONFIG.PEDIDOS_TABLE || 'pedidos_nuevos'}?${sellerId ? `id_vendedor=eq.${encodeURIComponent(sellerId)}&` : ''}order=id.desc&limit=50`;
    const response = await fetch(url, { headers: { Accept: 'application/json' }, signal });
    if (!response.ok) return [];
    const data = await response.json();
    if (!Array.isArray(data)) return [];
    return data.map(p => ({
      id: p.id,
      id_servidor: p.id,
      series: p.serie || 'VD',
      number: p.numero_pedido,
      date: p.fecha ? new Date(p.fecha).toLocaleDateString('es-ES') : '',
      clientCode: p.id_cliente,
      clientName: p.cliente,
      sellerName: p.vendedor,
      sellerId: p.id_vendedor,
      total: Number(p.total) || 0,
      status: p.estado === 'S' ? 'Sincronizado' : (p.synced_at ? 'Sincronizado' : 'Enviado'),
      estado: 'sincronizado',
      readonly: true,
      isHistorical: true,
      lines: Array.isArray(p.lineas) ? p.lineas.map(l => ({
        code: l.code,
        name: l.desc || l.name,
        qty: Number(l.qty) || 0,
        price: Number(l.price) || 0
      })) : []
    }));
  } catch (err) {
    console.warn('Error cargando pedidos desde PostgREST:', err);
    return [];
  }
}