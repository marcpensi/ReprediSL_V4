# Guía de Despliegue - ReprediSL_V4

**Estrategia de Despliegue:** Frontend Estático (CDN/Web Server) + API Backend Desacoplada (PostgREST sobre Windows Server / VPS)

---

## 1. Despliegue del Frontend (Aplicación Web / PWA)

1. Ejecutar la compilación de producción en `src/Frontend`:
   ```bash
   npm run build
   ```
2. Subir los archivos generados en la carpeta `src/Frontend/dist/` al servidor web de destino (Hostinger, Nginx, Apache o hosting comercial).
3. Asegurar que las peticiones se redirijan a `index.html` para permitir el correcto funcionamiento de la SPA.
4. Configurar la variable de entorno `VITE_API_URL` en la compilación apuntando a la URL pública del backend HTTPS (`https://api.repredisl.com`).

---

## 2. Despliegue del Backend API (PostgREST + Caddy)

1. **Configuración PostgREST:**
   - Copiar `src/API/postgrest.conf.example` a `src/API/postgrest.conf`.
   - Configurar la cadena de conexión a la base de datos PostgreSQL de producción (`db-uri`).
2. **Caddy Reverse Proxy & SSL:**
   - Configurar el dominio en `Caddyfile` para la obtención y renovación automática de certificados HTTPS mediante Let's Encrypt.
   - Habilitar el redireccionamiento del puerto 443 HTTPS al puerto interno 3000 de PostgREST.
3. **Persistencia como Servicio Windows:**
   - Registrar `postgrest.exe` como servicio de Windows mediante NSSM o tarea de sistema.
