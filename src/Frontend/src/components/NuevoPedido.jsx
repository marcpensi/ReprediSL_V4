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
      setMensaje('AÃ±ade al menos una linea con cantidad > 0');
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