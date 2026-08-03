export type HelpAlertSeverity = 'info' | 'warn' | 'block';

export interface HelpAlert {
  code: string;
  severity: HelpAlertSeverity;
  message: string;
  rgId?: string;
}

export interface HelpAlertContext {
  customer?: {
    name?: string;
    status?: string;
    balance?: number;
    creditLimit?: number;
  } | null;
  /** Encours commandes ouvertes déjà chargé côté UI (approx). */
  openOrdersTtc?: number;
  documentTtc?: number;
  lines?: Array<{
    productKey?: string;
    quantity?: number;
    unitPrice?: number;
    vatRate?: number;
  }>;
  stockByProduct?: Record<string, number>;
  /** Remise max profil commercial (défaut 20%). */
  maxDiscountPercent?: number;
  lineDiscounts?: number[];
  expectedVatRate?: number;
  documentKind?: 'Quote' | 'Order' | 'Invoice';
}

/** Moteur N5 — alertes intelligentes (client-side, miroir des RG). */
export function evaluateHelpAlerts(ctx: HelpAlertContext, t: (key: string, p?: Record<string, string | number>) => string): HelpAlert[] {
  const alerts: HelpAlert[] = [];
  const customer = ctx.customer;
  if (!customer) return alerts;

  const status = (customer.status || 'Active').toLowerCase();
  if (status === 'blocked' || status === 'closed') {
    alerts.push({
      code: 'customer.blocked',
      severity: 'block',
      rgId: 'RG-CT2',
      message: t('help.alert.customerBlocked', { name: customer.name || '' })
    });
  }

  const limit = customer.creditLimit ?? 0;
  if (limit > 0 && (ctx.documentKind === 'Order' || ctx.documentKind === 'Quote')) {
    const open = ctx.openOrdersTtc ?? 0;
    const doc = ctx.documentTtc ?? 0;
    const balance = customer.balance ?? 0;
    const projected = balance + open + doc;
    if (projected > limit + 0.01) {
      const over = projected - limit;
      alerts.push({
        code: 'credit.limit',
        severity: 'block',
        rgId: 'RG-CC2',
        message: t('help.alert.creditLimit', {
          name: customer.name || '',
          limit: formatEur(limit),
          projected: formatEur(projected),
          over: formatEur(over)
        })
      });
    }
  }

  for (const line of ctx.lines || []) {
    if ((line.unitPrice ?? 0) === 0 && (line.quantity ?? 0) > 0) {
      alerts.push({
        code: 'price.zero',
        severity: 'warn',
        rgId: 'RG-PR0',
        message: t('help.alert.priceZero', { product: line.productKey || '—' })
      });
    }

    const stock = line.productKey && ctx.stockByProduct
      ? ctx.stockByProduct[line.productKey]
      : undefined;
    if (stock != null && (line.quantity ?? 0) > stock + 0.0001) {
      alerts.push({
        code: 'stock.insufficient',
        severity: 'warn',
        rgId: 'RG-BL4',
        message: t('help.alert.stockInsufficient', {
          product: line.productKey || '—',
          qty: line.quantity ?? 0,
          stock
        })
      });
    }

    const expected = ctx.expectedVatRate;
    if (expected != null && line.vatRate != null && Math.abs(line.vatRate - expected) > 0.01) {
      alerts.push({
        code: 'vat.mismatch',
        severity: 'warn',
        rgId: 'RG-FC7',
        message: t('help.alert.vatMismatch', {
          product: line.productKey || '—',
          rate: line.vatRate,
          expected
        })
      });
    }
  }

  const maxDisc = ctx.maxDiscountPercent ?? 20;
  for (const d of ctx.lineDiscounts || []) {
    if (d > maxDisc + 0.01) {
      alerts.push({
        code: 'discount.excessive',
        severity: 'block',
        rgId: 'RG-REM',
        message: t('help.alert.discountExcessive', { discount: d, max: maxDisc })
      });
    }
  }

  // Dedupe by code+message
  const seen = new Set<string>();
  return alerts.filter(a => {
    const k = a.code + a.message;
    if (seen.has(k)) return false;
    seen.add(k);
    return true;
  });
}

function formatEur(n: number): string {
  return n.toLocaleString('fr-BE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}
