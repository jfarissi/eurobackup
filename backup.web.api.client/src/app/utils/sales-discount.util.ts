/** Miroir client de SalesBusinessRules (RG-RM1, RG-CP3, RG-FA1, RG-RE1, RG-RE5). */

const SHIPPING_FEE_KEYS = new Set(['FDP', 'SHIPPING']);

export function isShippingFeeKey(productKey?: string | null): boolean {
  return !!productKey && SHIPPING_FEE_KEYS.has(productKey.trim().toUpperCase());
}

export function capDiscountPercent(discountPercent: number): number {
  if (discountPercent < 0) return 0;
  if (discountPercent > 100) return 100;
  return discountPercent;
}

export function capNonNegativeAmount(amount: number): number {
  return amount < 0 ? 0 : amount;
}

export function calcLineTotals(
  quantity: number,
  unitPrice: number,
  discountPercent: number,
  vatRate: number
): { totalHT: number; totalTTC: number } {
  const cap = capDiscountPercent(discountPercent || 0);
  const totalHT = +(quantity * unitPrice * (1 - cap / 100)).toFixed(2);
  const totalTTC = +(totalHT * (1 + (vatRate || 0) / 100)).toFixed(2);
  return { totalHT, totalTTC };
}

export function calcDocumentTotals(
  lines: Array<{ productKey?: string; totalHT?: number; totalTTC?: number }>,
  headerDiscountPercent = 0,
  shippingAmountHt = 0,
  shippingVatRate = 21
): { ht: number; vat: number; ttc: number } {
  let merchHt = 0;
  let merchVat = 0;
  let shipLineHt = 0;
  let shipLineVat = 0;

  for (const l of lines) {
    const lineHt = l.totalHT || 0;
    const lineVat = (l.totalTTC || 0) - lineHt;
    if (isShippingFeeKey(l.productKey)) {
      shipLineHt += lineHt;
      shipLineVat += lineVat;
    } else {
      merchHt += lineHt;
      merchVat += lineVat;
    }
  }

  if (headerDiscountPercent > 0) {
    const factor = 1 - capDiscountPercent(headerDiscountPercent) / 100;
    merchHt = +(merchHt * factor).toFixed(2);
    merchVat = +(merchVat * factor).toFixed(2);
  }

  const shipHeaderHt = capNonNegativeAmount(shippingAmountHt || 0);
  const shipHeaderVat = +(shipHeaderHt * (capNonNegativeAmount(shippingVatRate || 0) / 100)).toFixed(2);

  const ht = +(merchHt + shipLineHt + shipHeaderHt).toFixed(2);
  const vat = +(merchVat + shipLineVat + shipHeaderVat).toFixed(2);
  return { ht, vat, ttc: +(ht + vat).toFixed(2) };
}
