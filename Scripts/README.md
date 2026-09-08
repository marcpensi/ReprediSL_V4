# Índice de Scripts - ReprediSL_V4

Este directorio contiene la suite de scripts utilitarios y de automatización para `ReprediSL_V4`, organizados por categorías:

## Categorías
- **Bootstrap:** Scripts de inicialización del entorno.
- **Desarrollo:** Scripts para ejecución y prueba local.
- **BaseDatos:** Scripts de gestión de esquemas SQL y migraciones desde Access.
- **Migracion:** Utilidades de migración de datos.
- **Despliegue:** Scripts de empaquetado y despliegue a producción.
- **Mantenimiento:** Tareas de limpieza y mantenimiento.
- **Diagnostico:** Scripts de salud e inspección de servicios.
- **Utilidades:** Herramientas varias de soporte.

---

## Scripts Disponibles en `Scripts/Migracion/` y `Scripts/Desarrollo/`

### 1. [`MigrarYActualizarAccess.ps1`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Migracion/MigrarYActualizarAccess.ps1) & [`MIGRAR_Y_ACTUALIZAR_ACCESS.bat`](file:///d:/programacio/repredi/ReprediSL_V4/Scripts/Desarrollo/MIGRAR_Y_ACTUALIZAR_ACCESS.bat)
- **Objetivo:** Automatiza el flujo completo de cliente en 1 solo clic:
  1. Copia `GESTION_ACTUAL.MDB` (o `dborigen.mdb`) hacia `bddestino.mdb`.
  2. Crea y actualiza dinámicamente las 5 consultas API (`QryClientesApi`, `QryVendedoresApi`, `QryTarifasApi`, `QryPreciosApi`, `QryUventasApi`).
  3. Inyecta / actualiza automáticamente el módulo VBA [`modActBdApi.bas`](file:///d:/programacio/repredi/ReprediSL_V4/src/Access/modActBdApi.bas) en la base de datos resultante mediante COM Automation.
- **Uso:** Hacer doble clic en `Scripts\Desarrollo\MIGRAR_Y_ACTUALIZAR_ACCESS.bat`.

### 2. `ARRANCAR_POSTGREST.bat`
- **Objetivo:** Inicia el servidor PostgREST 16 empleando la configuración de `src/API/postgrest.conf`.
- **Uso:** `Scripts\Desarrollo\ARRANCAR_POSTGREST.bat`

### 3. `BUILD_PRODUCCION.bat`
- **Objetivo:** Ejecuta el proceso de compilación optimizado para producción en `src/Frontend`.
- **Uso:** `Scripts\Desarrollo\BUILD_PRODUCCION.bat`

### 4. `PROBAR_API_LOCAL.bat`
- **Objetivo:** Realiza una petición de prueba contra `http://127.0.0.1:3000/clientes?limit=1` para verificar el estado de la API.
- **Uso:** `Scripts\Desarrollo\PROBAR_API_LOCAL.bat`

### 5. `ABRIR_FIREWALL_HTTPS.bat`
- **Objetivo:** Configura las reglas de entrada en el Firewall de Windows Defender para permitir tráfico en puertos de API/HTTPS.
