# ReprediSL_V4

**Versió:** 4.2.1  
**Estat:** Estable i Operativo  
**Descripció:** Aplicació web comercial PWA per a la força de vendes de REPREDISL

## Resum

Sistema de gestió comercial offline-first que permet als comercials:
- Gestionar clients amb cerques ràpides (debounce)
- Consultar fitxes tècniques detallades
- Generar comandes i exportar-les a PDF amb jsPDF
- Operar en mode Offline gràcies a IndexedDB

## Estructura del Projecte

```
ReprediSL_V4/
├── Documentacion/      # Documentació completa del projecte
├── Scripts/            # Scripts d'automatització i BD
│   └── BaseDatos/     # Scripts SQL per a PostgreSQL
├── src/               # Codi font
│   ├── Frontend/      # Aplicació React + Vite
│   ├── API/           # Configuració PostgREST
│   └── Daemon/        # Dimoni C# .NET 10
├── tests/             # Suite de proves automatitzades (8 tests)
├── .env.example       # Plantilla de variables d'entorn
└── README.md          # Aquest fitxer
```

## Instal·lació Ràpida

### 1. Clonar el repositori
```bash
git clone https://github.com/teva-usuari/ReprediSL_V4.git
cd ReprediSL_V4
```

### 2. Configurar variables d'entorn
```bash
cp .env.example .env
# Editar .env amb les teves credencials
```

### 3. Instal·lar dependències del Frontend
```bash
cd src/Frontend
npm install
```

### 4. Configurar la Base de Dades PostgreSQL
```bash
# Executar l'script de creació d'esquema
psql -U postgres -f ../../Scripts/BaseDatos/crear_esquema_postgresql.sql
```

### 5. Iniciar el desenvolupament
```bash
npm run dev
```

## Proves Automatitzades

El projecte inclou 8 tests unitaris que cobreixen:
- Normalització de dades de clients i productes
- Càlculs comercials (subtotal, IVA, totals)
- Gestió de valors buits i fallbacks

Per executar-los:
```bash
npm test
# o específicament:
node --test tests/*.test.js
```

## Compilació per a Producció

```bash
cd src/Frontend
npm run build
# Els fitxers generats estaran a dist/
```

## Documentació Completa

Consulta la documentació detallada a:
- [Estat del Projecte](Documentacion/00_EstadoProyecto.md)
- [Arquitectura](Documentacion/01_Arquitectura.md)
- [Guia de Desplegament](Documentacion/05_Despliegue.md)
- [Estratègia de Proves](Documentacion/06_Pruebas.md)

## Tecnologies Utilitzades

- **Frontend:** React 18.3.1 + Vite 6.0.5
- **API:** PostgREST 16.0
- **Base de Dades:** PostgreSQL 12+
- **Persistència Local:** IndexedDB
- **Generació PDF:** jsPDF
- **Daemon:** C# .NET 10 WinForms
- **Tests:** Node.js Test Runner

## Estat Actual

✅ **Completat:**
- Frontend PWA amb capacitat offline
- Suite de tests automatitzats (8/8)
- Documentació PostgreSQL
- Variables d'entorn configurades
- Scripts de migració Access → PostgreSQL

⏳ **Pendent:**
- Compilar el daemon `ReprediTrayDaemon.exe` (requereix Windows + .NET 10 SDK)
- Vincular a repositori GitHub remot

## Llicència

Propietari de REPREDISL

---

Veure `AGENTS.md` per a regles de treball i contribució.

