# GUÍA DE DESARROLLO - REPREDISL V3

**Proyecto:** REPREDISL_V3  
**Entorno Recomendado:** Node.js v18+, PowerShell, VS Code.  

---

## 1. Requisitos Previos

- **Node.js:** v18.0.0 o superior.
- **npm:** v9.0.0 o superior.
- **PostgREST:** v16.0 (incluido en la carpeta `API/`).
- **PostgreSQL:** v12+ (con la base de datos `repredisl` activa).

---

## 2. Configuración e Instalación del Frontend

```powershell
# Ir a la carpeta Frontend
cd d:\programacio\repredi\ReprediSL_V3\Frontend

# Instalar dependencias
npm install

# Ejecutar servidor de desarrollo Vite
npm run dev
```

Por defecto, la aplicación estará disponible en `http://localhost:5173` o en el puerto indicado por Vite.

---

## 3. Configuración de Variables de Entorno

Crear un archivo `.env` en la raíz de `Frontend`:

```env
# Entorno local
VITE_API_URL=http://127.0.0.1:3000

# Para producción (Hostinger / Reverse Proxy)
# VITE_API_URL=/api
```

---

## 4. Compilación para Producción

```powershell
npm run build
```
Generará la carpeta `dist/` optimizada para subir al servidor web (ej. Hostinger).

---

## 5. Ejecución del Backend API (Windows)

```powershell
cd d:\programacio\repredi\ReprediSL_V3\API
.\ARRANCAR_POSTGREST.bat
```
