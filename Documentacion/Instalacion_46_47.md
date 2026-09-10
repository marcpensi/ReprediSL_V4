# Instalación de las modificaciones v4.6.0 y v4.7.0

**Proyecto:** REPREDISL_V4  
**Fecha:** 10 de septiembre de 2026  
**Autor:** Marc Pensi Herrero (Pensisoft Soluciones Informáticas)

---

## 📋 Resumen de cambios

### v4.6.0 - PIN de seguridad e historial de compras
- ✅ `config.js`: Configuración centralizada (tablas, límites, mapeo de columnas, colores de marca)
- ✅ `db.js` (v3): Nuevos stores IndexedDB (`vendedor`, `historial_compras`, `pedidos_locales`)
- ✅ `clientApi.js`: Normalizador canónico del historial, sin hardcodes
- ✅ `PinScreen.jsx`: Pantalla de acceso con SHA-256 y teclado numérico
- ✅ `HistorialPedido.jsx`: Plantilla de pedido con botón "Mismas unidades"
- ✅ CSS: Estilos para PIN e historial

### v4.7.0 - Catálogo y pantalla NuevoPedido
- ✅ `Catalogo.jsx`: Búsqueda local de productos y añadir al pedido
- ✅ `NuevoPedido.jsx`: Pantalla completa que une historial + catálogo + guardado local
- ✅ CSS: Estilos para catálogo y nuevo pedido

---

## 🚀 Instalación paso a paso

### Paso 1: Clonar o actualizar el repositorio

```powershell
cd D:\programacio\repredi
git clone https://github.com/marcpensi/repredisl_v4.git
# O si ya lo tienes:
cd ReprediSL_V4
git pull origin main