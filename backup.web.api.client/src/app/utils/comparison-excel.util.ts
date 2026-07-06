import * as XLSX from 'xlsx';
import {
  ComparisonLine,
  ComparisonResult,
  ErpPriceDiffLine,
  InvoicePriceComparisonResult,
} from '../models/comparison';

function timestamp(): string {
  const d = new Date();
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}_${pad(d.getHours())}${pad(d.getMinutes())}`;
}

function downloadWorkbook(workbook: XLSX.WorkBook, filename: string): void {
  XLSX.writeFile(workbook, filename);
}

function rowsFromComparaison(lines: ComparisonLine[]) {
  return lines.map(l => ({
    Code: l.productKey ?? '',
    Produit: l.product ?? '',
    Unité: l.unit ?? '',
    'Qté Facture': l.invoiceQty ?? 0,
    'Qté BL': l.deliveryQty ?? 0,
    'Qté Réelle': l.actualQuantity ?? '',
    'Différence Qté': l.diff ?? 0,
    'Prix unit. facture actuelle': l.currentInvoiceUnitPrice ?? 0,
    'Total facture': l.invoiceTotalValue ?? '',
    'Prix unit. facture précédente': l.previousInvoiceUnitPrice ?? 0,
    'Différence prix': l.priceDiff ?? 0,
    Statut: l.status ?? '',
    Validé: l.isValidated ? 'Oui' : 'Non',
    'Stock mis à jour': l.stockUpdated ? 'Oui' : 'Non',
  }));
}

function rowsFromInvoicePrice(result: InvoicePriceComparisonResult) {
  return result.lines.map(l => ({
    Produit: l.product ?? '',
    'Prix unit. facture 1': l.invoice1UnitPrice ?? 0,
    'Prix unit. facture 2': l.invoice2UnitPrice ?? 0,
    'Différence prix': l.priceDiff ?? 0,
  }));
}

function rowsFromErp(lines: ErpPriceDiffLine[]) {
  return lines.map(l => ({
    'Code produit': l.productCode ?? '',
    EAN: l.ean ?? '',
    Désignation: l.product ?? '',
    'Prix facture': l.invoiceUnitPrice ?? 0,
    'Prix ERP': l.erpUnitPrice ?? '',
    Delta: l.delta ?? '',
    Statut: l.status ?? '',
  }));
}

function metaSheet(title: string, entries: Record<string, string | number>) {
  return [
    { Champ: 'Type', Valeur: title },
    ...Object.entries(entries).map(([Champ, Valeur]) => ({ Champ, Valeur })),
    { Champ: 'Exporté le', Valeur: new Date().toLocaleString('fr-BE') },
  ];
}

export function exportComparaisonToExcel(result: ComparisonResult): void {
  const wb = XLSX.utils.book_new();
  const meta = metaSheet('Comparaison Facture / BL', {
    'ID facture': result.invoiceId,
    'ID BL': result.deliveryId,
    'Lignes': result.lines.length,
  });
  XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(meta), 'Infos');
  XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(rowsFromComparaison(result.lines)), 'Comparaison');
  downloadWorkbook(wb, `comparaison_facture${result.invoiceId}_bl${result.deliveryId}_${timestamp()}.xlsx`);
}

export function exportInvoicePriceToExcel(result: InvoicePriceComparisonResult): void {
  const wb = XLSX.utils.book_new();
  const meta = metaSheet('Comparaison prix entre factures', {
    'Facture 1': result.invoice1Number ?? result.invoice1Id,
    'Fournisseur 1': result.invoice1Supplier ?? '',
    'Facture 2': result.invoice2Number ?? result.invoice2Id,
    'Fournisseur 2': result.invoice2Supplier ?? '',
    'Lignes': result.lines.length,
  });
  XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(meta), 'Infos');
  XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(rowsFromInvoicePrice(result)), 'Prix factures');
  downloadWorkbook(
    wb,
    `comparaison_prix_${result.invoice1Id}_vs_${result.invoice2Id}_${timestamp()}.xlsx`
  );
}

export function exportErpPriceToExcel(
  lines: ErpPriceDiffLine[],
  invoiceLabel: string
): void {
  const wb = XLSX.utils.book_new();
  const meta = metaSheet('Comparaison prix ERP', {
    Facture: invoiceLabel,
    Lignes: lines.length,
  });
  XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(meta), 'Infos');
  XLSX.utils.book_append_sheet(wb, XLSX.utils.json_to_sheet(rowsFromErp(lines)), 'Prix ERP');
  downloadWorkbook(wb, `comparaison_erp_${invoiceLabel.replace(/[^\w.-]+/g, '_')}_${timestamp()}.xlsx`);
}

export function exportAllComparisonsToExcel(options: {
  comparaison?: ComparisonResult | null;
  invoicePrice?: InvoicePriceComparisonResult | null;
  erpLines?: ErpPriceDiffLine[] | null;
  erpInvoiceLabel?: string;
}): void {
  const wb = XLSX.utils.book_new();
  let sheetCount = 0;

  if (options.comparaison?.lines?.length) {
    XLSX.utils.book_append_sheet(
      wb,
      XLSX.utils.json_to_sheet(rowsFromComparaison(options.comparaison.lines)),
      'Facture-BL'
    );
    sheetCount++;
  }
  if (options.invoicePrice?.lines?.length) {
    XLSX.utils.book_append_sheet(
      wb,
      XLSX.utils.json_to_sheet(rowsFromInvoicePrice(options.invoicePrice)),
      'Prix factures'
    );
    sheetCount++;
  }
  if (options.erpLines?.length) {
    XLSX.utils.book_append_sheet(
      wb,
      XLSX.utils.json_to_sheet(rowsFromErp(options.erpLines)),
      'Prix ERP'
    );
    sheetCount++;
  }

  if (sheetCount === 0) {
    return;
  }

  downloadWorkbook(wb, `comparaisons_${timestamp()}.xlsx`);
}
