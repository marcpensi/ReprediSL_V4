# Funcionalidad del Sistema - ReprediSL_V4

**Proyecto:** ReprediSL_V4  
**Área:** Aplicación Comercial de Campo

---

## 1. Módulos Funcionales Principales

### 1.1 Gestión de Clientes
- **Carga Inmediata:** Carga instantánea de clientes guardados en la base de datos local IndexedDB al iniciar la aplicación.
- **Búsqueda Avanzada:** Búsqueda en tiempo real con *debounce* de 300ms contra el backend PostgREST al escribir 2 o más caracteres (búsqueda por nombre comercial, razón social, NIF o código numérico).
- **Ficha Técnica Detallada:** Muestra datos fiscales completos, direcciones de envío y facturación desglosadas, teléfonos clicables, enlace directo a WhatsApp, correo electrónico, web, datos bancarios (IBAN) y vendedor asignado.
- **Creación y Edición:** Permite registrar nuevos clientes o modificar datos existentes.

### 1.2 Gestión de Pedidos Comercial
- **Selección de Comercial:** Selección de serie de vendedor (`V1`..`V9`, `VA`..`VD`) con correlativo automático por serie.
- **Catálogo y Líneas de Pedido:** Gestión de unidades por caja, precios unitarios e historial de compras previas por cliente.
- **Función "Mismas":** Copia rápida de las cantidades compradas en el último pedido del cliente.
- **Generación de PDF:** Creación instantánea en el dispositivo del PDF oficial del pedido mediante `jsPDF`, listo para compartir por WhatsApp o email.

### 1.3 Configuración y Caché
- **Gestión de Memoria:** Vaciamiento manual de caché local e indicador de sincronización.
- **Monitoreo de Red:** Próximamente indicador activo Online / Offline en la barra superior.

---

## 2. Especificaciones de la API REST

- Documentación y Ejemplos API: [API_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/02_API/API_V3.md)
