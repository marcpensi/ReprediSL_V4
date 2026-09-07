# REPREDISL V3 - COMPARATIVA DE VERSIONES

**Documento:** Comparativa detallada entre fuentes de código y documentación.  
**Origen A:** `REPREDISL_Definitiva_v1` (Código Avanzado React/Vite)  
**Origen B:** `REPREDISL_FINAL_FUNCIONAL_v2_TARIFA` (Documentación y Referencia Vanilla JS)  

---

## 1. Clasificación de Criterios

- **Categoría A (Incorporar en V3):** Características o artefactos superiores que formarán parte del núcleo de V3.
- **Categoría B (Revisar antes de incorporar):** Diseños, tablas o funcionalidades que aportan valor pero requieren adaptación técnica previa.
- **Categoría C (No incorporar):** Enfoques desactualizados, inconsistentes o redundantes que deben descartarse.

---

## 2. Matriz Comparativa y Clasificación

| Componente / Característica | Origen A (`Definitiva_v1`) | Origen B (`FINAL_FUNCIONAL`) | Clasificación V3 | Justificación Técnico-Estratégica |
|---|---|---|---|---|
| **Framework Core UI** | React 18.3.1 + Vite 6.0.5 | Vanilla JavaScript ES6 | **A (Origen A)** | React/Vite proporciona una arquitectura de componentes reactivos, mantenible, con excelente rendimiento PWA para dispositivos móviles de vendedores. |
| **Persistencia Local y Caché** | IndexedDB (`REPREDISL` DB, Límite 300 ítems) | `localStorage` simple (~5MB límite) | **A (Origen A)** | IndexedDB permite almacenar grandes volúmenes de datos estructurados sin bloquear el hilo principal y soporta consultas complejas offline. |
| **Integración y Búsqueda API** | PostgREST con debounce (300ms) y límite de 30 clientes | Fetch directo simple sin debounce explícito | **A (Origen A)** | Previene sobrecargar la base de datos y la red móvil del vendedor durante la búsqueda de clientes. |
| **Generación de Documentos** | jsPDF 2.5.2 en el cliente | Impresión HTML nativa | **A (Origen A)** | Permite generar archivos PDF estructurados de pedidos al instante, descargables y listos para enviar por email o WhatsApp. |
| **Esquema de Documentación** | Archivos TXT breves | 9 Archivos Markdown estructurados | **A (Origen B)** | La estructura de documentación de Origen B es altamente profesional y se adapta como documentación viva para V3. |
| **Gestión de Tarifas (v2_TARIFA)** | No presente / simulado en cliente | Esquema de tabla `tarifas` en PostgREST | **B (Origen B)** | El modelo de tablas de tarifas de Origen B es adecuado para el backend, pero debe integrarse con el modelo de datos de React en V3. |
| **Eliminación de Bundler (Vanilla JS puro)** | No implementado | Propuesto como refactorización | **C (Origen B)** | Sustituir React/Vite por Vanilla JS puro es un retroceso en mantenibilidad y escalabilidad para la versión V3. |
| **Scripts de Servidor API Windows** | No incluidos en el frontend | Incluidos en `API_REPREDISL_WINDOWS` | **A (API)** | La carpeta API independiente con PostgREST y Caddy proporciona el entorno backend listo para desplegar en Windows. |

---

## 3. Plan de Adopción para V3

1. **Frontend:** Migrar íntegramente la base de código de `REPREDISL_Definitiva_v1` a `ReprediSL_V3/Frontend`.
2. **Documentación:** Migrar, adaptar y ampliar la documentación de `REPREDISL_FINAL_FUNCIONAL` a la estructura numerada de `ReprediSL_V3/Documentacion`.
3. **Backend API:** Consolidar los ejecutables, configuraciones y scripts de `API_REPREDISL_WINDOWS` en `ReprediSL_V3/API`.
4. **Tarifas & Futuras Extensiones:** Mantener en la documentación del backend la especificación del esquema de tarifas para la FASE posterior a la integración inicial.
