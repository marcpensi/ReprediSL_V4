# aplicar_v4.7.ps1
# Crea Catalogo.jsx, NuevoPedido.jsx y añade CSS al styles.css

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$frontendSrc = Join-Path $root "src\Frontend\src"
$componentsDir = Join-Path $frontendSrc "components"
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupDir = Join-Path $root "backups\v4.7.0_$timestamp"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " REPREDISL v4.7.0 - Catalogo + NuevoPedido" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# --- Backup de styles.css ---
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
$stylesPath = Join-Path $frontendSrc "styles.css"
if (Test-Path $stylesPath) {
    Copy-Item $stylesPath (Join-Path $backupDir "styles.css") -Force
    Write-Host "[BACKUP] styles.css" -ForegroundColor Yellow
}

function Save-Utf8Bom($path, $content) {
    $utf8Bom = New-Object System.Text.UTF8Encoding $true
    [System.IO.File]::WriteAllText($path, $content, $utf8Bom)
}

# --- 1. Catalogo.jsx ---
$catalogoContent = @'
import { useState, useEffect, useMemo } from 'react';
import { allCachedProducts, cacheProducts } from '../db';
import { loadProductsApi } from '../clientApi';

export default function Catalogo({ onAddProducto, productosYaEnPedido = [] }) {
  const [productos, setProductos] = useState([]);
  const [busqueda, setBusqueda] = useState('');
  const [cargando, setCargando] = useState(false);

  useEffect(() => {
    const cargar = async () => {
      try {
        let datos = await allCachedProducts();
        if (datos.length === 0 && navigator.onLine) {
          setCargando(true);
          const frescos = await loadProductsApi();
          await cacheProducts(frescos);
          datos = await allCachedProducts();
          setCargando(false);
        }
        setProductos(datos);
      } catch (err) {
        console.error('Error cargando catalogo:', err);
      }
    };
    cargar();
  }, []);

  const filtrados = useMemo(() => {
    const q = busqueda.trim().toLowerCase();
    if (!q) return productos;
    return productos.filter(p =>
      `${p.code || ''} ${p.name || ''}`.toLowerCase().includes(q)
    );
  }, [productos, busqueda]);

  const yaAñadido = (code) => productosYaEnPedido.includes(code);

  return (
    <div className="catalogo-container">
      <div className="catalogo-header">
        <h3>Catalogo de productos</h3>
        <input
          type="text"
          className="catalogo-search"
          placeholder="Buscar por codigo o nombre..."
          value={busqueda}
          onChange={(e) => setBusqueda(e.target.value)}
          autoFocus
        />
      </div>

      {cargando && <p className="catalogo-loading">Sincronizando catalogo...</p>}

      <div className="catalogo-list">
        {filtrados.length === 0 ? (
          <p className="catalogo-empty">
            {busqueda ? 'Sin resultados para la busqueda.' : 'Catalogo vacio.'}
          </p>
        ) : (
          filtrados.slice(0, 100).map((p) => {
            const duplicado = yaAñadido(p.code);
            return (
              <div key={p.code} className={`catalogo-item ${duplicado ? 'disabled' : ''}`}>
                <div className="catalogo-item-info">
                  <div className="catalogo-item-name">{p.name}</div>
                  <div className="catalogo-item-meta">
                    <span> Cod: {p.code}</span>
                    <span> - </span>
                    <span>{p.price.toFixed(2)} EUR</span>
                    <span> - </span>
                    <span>Stock: {p.stock}</span>
                  </div>
                </div>
                <button
                  className="catalogo-add-btn"
                  onClick={() => !duplicado && onAddProducto(p)}
                  disabled={duplicado}
                >
                  {duplicado ? 'Añadido' : '+ Añadir'}
                </button>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}
'@
Save-Utf8Bom (Join-Path $componentsDir "Catalogo.jsx") $catalogoContent
Write-Host "[OK] Creado components/Catalogo.jsx" -ForegroundColor Green

# --- 2. NuevoPedido.jsx ---
$nuevoPedidoContent = @'
import { useState, useCallback, useEffect } from 'react';
import HistorialPedido from './HistorialPedido';
import Catalogo from './Catalogo';
import { savePedidoLocal, getVendedorActivo, getHistorialByCliente } from '../db';

export default function NuevoPedido({ cliente, onSalir, onPedidoCreado }) {
  const [modo, setModo] = useState('historial');
  const [lineas, setLineas] = useState([]);
  const [guardando, setGuardando] = useState(false);
  const [mensaje, setMensaje] = useState('');
  const [historial, setHistorial] = useState([]);

  useEffect(() => {
    if (cliente?.code) {
      getHistorialByCliente(cliente.code).then(setHistorial);
    }
  }, [cliente]);

  const productosEnPedido = lineas.map(l => l.productoCode);

  const handleUpdateLineas = useCallback((nuevasLineas) => {
    setLineas(prev => {
      const desdeCatalogo = prev.filter(l => l.desdeCatalogo);
      return [...desdeCatalogo, ...nuevasLineas];
    });
  }, []);

  const handleAddDesdeCatalogo = (producto) => {
    const nuevaLinea = {
      id: crypto.randomUUID(),
      clienteCode: cliente.code,
      productoCode: producto.code,
      nombreProducto: producto.name,
      cantidad: 1,
      precio: producto.price,
      fecha: Date.now(),
      cantidadPedido: 1,
      desdeCatalogo: true
    };
    setLineas(prev => [...prev, nuevaLinea]);
  };

  const handleEliminarLinea = (id) => {
    setLineas(prev => prev.filter(l => l.id !== id));
  };

  const handleHacerPedido = async () => {
    const lineasValidas = lineas.filter(l => l.cantidadPedido > 0);
    if (lineasValidas.length === 0) {
      setMensaje('Añade al menos una linea con cantidad > 0');
      return;
    }

    setGuardando(true);
    setMensaje('');

    try {
      const vendedor = await getVendedorActivo();
      const pedido = {
        id_local: crypto.randomUUID(),
        estado: 'borrador',
        fecha: Date.now(),
        vendedorId: vendedor?.id || vendedor?.sellerId || 'desconocido',
        vendedorNombre: vendedor?.nombre || vendedor?.sellerName || '',
        clienteCode: cliente.code,
        clienteNombre: cliente.commercial || cliente.fiscal,
        serie: vendedor?.serie || '1',
        lineas: lineasValidas.map(l => ({
          productoCode: l.productoCode,
          nombreProducto: l.nombreProducto,
          cantidad: l.cantidadPedido,
          precio: l.precio,
          subtotal: l.cantidadPedido * l.precio
        })),
        total: lineasValidas.reduce((acc, l) => acc + (l.cantidadPedido * l.precio), 0)
      };

      await savePedidoLocal(pedido);
      setMensaje('Pedido guardado como borrador');
      setTimeout(() => onPedidoCreado?.(pedido), 800);
    } catch (err) {
      console.error('Error guardando pedido:', err);
      setMensaje('Error al guardar el pedido');
    } finally {
      setGuardando(false);
    }
  };

  const total = lineas.reduce((acc, l) => acc + (l.cantidadPedido * l.precio), 0);
  const numLineas = lineas.filter(l => l.cantidadPedido > 0).length;

  return (
    <div className="nuevo-pedido-container">
      <div className="np-header">
        <button className="np-btn-volver" onClick={onSalir}>Volver</button>
        <div className="np-cliente-info">
          <div className="np-cliente-nombre">{cliente.commercial || cliente.fiscal}</div>
          <div className="np-cliente-codigo">Cod: {cliente.code}</div>
        </div>
      </div>

      <div className="np-modo-tabs">
        <button
          className={`np-tab ${modo === 'historial' ? 'active' : ''}`}
          onClick={() => setModo('historial')}
        >
          Cargar historial
        </button>
        <button
          className={`np-tab ${modo === 'catalogo' ? 'active' : ''}`}
          onClick={() => setModo('catalogo')}
        >
          Catalogo
        </button>
      </div>

      <div className="np-contenido">
        {modo === 'historial' ? (
          <HistorialPedido historial={historial} onUpdateLineas={handleUpdateLineas} />
        ) : (
          <Catalogo
            onAddProducto={handleAddDesdeCatalogo}
            productosYaEnPedido={productosEnPedido}
          />
        )}
      </div>

      {lineas.filter(l => l.cantidadPedido > 0).length > 0 && (
        <div className="np-lineas-actuales">
          <h4>Lineas del pedido ({numLineas})</h4>
          <div className="np-lineas-lista">
            {lineas.filter(l => l.cantidadPedido > 0).map(l => (
              <div key={l.id} className="np-linea-item">
                <span className="np-linea-nombre">{l.nombreProducto}</span>
                <span className="np-linea-cant">{l.cantidadPedido} x {l.precio.toFixed(2)} EUR</span>
                <button
                  className="np-linea-eliminar"
                  onClick={() => handleEliminarLinea(l.id)}
                  title="Eliminar linea"
                >
                  X
                </button>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="np-footer">
        <div className="np-total">
          <span>Total:</span>
          <strong>{total.toFixed(2)} EUR</strong>
        </div>
        <button
          className="np-btn-hacer-pedido"
          onClick={handleHacerPedido}
          disabled={guardando || numLineas === 0}
        >
          {guardando ? 'Guardando...' : 'Hacer pedido'}
        </button>
      </div>

      {mensaje && <div className="np-mensaje">{mensaje}</div>}
    </div>
  );
}
'@
Save-Utf8Bom (Join-Path $componentsDir "NuevoPedido.jsx") $nuevoPedidoContent
Write-Host "[OK] Creado components/NuevoPedido.jsx" -ForegroundColor Green

# --- 3. Añadir CSS ---
$cssAppend = @'

/* ============================================ */
/* === CATALOGO (v4.7.0) ==================== */
/* ============================================ */
.catalogo-container {
  background: #ffffff;
  border-radius: 8px;
  border: 1px solid #e0e0e0;
  padding: 1rem;
  margin-top: 1rem;
}

.catalogo-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  flex-wrap: wrap;
  gap: 0.5rem;
}

.catalogo-header h3 {
  margin: 0;
  font-size: 1rem;
  color: #000000;
  font-weight: 600;
}

.catalogo-search {
  padding: 0.5rem 0.75rem;
  border: 1px solid #cccccc;
  border-radius: 6px;
  font-size: 0.9rem;
  min-width: 200px;
}

.catalogo-search:focus {
  outline: none;
  border-color: #28a745;
  box-shadow: 0 0 0 2px rgba(40, 167, 69, 0.2);
}

.catalogo-loading, .catalogo-empty {
  text-align: center;
  color: #666666;
  padding: 1rem;
}

.catalogo-list {
  max-height: 400px;
  overflow-y: auto;
}

.catalogo-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.75rem;
  border-bottom: 1px solid #f0f0f0;
  gap: 1rem;
}

.catalogo-item:hover { background-color: #f8f9fa; }
.catalogo-item.disabled { opacity: 0.5; }

.catalogo-item-info { flex: 1; min-width: 0; }

.catalogo-item-name {
  font-weight: 500;
  color: #000000;
  margin-bottom: 2px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.catalogo-item-meta {
  font-size: 0.75rem;
  color: #666666;
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

.catalogo-add-btn {
  background-color: #28a745;
  color: #ffffff;
  border: none;
  padding: 0.4rem 0.8rem;
  border-radius: 6px;
  font-weight: 600;
  font-size: 0.8rem;
  cursor: pointer;
  white-space: nowrap;
}

.catalogo-add-btn:disabled {
  background-color: #cccccc;
  cursor: not-allowed;
}

.catalogo-add-btn:active:not(:disabled) {
  background-color: #000000;
}

/* ============================================ */
/* === NUEVO PEDIDO (v4.7.0) ================ */
/* ============================================ */
.nuevo-pedido-container {
  max-width: 900px;
  margin: 0 auto;
  padding: 1rem;
}

.np-header {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background: #000000;
  color: #ffffff;
  border-radius: 8px;
  margin-bottom: 1rem;
}

.np-btn-volver {
  background: transparent;
  color: #ffffff;
  border: 1px solid #ffffff;
  padding: 0.5rem 1rem;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
}

.np-btn-volver:hover { background: #ffffff; color: #000000; }

.np-cliente-info { flex: 1; }

.np-cliente-nombre {
  font-size: 1.1rem;
  font-weight: 600;
}

.np-cliente-codigo {
  font-size: 0.85rem;
  opacity: 0.8;
}

.np-modo-tabs {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
}

.np-tab {
  flex: 1;
  padding: 0.75rem;
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  cursor: pointer;
  font-weight: 600;
  color: #333333;
  transition: all 0.2s;
}

.np-tab.active {
  background: #28a745;
  color: #ffffff;
  border-color: #28a745;
}

.np-contenido {
  min-height: 200px;
}

.np-lineas-actuales {
  background: #ffffff;
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 1rem;
  margin-top: 1rem;
}

.np-lineas-actuales h4 {
  margin: 0 0 0.75rem 0;
  color: #000000;
  font-size: 0.95rem;
}

.np-lineas-lista {
  max-height: 200px;
  overflow-y: auto;
}

.np-linea-item {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 0.5rem;
  border-bottom: 1px solid #f0f0f0;
}

.np-linea-nombre {
  flex: 1;
  font-size: 0.9rem;
  color: #000000;
}

.np-linea-cant {
  font-weight: 600;
  color: #28a745;
  font-size: 0.85rem;
  white-space: nowrap;
}

.np-linea-eliminar {
  background: transparent;
  border: none;
  color: #dc3545;
  cursor: pointer;
  font-size: 1rem;
  padding: 4px 8px;
  border-radius: 4px;
}

.np-linea-eliminar:hover { background: #fee; }

.np-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1rem;
  background: #ffffff;
  border: 2px solid #000000;
  border-radius: 8px;
  margin-top: 1rem;
  position: sticky;
  bottom: 1rem;
}

.np-total {
  font-size: 1.1rem;
  color: #000000;
}

.np-total strong {
  font-size: 1.3rem;
  color: #28a745;
  margin-left: 0.5rem;
}

.np-btn-hacer-pedido {
  background: #28a745;
  color: #ffffff;
  border: none;
  padding: 0.75rem 1.5rem;
  border-radius: 8px;
  font-weight: 700;
  font-size: 1rem;
  cursor: pointer;
  transition: background 0.2s;
}

.np-btn-hacer-pedido:hover:not(:disabled) { background: #218838; }
.np-btn-hacer-pedido:disabled { background: #cccccc; cursor: not-allowed; }

.np-mensaje {
  margin-top: 1rem;
  padding: 0.75rem;
  border-radius: 6px;
  background: #f8f9fa;
  border: 1px solid #e0e0e0;
  text-align: center;
  font-weight: 500;
}
'@

$existingCss = if (Test-Path $stylesPath) { [System.IO.File]::ReadAllText($stylesPath) } else { "" }
if ($existingCss -notmatch "CATALOGO \(v4\.7\.0\)") {
    $newCss = $existingCss.TrimEnd() + "`n`n" + $cssAppend
    Save-Utf8Bom $stylesPath $newCss
    Write-Host "[OK] CSS v4.7.0 añadido a styles.css" -ForegroundColor Green
} else {
    Write-Host "[SKIP] CSS v4.7.0 ya estaba aplicado" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " COMPLETADO v4.7.0" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Archivos creados:" -ForegroundColor Green
Write-Host "  - src/Frontend/src/components/Catalogo.jsx"
Write-Host "  - src/Frontend/src/components/NuevoPedido.jsx"
Write-Host "  - CSS añadido a src/Frontend/src/styles.css"
Write-Host ""
Write-Host "Backup en: $backupDir" -ForegroundColor Yellow