# Estratègia i Registre de Proves - ReprediSL_V4

**Estat de Proves:** Verificació de Compilació Completada | Tests Automatitzats Implementats (8/8)

---

## 1. Proves de Compilació i Sintaxi

- **Eina:** Vite 6.0.5 (`vite build`)
- **Resultat:** **EXITÓS (0 errors)**
- **Detalls del Build:**
  - 1.800 mòduls transformats i integrats.
  - Temps total de compilació: 6.85 segons.
  - Bundle generat a `src/Frontend/dist`.

---

## 2. Proves de Persistència i API (Manuals)

- **Cerca amb Debounce:** Verificat l'enviament de peticions HTTP `GET /clients?or=...` després de 300ms d'inactivitat de teclat.
- **IndexedDB Fallback:** Verificada la càrrega de fins a 300 clients a la base de dades `REPREDISL` del navegador en mode offline.
- **Generació de PDF:** Verificada la creació i descàrrega de documents de comanda en client utilitzant `jsPDF`.

---

## 3. Suite de Proves Automatitzades (Implementada)

- **Directori de Tests:** `tests/`
- **Eina:** Node.js Test Runner (`node --test`)
- **Total de Tests:** 8 proves automatitzades

### 3.1 Tests Implementats

#### `clientApi.test.js` (3 tests)
1. ✅ `normalizeClient` - Normalitza un objecte de client provinent de PostgREST
2. ✅ `normalizeProduct` - Normalitza valors i tipus de dades d'un producte
3. ✅ `normalizeProduct` - Assigna valors per defecte segurs davant camps buits

#### `products.test.js` (2 tests)
4. ✅ `calculateOrderTotal` - Calcula subtotal, IVA 21% i total correctament
5. ✅ `calculateOrderTotal` - Maneja comandes buides amb total zero

#### `dbSync.test.js` (3 tests) - NOU
6. ✅ `normalizeClient` - Gestiona camps buits o indefinits correctament
7. ✅ `normalizeClient` - Preserva valors numèrics en codi de client
8. ✅ `normalizeClient` - Normalitza adreces de lliurament quan són buides

### 3.2 Execució de Tests

Per executar tota la suite de proves:

```bash
npm test
```

O específicament:

```bash
node --test tests/*.test.js
```

### 3.3 Cobertura de Funcionalitats

- **Normalització de Dades:** Clients i productes
- **Càlculs Comercials:** Subtotal, IVA, totals de comanda
- **Gestió de Errors:** Valors buits, nulls, undefined
- **Fallbacks:** Adreces de lliurament, noms per defecte

---

## 4. Pròximes Passes en Qualitat

- [ ] Implementar proves d'integració per a sincronització IndexedDB
- [ ] Afegir proves E2E amb Playwright o Cypress
- [ ] Configurar cobertura de codi amb c8 o istanbul
