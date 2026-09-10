import { useState, useEffect } from 'react';

export default function HistorialPedido({ historial, onUpdateLineas }) {
  const [lineas, setLineas] = useState([]);

  useEffect(() => {
    if (historial && historial.length > 0) {
      const lineasInit = historial.map(h => ({ ...h, cantidadPedido: 0 }));
      setLineas(lineasInit);
      onUpdateLineas(lineasInit.filter(l => l.cantidadPedido > 0));
    } else {
      setLineas([]);
      onUpdateLineas([]);
    }
  }, [historial, onUpdateLineas]);

  const handleCantidadChange = (id, valor) => {
    const num = Math.max(0, Number(valor) || 0);
    const nuevasLineas = lineas.map(l =>
      l.id === id ? { ...l, cantidadPedido: num } : l
    );
    setLineas(nuevasLineas);
    onUpdateLineas(nuevasLineas.filter(l => l.cantidadPedido > 0));
  };

  const aplicarMismasUnidades = () => {
    const nuevasLineas = lineas.map(l => ({
      ...l,
      cantidadPedido: l.cantidad > 0 ? l.cantidad : 1
    }));
    setLineas(nuevasLineas);
    onUpdateLineas(nuevasLineas.filter(l => l.cantidadPedido > 0));
  };

  if (!historial || historial.length === 0) {
    return (
      <div className="historial-empty">
        <p>No se encontraron compras anteriores para este cliente.</p>
        <p className="hint">Usa el "Catálogo" para añadir productos manualmente.</p>
      </div>
    );
  }

  return (
    <div className="historial-container">
      <div className="historial-header">
        <h3>Últimas compras del cliente</h3>
        <button className="btn-mismas-unidades" onClick={aplicarMismasUnidades}>
          Mismas unidades
        </button>
      </div>

      <div className="historial-table-wrapper">
        <table className="historial-table">
          <thead>
            <tr>
              <th>Producto</th>
              <th className="text-right">Últ. Cant.</th>
              <th className="text-right">Últ. Precio</th>
              <th className="text-center">Pedido</th>
            </tr>
          </thead>
          <tbody>
            {lineas.map((linea) => (
              <tr key={linea.id} className={linea.cantidadPedido > 0 ? 'row-active' : ''}>
                <td className="col-producto">
                  <div className="producto-nombre">{linea.nombreProducto}</div>
                  <div className="producto-codigo">{linea.productoCode}</div>
                </td>
                <td className="text-right">{linea.cantidad}</td>
                <td className="text-right">{linea.precio.toFixed(2)} €</td>
                <td className="text-center">
                  <input
                    type="number"
                    min="0"
                    step="1"
                    className="input-cantidad"
                    value={linea.cantidadPedido}
                    onChange={(e) => handleCantidadChange(linea.id, e.target.value)}
                    placeholder="0"
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="historial-footer">
        <span>
          Total líneas añadidas: <strong>{lineas.filter(l => l.cantidadPedido > 0).length}</strong>
        </span>
      </div>
    </div>
  );
}