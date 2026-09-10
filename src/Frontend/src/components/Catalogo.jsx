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

  const yaAÃ±adido = (code) => productosYaEnPedido.includes(code);

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
            const duplicado = yaAÃ±adido(p.code);
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
                  {duplicado ? 'AÃ±adido' : '+ AÃ±adir'}
                </button>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}