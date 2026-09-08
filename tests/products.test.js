import { test } from 'node:test';
import assert from 'node:assert/strict';

function calculateOrderTotal(lines, vatRate = 0.21) {
  const subtotal = lines.reduce((acc, line) => acc + (line.qty * line.price), 0);
  const vat = subtotal * vatRate;
  const total = subtotal + vat;
  return {
    subtotal: Number(subtotal.toFixed(2)),
    vat: Number(vat.toFixed(2)),
    total: Number(total.toFixed(2))
  };
}

test('calculateOrderTotal - calcula subtotal, IVA 21% y total correctamente', () => {
  const lines = [
    { code: 'A001', qty: 24, price: 1.12 }, // 26.88
    { code: 'A002', qty: 12, price: 0.84 }  // 10.08
  ];

  const result = calculateOrderTotal(lines);

  assert.equal(result.subtotal, 36.96);
  assert.equal(result.vat, 7.76);
  assert.equal(result.total, 44.72);
});

test('calculateOrderTotal - maneja pedidos vacíos con total cero', () => {
  const result = calculateOrderTotal([]);

  assert.equal(result.subtotal, 0);
  assert.equal(result.vat, 0);
  assert.equal(result.total, 0);
});
