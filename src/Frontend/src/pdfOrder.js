import { jsPDF } from 'jspdf';
import { REPREDI_LOGO_BASE64 } from './reprediLogoBase64.js';

/**
 * Formatea un número a estilo español con 2 decimales y coma.
 */
function fmtNum(num) {
  const val = Number(num) || 0;
  return val.toLocaleString('es-ES', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

/**
 * Genera el documento PDF del pedido con el diseño oficial idéntico al de las facturas de Repredi SL.
 * Incluye cabecera corporativa, cuadro de metadatos con VENDEDOR, caja de cliente, tabla de artículos,
 * desglose de IVA/Bases, Total Pedido y pie legal LOPD.
 *
 * @param {Object} order - Objeto pedido ({ series, number, date, lines, sellerName, ... })
 * @param {Object} client - Objeto cliente ({ code, commercial, fiscal, nif, fiscalAddress, deliveryAddress, paymentMethod, bank, iban, ... })
 * @param {number} total - Importe total del pedido
 * @param {Object} [terminalConfig] - Configuración activa del terminal ({ sellerName, sellerId, series, ... })
 * @returns {jsPDF} Instancia del documento jsPDF
 */
export function buildOrderPdf(order, client, total, terminalConfig = {}) {
  const doc = new jsPDF({
    orientation: 'portrait',
    unit: 'mm',
    format: 'a4'
  });

  const c = client || {};
  const ord = order || {};
  const lines = (ord.lines || []).filter(l => Number(l.qty) > 0 && l.validated !== false);

  // Vendedor: prioridad en orden -> terminalConfig -> cliente -> default
  const seller = ord.sellerName || ord.vendedor || terminalConfig.sellerName || c.sellerName || '13 - Jose';
  const orderSeries = ord.series || ord.serie || terminalConfig.series || 'VD';
  const orderNum = ord.number || ord.numero_pedido || '';
  const orderDate = ord.date || ord.fecha || new Date().toLocaleDateString('es-ES');

  const xLeft = 14;
  const xRight = 196;
  const contentWidth = xRight - xLeft; // 182 mm

  // ==========================================
  // 1. CABECERA IZQUIERDA: LOGO Y DATOS FISCALES DE LA EMPRESA
  // ==========================================
  try {
    if (REPREDI_LOGO_BASE64) {
      // Logo Repredi oficial
      doc.addImage(REPREDI_LOGO_BASE64, 'PNG', xLeft, 12, 46, 26);
    }
  } catch (err) {
    console.warn('No se pudo cargar el logo en el PDF:', err);
  }

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(7.5);
  doc.setTextColor(30, 30, 30);
  doc.text('REPRESENTACIONES Y DISTRIBUCIONES, S. L.', xLeft, 41);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(7);
  doc.setTextColor(70, 70, 70);
  doc.text('Avda. Hnos. Bou Km. 2 - Tel. 964 22 74 00 - Fax 964 22 08 05', xLeft, 45);
  doc.text('12003 - CASTELLÓN', xLeft, 48.8);
  doc.text('C.I.F. B-12043303', xLeft, 52.6);

  // Título del documento: "PEDIDO"
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(16);
  doc.setTextColor(20, 20, 20);
  doc.text('PEDIDO', xLeft + 22, 64, { align: 'center' });

  // ==========================================
  // 2. CABECERA DERECHA: TABLA DE METADATOS Y CAJA DE CLIENTE
  // ==========================================
  const xBox = 112;
  const wBox = 84;

  // 2.1. Tabla de Metadatos (Número | Fecha | Vendedor | N.I.F.)
  const colW = [20, 20, 24, 20]; // Total 84 mm
  const colX = [
    xBox,
    xBox + colW[0],
    xBox + colW[0] + colW[1],
    xBox + colW[0] + colW[1] + colW[2]
  ];

  doc.setLineWidth(0.3);
  doc.setDrawColor(30, 30, 30);

  // Cabecera de metadatos (Gris claro)
  doc.setFillColor(226, 232, 240);
  doc.rect(xBox, 13, wBox, 5, 'FD');

  doc.line(colX[1], 13, colX[1], 18);
  doc.line(colX[2], 13, colX[2], 18);
  doc.line(colX[3], 13, colX[3], 18);

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(7.5);
  doc.setTextColor(15, 23, 42);
  doc.text('Número', colX[0] + colW[0] / 2, 16.8, { align: 'center' });
  doc.text('Fecha', colX[1] + colW[1] / 2, 16.8, { align: 'center' });
  doc.text('Vendedor', colX[2] + colW[2] / 2, 16.8, { align: 'center' });
  doc.text('N.I.F.', colX[3] + colW[3] / 2, 16.8, { align: 'center' });

  // Fila de valores
  doc.rect(xBox, 18, wBox, 7, 'S');
  doc.line(colX[1], 18, colX[1], 25);
  doc.line(colX[2], 18, colX[2], 25);
  doc.line(colX[3], 18, colX[3], 25);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  doc.setTextColor(15, 23, 42);

  const strNum = `${orderSeries}/ ${orderNum}`;
  doc.text(strNum, colX[0] + colW[0] / 2, 22.8, { align: 'center' });
  doc.text(String(orderDate), colX[1] + colW[1] / 2, 22.8, { align: 'center' });
  doc.text(String(seller), colX[2] + colW[2] / 2, 22.8, { align: 'center' });
  doc.text(String(c.nif || ''), colX[3] + colW[3] / 2, 22.8, { align: 'center' });

  // 2.2. Caja de Cliente (Borde negro prominente, idéntico a factura)
  const yCust = 29;
  const hCust = 33;
  doc.setLineWidth(0.6);
  doc.rect(xBox, yCust, wBox, hCust, 'S');

  const xCustText = xBox + 3.5;
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(8.5);
  doc.setTextColor(10, 10, 10);

  const fiscalName = (c.fiscal || c.commercial || `CLIENTE ${c.code || ''}`).toUpperCase();
  const addressStr = (c.fiscalAddress?.address || c.deliveryAddress?.address || '').toUpperCase();
  const postalCity = `${c.fiscalAddress?.postal || c.deliveryAddress?.postal || ''}  ${(c.fiscalAddress?.city || c.deliveryAddress?.city || '').toUpperCase()}`.trim();
  const commercialExtra = (c.commercial && c.commercial !== c.fiscal)
    ? c.commercial.toUpperCase()
    : (c.fiscalAddress?.province || c.deliveryAddress?.province || '').toUpperCase();

  doc.text(fiscalName.substring(0, 42), xCustText, yCust + 6);
  doc.text(addressStr.substring(0, 42), xCustText, yCust + 13);
  doc.text(postalCity.substring(0, 42), xCustText, yCust + 20);
  if (commercialExtra) {
    doc.text(commercialExtra.substring(0, 42), xCustText, yCust + 27);
  }

  // ==========================================
  // 3. TABLA DE ARTÍCULOS
  // ==========================================
  let yCurrent = 72;

  function renderTableHeader(y) {
    doc.setLineWidth(0.3);
    doc.setDrawColor(40, 40, 40);
    doc.setFillColor(241, 245, 249); // Fondo gris muy suave
    doc.rect(xLeft, y, contentWidth, 5.5, 'FD');

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(8);
    doc.setTextColor(15, 23, 42);

    // Artículo (X: 14..36)
    doc.text('Artículo', xLeft + 2, y + 4);
    // Descripción (X: 36..120)
    doc.text('Descripción', xLeft + 22, y + 4);
    // Unidades (X: 120..140)
    doc.text('Unidades', xLeft + 124, y + 4, { align: 'right' });
    // Precio (X: 140..158)
    doc.text('Precio', xLeft + 144, y + 4, { align: 'right' });
    // Dto (X: 158..168)
    doc.text('Dto', xLeft + 158, y + 4, { align: 'right' });
    // Importe (X: 168..196)
    doc.text('Importe', xRight - 2, y + 4, { align: 'right' });
  }

  renderTableHeader(yCurrent);
  yCurrent += 6.5;

  let totalBaseCalc = 0;
  const vatBreakdown = {}; // { '10.00': { base, iva }, '21.00': { base, iva } }

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  doc.setTextColor(15, 23, 42);

  lines.forEach((l) => {
    // Si la tabla alcanza la zona de totales en la página 1, saltar de página
    if (yCurrent > 218) {
      doc.addPage();
      yCurrent = 16;
      renderTableHeader(yCurrent);
      yCurrent += 6.5;
      doc.setFont('helvetica', 'normal');
      doc.setFontSize(8);
      doc.setTextColor(15, 23, 42);
    }

    const qty = Number(l.qty) || 0;
    const unitPrice = l.currentPrice != null ? Number(l.currentPrice) : (Number(l.price) || 0);
    const lineTotal = Number((qty * unitPrice).toFixed(2));
    totalBaseCalc += lineTotal;

    // Desglose de IVA por línea (o 10% por defecto si no especifica)
    const vatRate = Number(l.vat != null ? l.vat : (l.iva != null ? l.iva : 10));
    const rateKey = vatRate.toFixed(2);
    if (!vatBreakdown[rateKey]) {
      vatBreakdown[rateKey] = { rate: vatRate, base: 0, iva: 0 };
    }
    vatBreakdown[rateKey].base += lineTotal;

    const codeStr = String(l.code || '');
    const nameStr = String(l.name || '');
    const qtyStr = fmtNum(qty);
    const priceStr = fmtNum(unitPrice);
    const dtoStr = l.discount ? `${Number(l.discount).toFixed(0)} %` : '';
    const totalStr = fmtNum(lineTotal);

    doc.text(codeStr.substring(0, 12), xLeft + 2, yCurrent);
    doc.text(nameStr.substring(0, 52), xLeft + 22, yCurrent);
    doc.text(qtyStr, xLeft + 124, yCurrent, { align: 'right' });
    doc.text(priceStr, xLeft + 144, yCurrent, { align: 'right' });
    doc.text(dtoStr, xLeft + 158, yCurrent, { align: 'right' });
    doc.text(totalStr, xRight - 2, yCurrent, { align: 'right' });

    yCurrent += 5.2;
  });

  // Calcular importes de IVA
  let totalIvaCalc = 0;
  Object.keys(vatBreakdown).forEach(k => {
    const entry = vatBreakdown[k];
    entry.iva = Number(((entry.base * entry.rate) / 100).toFixed(2));
    totalIvaCalc += entry.iva;
  });

  const grandTotal = Number((totalBaseCalc + totalIvaCalc).toFixed(2));

  // ==========================================
  // 4. BLOQUE INFERIOR: FORMA DE PAGO, BASES/IVA Y TOTAL FACTURA / PEDIDO
  // ==========================================
  const yBottom = 226;

  // 4.1. Cuadro Izquierdo: Forma de Pago y Vencimiento
  const wPay = 96;
  doc.setLineWidth(0.3);
  doc.setDrawColor(40, 40, 40);

  // Cabecera Forma de Pago
  doc.setFillColor(226, 232, 240);
  doc.rect(xLeft, yBottom, wPay, 5, 'FD');
  doc.line(xLeft + 58, yBottom, xLeft + 58, yBottom + 5);

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(7.5);
  doc.setTextColor(15, 23, 42);
  doc.text('Forma de Pago', xLeft + 29, yBottom + 3.8, { align: 'center' });
  doc.text('Vencimiento', xLeft + 58 + 19, yBottom + 3.8, { align: 'center' });

  // Valores Forma de Pago
  doc.rect(xLeft, yBottom + 5, wPay, 7, 'S');
  doc.line(xLeft + 58, yBottom + 5, xLeft + 58, yBottom + 12);

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(8);
  const payMethod = String(c.paymentMethod || 'RESUMEN MENSUAL').toUpperCase();
  doc.text(payMethod, xLeft + 29, yBottom + 9.8, { align: 'center' });
  doc.text(String(orderDate), xLeft + 58 + 19, yBottom + 9.8, { align: 'center' });

  // Sub-caja bancaria / domiciliación
  doc.setFillColor(248, 250, 252);
  doc.rect(xLeft, yBottom + 12, wPay, 10, 'FD');
  const bankDesc = c.bank
    ? `${c.bank}${c.iban ? ' · ' + c.iban : ''}`
    : (c.iban ? `IBAN: ${c.iban}` : 'Domiciliación bancaria');
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(7.5);
  doc.setTextColor(50, 50, 50);
  doc.text(bankDesc.substring(0, 55), xLeft + wPay / 2, yBottom + 18, { align: 'center' });

  // 4.2. Cuadro Derecho: Desglose Bases Imponibles e IVA
  const xVat = xLeft + wPay + 4; // 14 + 96 + 4 = 114 mm
  const wVat = xRight - xVat;    // 196 - 114 = 82 mm

  // Cabecera Desglose IVA
  doc.setFillColor(226, 232, 240);
  doc.rect(xVat, yBottom, wVat, 5, 'FD');
  doc.line(xVat + 32, yBottom, xVat + 32, yBottom + 5);
  doc.line(xVat + 52, yBottom, xVat + 52, yBottom + 5);

  doc.setFont('helvetica', 'bold');
  doc.setFontSize(7.5);
  doc.setTextColor(15, 23, 42);
  doc.text('Base Imponible', xVat + 30, yBottom + 3.8, { align: 'right' });
  doc.text('% Iva', xVat + 48, yBottom + 3.8, { align: 'right' });
  doc.text('Importe Iva', xVat + wVat - 2, yBottom + 3.8, { align: 'right' });

  // Filas de desglose
  let yVatRow = yBottom + 5;
  const vatKeys = Object.keys(vatBreakdown);
  if (vatKeys.length === 0) {
    // Fila vacía
    doc.rect(xVat, yVatRow, wVat, 6, 'S');
    doc.line(xVat + 32, yVatRow, xVat + 32, yVatRow + 6);
    doc.line(xVat + 52, yVatRow, xVat + 52, yVatRow + 6);
    yVatRow += 6;
  } else {
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(8);
    vatKeys.forEach(k => {
      const v = vatBreakdown[k];
      doc.rect(xVat, yVatRow, wVat, 5.5, 'S');
      doc.line(xVat + 32, yVatRow, xVat + 32, yVatRow + 5.5);
      doc.line(xVat + 52, yVatRow, xVat + 52, yVatRow + 5.5);

      doc.text(fmtNum(v.base), xVat + 30, yVatRow + 4, { align: 'right' });
      doc.text(fmtNum(v.rate), xVat + 48, yVatRow + 4, { align: 'right' });
      doc.text(fmtNum(v.iva), xVat + wVat - 2, yVatRow + 4, { align: 'right' });

      yVatRow += 5.5;
    });
  }

  // 4.3. TOTAL FACTURA / TOTAL PEDIDO (Destacado)
  const yTotal = Math.max(yVatRow, yBottom + 17);
  const hTotal = 8;

  // Etiqueta "Total Pedido"
  doc.setFillColor(226, 232, 240);
  doc.rect(xVat, yTotal, 44, hTotal, 'FD');
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(9);
  doc.setTextColor(15, 23, 42);
  doc.text('Total Pedido', xVat + 4, yTotal + 5.3);

  // Valor Total en Euros
  doc.rect(xVat + 44, yTotal, wVat - 44, hTotal, 'S');
  doc.setFont('helvetica', 'bold');
  doc.setFontSize(11);
  doc.setTextColor(15, 23, 42);
  const displayTotal = total != null ? Number(total) : grandTotal;
  doc.text(`${fmtNum(displayTotal)} €`, xRight - 2, yTotal + 5.4, { align: 'right' });

  // ==========================================
  // 5. PIE LEGAL LOPD (Idéntico al texto de la factura)
  // ==========================================
  const lopdText =
    'A efectos de lo dispuesto en la Ley Orgánica 15/1999 de Protección de Datos de Carácter Personal informamos al CLIENTE que sus datos personales van a ser incorporados a un fichero del que es ' +
    'responsable Repredi S.L. con la finalidad de dar cumplimiento a la relación que se deriva del presente documento y de mantenerse informado de los servicios que siendo similares a los actuales, ' +
    'habitualmente ofrecemos a nuestros clientes en condiciones más ventajosas. No obstante, le recordamos que dispone de sus derechos de acceso, rectificación, cancelación y oposición al ' +
    'tratamiento de sus datos que podrá ejercer en nuestro domicilio, el cual se encuentra en Avda. Hnos. Bou Km. 2 de Castellón de la Plana.';

  doc.setFont('helvetica', 'normal');
  doc.setFontSize(5.5);
  doc.setTextColor(90, 100, 115);

  const splitLopd = doc.splitTextToSize(lopdText, contentWidth);
  doc.text(splitLopd, xLeft, 260);

  return doc;
}

/**
 * Genera el documento y activa la descarga directa en el navegador.
 */
export function downloadOrderPdf(order, client, total, terminalConfig = {}) {
  const doc = buildOrderPdf(order, client, total, terminalConfig);
  const series = order?.series || order?.serie || terminalConfig?.series || 'VD';
  const num = order?.number || order?.numero_pedido || '';
  const code = client?.code || 'cliente';
  doc.save(`Pedido_${series}_${num}_${code}.pdf`);
}
