# Guía de Desarrollo - ReprediSL_V4

**Requisitos Previos:**
- Node.js v18+ o v20+
- npm v9+
- Windows PowerShell / CMD (para ejecutables de la API)

---

## 1. Configuración del Entorno Frontend

Navegar a la carpeta del Frontend:
```bash
cd src/Frontend
```

### Instalar Dependencias
```bash
npm install
```

### Iniciar Servidor de Desarrollo
```bash
npm run dev
```
Acceder a la URL local indicada por Vite (normalmente `http://localhost:5173`).

### Compilar para Producción
```bash
npm run build
```
Genera la carpeta `dist/` optimizada y minificada en `src/Frontend/dist`.

---

## 2. Configuración de la API (Backend Local)

Navegar a la carpeta de la API:
```bash
cd src/API
```

### Iniciar PostgREST
Ejecutar el script de arranque incluido en `Scripts`:
```cmd
Scripts\Desarrollo\ARRANCAR_POSTGREST.bat
```
Verificar conectividad local en `http://127.0.0.1:3000/clientes?limit=1`.

---

## 3. Guías y ADRs de Referencia

- Guía de Desarrollo Completa: [GUIA_DESARROLLO_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/03_Desarrollo/GUIA_DESARROLLO_V3.md)
- Registro de Decisiones de Arquitectura Frontend: [ADR-001-Frontend.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/06_Decisiones/ADR-001-Frontend.md)
- Decisiones Técnicas Generales: [DECISIONES_TECNICAS.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/06_Decisiones/DECISIONES_TECNICAS.md)
