# Registro de Incidencias - ReprediSL_V4

---

## Incidencia 001 - Acceso Denegado por Sandbox en Ejecución de Build

### Síntoma
La herramienta `run_command` devolvió un error de acceso denegado durante el primer intento de verificación de build (`npm run build`) en `src/Frontend`.

### Diagnóstico
El entorno de sandbox aislado restringió la ejecución por accesos cruzados a rutas fuera del espacio de trabajo.

### Causa
Restricción predeterminada del sandbox de ejecución de comandos.

### Solución
Se ejecutó la compilación mediante un proceso asíncrono con bypass de sandbox autorizado (`task-63`), completando la compilación de Vite de forma limpia en 6.85s con 0 errores.

### Tecnología
Vite 6.0.5 / Node.js

### Herramienta utilizada
`run_command` (asíncrono) + `manage_task`

### Reutilizable
Sí

---

## Incidencia 002 - Error de Sintaxis en ARRANCAR_CADDY.bat y Fallo de Carga de Clientes

### Síntoma
Al abrir `https://pedidos.repredisl.com`, la aplicación web PWA muestra que no puede conectar con la API ni cargar los clientes, a pesar de haber ejecutado `INICIAR_SISTEMA_COMPLETO.bat`.

### Diagnóstico
1. `postgrest.exe` funcionaba en el puerto 3000 contra la base de datos `repredisl_api` (3.180 clientes en `api.clientes`).
2. Sin embargo, el proxy inverso `caddy.exe` (encargado del puerto 443 HTTPS y los certificados SSL Let's Encrypt para `api.repredisl.com`) no estaba en ejecución.
3. Al invocar `Scripts\Desarrollo\ARRANCAR_CADDY.bat`, el intérprete de comandos `cmd.exe` abortaba de inmediato con el error: `No se esperaba . en este momento.`
4. Adicionalmente, al probar en el navegador del mismo equipo que hospeda el servidor, las peticiones hacia el dominio público se veían bloqueadas por falta de NAT Loopback en el router local.

### Causa
En `ARRANCAR_CADDY.bat`, dentro de un bloque condicional `if (...)`, la línea `echo [INFO] Caddy ya esta ejecutandose en segundo plano (puertos 80 y 443 activos).` contenía paréntesis y un punto final no escapados. El parser de CMD interpretaba el cierre del paréntesis como el final del bloque `if`, provocando la terminación inmediata del script antes de lanzar Caddy.

### Solución
1. Se corrigió `Scripts\Desarrollo\ARRANCAR_CADDY.bat` limpiando la comprobación del proceso con `findstr /i "caddy.exe"` y eliminando los paréntesis anidados.
2. Se verificó el arranque de Caddy y se confirmó mediante `curl` que el endpoint HTTPS `https://api.repredisl.com/clientes` responde `HTTP 200 OK` con cabeceras CORS correctas para `https://pedidos.repredisl.com`.
3. Se creó el script auxiliar `Scripts\Desarrollo\CONFIGURAR_HOSTS_LOCAL.bat` para resolver automáticamente `127.0.0.1 api.repredisl.com` en el archivo `hosts` cuando se realicen pruebas desde el mismo ordenador.

### Tecnología
Caddy Server 2 / Windows CMD / PostgREST 16

### Herramienta utilizada
`replace_file_content` + `write_to_file` + `run_command`

### Reutilizable
Sí

---

## Plantilla para Nuevas Incidencias

### Síntoma
### Diagnóstico
### Causa
### Solución
### Tecnología
### Herramienta utilizada
### Reutilizable
Sí / No / Parcial
