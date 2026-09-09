-- Script de creació i inicialització de l'esquema PostgreSQL per a ReprediSL_V4
-- Versió: 4.2.0
-- Data: 2026-09-09

-- ============================================================================
-- 1. CREACIÓ DE LA BASE DE DADES (si no existeix)
-- ============================================================================
-- Nota: Executar això com a superusuari o usuari amb privilegis de creació de BD
-- CREATE DATABASE repredisl WITH ENCODING 'UTF8' LC_COLLATE='ca_ES.UTF-8' LC_CTYPE='ca_ES.UTF-8';

-- Connectar-se a la base de dades repredisl abans d'executar la resta de scripts

-- ============================================================================
-- 2. TAULES PRINCIPALS
-- ============================================================================

-- Taula de clients
CREATE TABLE IF NOT EXISTS clients (
    id_cliente SERIAL PRIMARY KEY,
    nombre VARCHAR(200),
    nombre_comercial VARCHAR(200),
    nif VARCHAR(20),
    telefono VARCHAR(20),
    movil VARCHAR(20),
    email VARCHAR(100),
    web VARCHAR(200),
    street VARCHAR(200),
    codigo_postal VARCHAR(10),
    city VARCHAR(100),
    state VARCHAR(100),
    direccionenvio VARCHAR(200),
    cpostalenvio VARCHAR(10),
    poblacionenvio VARCHAR(100),
    provinciaenvio VARCHAR(100),
    nombre_banco VARCHAR(100),
    cuenta_bancaria VARCHAR(50),
    id_formapago INTEGER,
    id_industria INTEGER,
    id_vendedor INTEGER,
    fecha_alta TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    fecha_modificacion TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Índexs per a optimitzar cerques
CREATE INDEX IF NOT EXISTS idx_clients_nom ON clients(nombre);
CREATE INDEX IF NOT EXISTS idx_clients_nomcomercial ON clients(nombre_comercial);
CREATE INDEX IF NOT EXISTS idx_clients_nif ON clients(nif);
CREATE INDEX IF NOT EXISTS idx_clients_ciutat ON clients(city);

-- Taula de productes
CREATE TABLE IF NOT EXISTS productes (
    id_producte SERIAL PRIMARY KEY,
    codigo VARCHAR(50) UNIQUE NOT NULL,
    nombre VARCHAR(200) NOT NULL,
    descripcio TEXT,
    preu_cost NUMERIC(10,4),
    preu_venta NUMERIC(10,4) NOT NULL,
    unitats_caja INTEGER DEFAULT 24,
    stock INTEGER DEFAULT 0,
    categoria VARCHAR(100),
    familia VARCHAR(100),
    actiu BOOLEAN DEFAULT TRUE,
    data_alta TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    data_modificacio TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Índexs per a productes
CREATE INDEX IF NOT EXISTS idx_productes_codigo ON productes(codigo);
CREATE INDEX IF NOT EXISTS idx_productes_nom ON productes(nombre);
CREATE INDEX IF NOT EXISTS idx_productes_categoria ON productes(categoria);

-- Taula de comandes / pedidos
CREATE TABLE IF NOT EXISTS comandes (
    id_comanda SERIAL PRIMARY KEY,
    id_client INTEGER REFERENCES clients(id_cliente),
    data_comanda TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    estat VARCHAR(50) DEFAULT 'pendent', -- pendent, confirmada, enviada, facturada
    subtotal NUMERIC(12,2),
    iva NUMERIC(12,2),
    total NUMERIC(12,2),
    notes TEXT,
    data_enviament TIMESTAMP,
    data_facturacio TIMESTAMP
);

-- Índexs per a comandes
CREATE INDEX IF NOT EXISTS idx_comandes_client ON comandes(id_client);
CREATE INDEX IF NOT EXISTS idx_comandes_data ON comandes(data_comanda);
CREATE INDEX IF NOT EXISTS idx_comandes_estat ON comandes(estat);

-- Taula de línies de comanda
CREATE TABLE IF NOT EXISTS comandes_linies (
    id_linia SERIAL PRIMARY KEY,
    id_comanda INTEGER REFERENCES comandes(id_comanda) ON DELETE CASCADE,
    id_producte INTEGER REFERENCES productes(id_producte),
    quantitat INTEGER NOT NULL DEFAULT 1,
    preu_unitari NUMERIC(10,4) NOT NULL,
    descompte NUMERIC(5,2) DEFAULT 0,
    total_linia NUMERIC(12,2)
);

-- Índexs per a línies de comanda
CREATE INDEX IF NOT EXISTS idx_linies_comanda ON comandes_linies(id_comanda);
CREATE INDEX IF NOT EXISTS idx_linies_producte ON comandes_linies(id_producte);

-- Taula de formes de pagament
CREATE TABLE IF NOT EXISTS formes_pagament (
    id_formapago SERIAL PRIMARY KEY,
    nom VARCHAR(100) NOT NULL,
    descripcio TEXT,
    dies_pagament INTEGER DEFAULT 0,
    actiu BOOLEAN DEFAULT TRUE
);

-- Taula d'industries / sectors
CREATE TABLE IF NOT EXISTS industries (
    id_industria SERIAL PRIMARY KEY,
    nom VARCHAR(100) NOT NULL,
    descripcio TEXT,
    actiu BOOLEAN DEFAULT TRUE
);

-- Taula de venedors / comercials
CREATE TABLE IF NOT EXISTS venedors (
    id_venedor SERIAL PRIMARY KEY,
    nom VARCHAR(100) NOT NULL,
    cognoms VARCHAR(100),
    email VARCHAR(100),
    telefon VARCHAR(20),
    actiu BOOLEAN DEFAULT TRUE
);

-- ============================================================================
-- 3. DADES INICIALS (SEED DATA)
-- ============================================================================

-- Formes de pagament bàsiques
INSERT INTO formes_pagament (nom, descripcio, dies_pagament) VALUES
    ('Comptat', 'Pagament al comptat', 0),
    ('Transferència 30 dies', 'Transferència bancària a 30 dies', 30),
    ('Transferència 60 dies', 'Transferència bancària a 60 dies', 60),
    ('Rebut domiciliat', 'Cobrament per rebut domiciliat', 30),
    ('Xec', 'Pagament amb xec', 0)
ON CONFLICT DO NOTHING;

-- Industries / sectors comuns
INSERT INTO industries (nom, descripcio) VALUES
    ('Restauració', 'Bars, restaurants, cafeteries'),
    ('Hoteleria', 'Hotels, hostals, apartaments turístics'),
    ('Retail', 'Botigues, supermercats, comerços'),
    ('Institucions', 'Escoles, hospitals, administracions'),
    ('Distribució', 'Distribuïdors i majors')
ON CONFLICT DO NOTHING;

-- ============================================================================
-- 4. VISTES PER A POSTGREST
-- ============================================================================

-- Vista de clients amb informació completa
CREATE OR REPLACE VIEW vw_clients AS
SELECT 
    c.*,
    fp.nom AS nom_forma_pago,
    i.nom AS nom_industria,
    v.nom AS nom_venedor
FROM clients c
LEFT JOIN formes_pagament fp ON c.id_formapago = fp.id_formapago
LEFT JOIN industries i ON c.id_industria = i.id_industria
LEFT JOIN venedors v ON c.id_vendedor = v.id_venedor;

-- Vista de productes amb estoc actualitzat
CREATE OR REPLACE VIEW vw_productes AS
SELECT 
    p.*,
    CASE 
        WHEN p.stock > 0 THEN 'Disponible'
        ELSE 'Esgotat'
    END AS disponibilitat
FROM productes p
WHERE p.actiu = TRUE;

-- Vista de comandes amb detalls
CREATE OR REPLACE VIEW vw_comandes AS
SELECT 
    c.id_comanda,
    c.data_comanda,
    cl.nombre AS client_nom,
    cl.nif AS client_nif,
    c.estat,
    c.subtotal,
    c.iva,
    c.total,
    COUNT(cln.id_linia) AS num_línies
FROM comandes c
JOIN clients cl ON c.id_client = cl.id_cliente
LEFT JOIN comandes_linies cln ON c.id_comanda = cln.id_comanda
GROUP BY c.id_comanda, c.data_comanda, cl.nombre, cl.nif, c.estat, c.subtotal, c.iva, c.total;

-- ============================================================================
-- 5. TRIGGERS PER ACTUALITZACIÓ AUTOMÀTICA
-- ============================================================================

-- Trigger per actualitzar fecha_modificació en clients
CREATE OR REPLACE FUNCTION update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fecha_modificacion = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_update_clients
BEFORE UPDATE ON clients
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

-- Trigger similar per productes
CREATE TRIGGER trg_update_productes
BEFORE UPDATE ON productes
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

-- ============================================================================
-- 6. ROLS I PERMISOS (OPCIONAL)
-- ============================================================================

-- Crear rol per a l'aplicació
-- CREATE ROLE repredisl_app WITH LOGIN PASSWORD 'password_segur';
-- GRANT USAGE ON SCHEMA public TO repredisl_app;
-- GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO repredisl_app;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO repredisl_app;

-- Rol de només lectura per a usuaris externs
-- CREATE ROLE repredisl_readonly WITH LOGIN PASSWORD 'password_segur';
-- GRANT USAGE ON SCHEMA public TO repredisl_readonly;
-- GRANT SELECT ON ALL TABLES IN SCHEMA public TO repredisl_readonly;

-- ============================================================================
-- FI DEL SCRIPT
-- ============================================================================
