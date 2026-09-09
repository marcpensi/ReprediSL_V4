import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeClient } from '../src/Frontend/src/clientApi.js';

test('normalizeClient - gestiona camps buits o indefinits correctament', () => {
  const row = {
    id_cliente: 102,
    nombre: '',
    nombre_comercial: null,
    nif: undefined,
    telefono: '933445566',
    email: ''
  };

  const client = normalizeClient(row);

  // El nom comercial hauria de fallback al fiscal o generar un nom per defecte
  assert.ok(client.commercial.includes('Cliente') || client.commercial === '');
  assert.equal(client.phone, '933445566');
  assert.equal(client.email, '');
});

test('normalizeClient - preserva valors numèrics en codi de client', () => {
  const row = {
    id_cliente: 999,
    nombre: 'Test Client SL'
  };

  const client = normalizeClient(row);

  assert.equal(client.code, '999');
  assert.equal(client.fiscal, 'Test Client SL');
});

test('normalizeClient - normalitza adreces de lliurament quan són buides', () => {
  const row = {
    id_cliente: 103,
    nombre: 'Client Sense Entrega',
    street: 'Carrer Principal 123',
    codigo_postal: '08001',
    city: 'Barcelona',
    state: 'Barcelona'
  };

  const client = normalizeClient(row);

  // Si no hi ha adreça d'entrega específica, hauria de fer servir la fiscal
  assert.equal(client.deliveryAddress.address, 'Carrer Principal 123');
  assert.equal(client.deliveryAddress.postal, '08001');
  assert.equal(client.deliveryAddress.city, 'Barcelona');
});
