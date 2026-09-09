# Estat del Projecte - ReprediSL_V4

**Data d'actualització:** 09-09-2026
**Versió:** 4.2.1
**Estat General:** Estable i Operativo (Consolidació V4.2.1 - Tests Ampliats, Documentació PostgreSQL i Variables d'Entorn)

---

## 1. Resum Executiu

`ReprediSL_V4` és l'evolució consolidada de l'aplicació web comercial per a la força de vendes de REPREDISL. El sistema permet als comercials gestionar clients, realitzar cerques ràpides amb debounce, consultar fitxes tècniques detallades i generar comandes comercials exportables instantàniament a PDF mitjançant `jsPDF`, operant tant Online com Offline gràcies a IndexedDB.

---

## 2. Fites Completades

- [x] **Consolidació V4:** Integració del codi Frontend React 18.3.1 + Vite 6.0.5 i binaris de l'API PostgREST 16.0 en l'estructura estandarditzada de V4.
- [x] **Verificació de Compilació:** Compilació de producció provada amb èxit (`npm run build` executat sense errors).
- [x] **Capacitat PWA Offline:** Service Worker (`sw.js`), `manifest.json`, detecció de connexió online/offline en viu a la interfície d'usuari.
- [x] **Catàleg de Productes i Tarifes:** Normalització d'esquemes PostgREST a IndexedDB v2 (store `productes`) amb consultes offline.
- [x] **Suite de Proves unitàries:** 8/8 proves unitàries automatitzades amb la suite nativa de Node.js (`npm test`).
- [x] **Actualitzador Automàtic MDB i Sync en Lot:** Mòdul VBA `modActBdApi.bas` optimitzat amb `BATCH_SIZE = 500`, DDL dinàmic per a camps/consultes API, i logging en viu.
- [x] **Dimoni Natiu C# WinForms (.NET 10):** Migració a executable natiu `.exe` a `src/Daemon/bin/ReprediTrayDaemon.exe` (.NET 10), protecció d'instància única amb Mutex local, consum mínim de RAM (~25MB), monitoratge visual en temps real amb ressaltat de logs en VERMELL/TARONJA/VERD i diàleg de control de ràfegues d'errors (>3) amb opcions de cancel·lació o continuació en silenci.
- [x] **Integració amb Servidor MCP-Access:** Configuració oficial del servidor MCP (`luna-soft.access-explorer`) a `.vscode/mcp.json` per a consulta i inspecció de `gestion.mdb` assistida per IA.
- [x] **Sincronització Dinàmica Postgres (`DROP CASCADE`):** Recreació automàtica de taules en PostgreSQL amb `DROP TABLE ... CASCADE` a `modActBdApi.bas` per garantir coincidència total d'esquemes amb les consultes API.
- [x] **Script de Migració .MDB Extern:** Automatització completa que clona `dborigen.mdb` -> `bddestino.mdb`, actualitza esquemes/consultes DAO i injecta el codi VBA sense intervenció manual.
- [x] **Variables d'Entorn:** Fitxer `.env.example` creat amb tota la configuració necessària per a desenvolupament.
- [x] **Documentació PostgreSQL:** Script SQL complet per a creació i inicialització de l'esquema de base de dades a `Scripts/BaseDatos/crear_esquema_postgresql.sql`.
- [ ] **Compilació del Daemon:** Pendent generar `ReprediTrayDaemon.exe` (requereix entorn Windows amb .NET 10 SDK).
- [ ] **Repositori GitHub:** Pendent vincular el projecte a un repositori remot `ReprediSL_V4`.

---

## 3. Documentació Històrica i Detallada

- Auditoria Inicial: [ESTADO_INICIAL.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/ESTADO_INICIAL.md)
- Roadmap d'Evolució: [ROADMAP_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/ROADMAP_V3.md)
- Estat de Fases: [PENDIENTES_V3.md](file:///d:/programacio/repredi/ReprediSL_V4/Documentacion/00_EstadoProyecto/PENDIENTES_V3.md)
