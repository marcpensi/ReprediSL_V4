# REPREDISL Comercial — versión consolidada

Prototipo React/Vite consolidado con el flujo acordado.

## Ejecutar

```powershell
npm install
npm run dev
```

## Comprobar build

```powershell
npm run build
```

## Incluido

- Colores REPREDISL: rojo, blanco y negro.
- Navegación inferior: Clientes, Pedidos, Catálogo, Config.
- Clientes es la entrada principal.
- Búsqueda por código, nombre comercial y razón/nombre fiscal.
- Teléfono clicable y WhatsApp en verde.
- Correo y web clicables en ficha de cliente.
- Al pulsar un cliente se abre directamente su lista de pedidos.
- Botón Volver y botón + para nuevo pedido del cliente.
- Ver/editar ficha de cliente desde la lista de pedidos.
- Crear y editar clientes; no existe borrado.
- Datos cliente: código, nombre comercial, razón social, NIF/CIF, direcciones fiscal y reparto desglosadas (dirección, CP, población, provincia), teléfono, móvil, email, web, banco, IBAN, control y última sincronización.
- Vendedores 1..13 con nombres capitalizados y series V1..V9, VA..VD.
- Nuevo pedido con vendedor, serie y siguiente número de esa serie.
- Cargar historial de compras opcional.
- Una última compra por producto y cliente en los datos de ejemplo.
- Unidades/caja visibles en catálogo y líneas de pedido.
- Cantidad de pedido numérica editable sin límite 999.
- Botón “Mismas” para recuperar cantidad de última compra y X para quitar línea.
- Catálogo para añadir productos no presentes en historial.
- Al añadir desde catálogo aparece diálogo de unidades con Cancelar/Aceptar.
- Si el artículo tiene unidades predeterminadas, aparecen precargadas.
- Comprobar existencias y Actualizar precio solo cuando está Online.
- Hacer pedido genera un PDF real con jsPDF y guarda el pedido en el estado de la app.
- Detalle de pedidos existentes con regeneración de PDF y acción Reenviar.
- Configuración y sincronización con vendedor/serie.

## Aún simulado hasta conectar la API

El frontend ya genera el PDF. El registro HTTP real en el servidor y el envío automático del PDF por correo necesitan conectar la API/servicio de correo. En esta versión se representan en la interfaz y el pedido queda registrado en el estado local de la app.
