export const API_URL = (import.meta.env.VITE_API_URL || 'http://127.0.0.1:3000').replace(/\/$/, '');
const CLIENT_LIMIT = 30;

function apiValue(row, ...names) {
  const keys = Object.keys(row || {});
  for (const name of names) {
    if (row?.[name] !== undefined && row?.[name] !== null) return row[name];
    const key = keys.find(k => k.toLowerCase() === String(name).toLowerCase());
    if (key && row[key] !== undefined && row[key] !== null) return row[key];
  }
  return '';
}

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

function escapePostgrestLike(value) {
  // Evita que caracteres de control rompan el filtro PostgREST.
  return String(value).replace(/[(),]/g, ' ').replace(/\*/g, '').trim();
}

export async function loadInitialClientsApi({signal} = {}) {
  const url = `${API_URL}/clientes?order=id_cliente.asc&limit=${CLIENT_LIMIT}`;
  const response = await fetch(url, {headers: {Accept: 'application/json'}, signal});
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
  const data = await response.json();
  if (!Array.isArray(data)) throw new Error('La API no ha devuelto una lista de clientes');
  return data.map(normalizeClient).filter(c => c.code);
}

export async function searchClientsApi(text, {signal} = {}) {
  const term = escapePostgrestLike(text);
  if (term.length < 2) return [];

  const filters = [
    `nombre.ilike.*${term}*`,
    `nombre_comercial.ilike.*${term}*`,
    `nif.ilike.*${term}*`
  ];

  // Si es numérico también intenta el código exacto, que normalmente es bigint.
  if (/^\d+$/.test(term)) filters.push(`id_cliente.eq.${Number(term)}`);

  const orFilter = encodeURIComponent(`(${filters.join(',')})`);
  const url = `${API_URL}/clientes?or=${orFilter}&limit=${CLIENT_LIMIT}`;
  const response = await fetch(url, {headers: {Accept: 'application/json'}, signal});
  if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);

  const data = await response.json();
  if (!Array.isArray(data)) throw new Error('La API no ha devuelto una lista de clientes');
  return data.map(normalizeClient).filter(c => c.code);
}


export async function syncCachedClientsApi(codes, {signal} = {}) {
  const numericCodes = [...new Set((codes || [])
    .map(code => String(code ?? '').trim())
    .filter(code => /^\d+$/.test(code))
  )];
  if (!numericCodes.length) return [];

  const BATCH = 50;
  const result = [];
  for (let i = 0; i < numericCodes.length; i += BATCH) {
    const batch = numericCodes.slice(i, i + BATCH);
    const filter = batch.join(',');
    const url = `${API_URL}/clientes?id_cliente=in.(${filter})`;
    const response = await fetch(url, {headers: {Accept: 'application/json'}, signal});
    if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
    const data = await response.json();
    if (!Array.isArray(data)) throw new Error('La API no ha devuelto una lista de clientes');
    result.push(...data.map(normalizeClient).filter(c => c.code));
  }
  return result;
}
