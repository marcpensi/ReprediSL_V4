import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeClient, normalizeProduct } from '../src/Frontend/src/clientApi.js';

test('normalizeClient - normaliza un objeto de cliente proveniente de PostgREST', () => {
  const row = {
    id_cliente: 101,
    nombre: 'RAZON SOCIAL S.L.',
    nombre_comercial: 'COMERCO SOL',
    nif: 'B12345678',
    telefono: '912345678',
    movil: '600112233',
    email: 'contacto@comerco.es',
    web: 'https://comerco.es',
    street: 'Calle Mayor 10',
    codigo_postal: '28001',
    city: 'Madrid',
    state: 'Madrid',
    direccionenvio: 'Avda Industria 4',
    cpostalenvio: '28005',
    poblacionenvio: 'Madrid',
    provinciaenvio: 'Madrid',
    nombre_banco: 'BBVA',
    cuenta_bancaria: 'ES9100000000000000000000'
  };

  const client = normalizeClient(row);

  assert.equal(client.code, '101');
  assert.equal(client.commercial, 'COMERCO SOL');
  assert.equal(client.fiscal, 'RAZON SOCIAL S.L.');
  assert.equal(client.nif, 'B12345678');
  assert.equal(client.phone, '912345678');
  assert.equal(client.mobile, '600112233');
  assert.equal(client.fiscalAddress.address, 'Calle Mayor 10');
  assert.equal(client.deliveryAddress.address, 'Avda Industria 4');
  assert.equal(client.bank, 'BBVA');
  assert.equal(client.iban, 'ES9100000000000000000000');
});

test('normalizeProduct - normaliza valores y tipos de datos de un producto', () => {
  const row = {
    codigo: 'P100',
    nombre: 'Cerveza Especial 33cl',
    precio_venta: '1.25',
    unidades_caja: '12',
    stock: '250'
  };

  const product = normalizeProduct(row);

  assert.equal(product.code, 'P100');
  assert.equal(product.name, 'Cerveza Especial 33cl');
  assert.equal(product.price, 1.25);
  assert.equal(product.box, 12);
  assert.equal(product.stock, 250);
});

test('normalizeProduct - asigna valores por defecto seguros ante campos vacíos', () => {
  const product = normalizeProduct({});

  assert.ok(product.code.startsWith('PROD_'));
  assert.ok(product.name.startsWith('Producto '));
  assert.equal(product.price, 0);
  assert.equal(product.box, 24);
  assert.equal(product.stock, 100);
});
