import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MaterialModule } from '../../material.module';
import { PythonExtractorService, ParseResult, ParsedItem } from '../../services/python-extractor.service';
import { DocumentService } from '../../services/document.service';
import { Document } from '../../models/document';
import { MatTableDataSource } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';

@Component({
  selector: 'app-python-test',
  templateUrl: './python-test.component.html',
  styleUrls: ['./python-test.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule]
})
export class PythonTestComponent implements OnInit {
  /** Exposé au template pour les comparaisons d’écart */
  readonly Math = Math;
  file: File | null = null;
  loading = false;
  error: string | null = null;
  forceCatalog = false; // Checkbox pour forcer le parsing en tant que catalogue
  returnSql = false; // Checkbox pour retourner un script SQL
  sqlScript: string | null = null; // Script SQL généré
  
  // Résultats du parsing
  parseResult: ParseResult | null = null;
  itemsDataSource = new MatTableDataSource<ParsedItem>([]);
  displayedColumns: string[] = [
    'sku',
    'ean',
    'description',
    'qty',
    'unit',
    'unit_price',
    'line_calc',
    'line_total'
  ];
  
  // Métadonnées
  metadata: any = null;
  
  // Statistiques
  stats = {
    totalItems: 0,
    itemsWithSku: 0,
    itemsWithEan: 0,
    /** Somme regelbedrag (uniquement line_total parsé) */
    totalRegelbedrag: 0,
    /** Somme affichée (regelbedrag ou repli qté×PU si line_total absent) */
    totalValue: 0,
    /** Somme si on faisait uniquement qté × prix unitaire */
    totalFromQtyUnit: 0,
    /** Lignes où regelbedrag ≠ qté×PU (≥ 0,01 €) */
    amountMismatchCount: 0
  };

  /** Résumé HT facture vs somme lignes */
  amountSummary: {
    footerHt: number | null;
    linesSumUi: number;
    linesSumParser: number | null;
    discrepancy: number | null;
    /** Montant HT de référence (aligné EuroBrico : subtotaal si écart modéré) */
    valeurTotale: number;
    valeurTotaleLabel: string;
  } = {
    footerHt: null,
    linesSumUi: 0,
    linesSumParser: null,
    discrepancy: null,
    valeurTotale: 0,
    valeurTotaleLabel: ''
  };

  /** Paramètres optionnels pour /parse avec Ollama (laisser vide = défauts du service Python) */
  ollamaHost = '';
  ollamaProfile = '';
  ollamaModel = '';
  /** Modèles renvoyés par GET /ollama/models */
  ollamaModels: string[] = [];
  loadingOllamaModels = false;

  // Documents pour reparse IA
  documents: Document[] = [];
  documentsDataSource = new MatTableDataSource<Document>([]);
  documentsDisplayedColumns: string[] = ['id', 'type', 'numero', 'supplier', 'actions'];
  loadingDocuments = false;

  constructor(
    private pythonService: PythonExtractorService,
    private documentService: DocumentService,
    private snack: MatSnackBar
  ) {}

  ngOnInit() {
    this.loadDocuments();
    // Ollama : chargement à la demande (bouton Rafraîchir), pas au démarrage.
  }

  /**
   * Charge la liste des modèles depuis Ollama (utilise l’hôte saisi ou le défaut du serveur Python).
   * @param showSuccessSnack si true (ex. bouton Rafraîchir), affiche un message quand la liste est non vide
   */
  loadOllamaModels(showSuccessSnack = false) {
    this.loadingOllamaModels = true;
    const hostArg = this.ollamaHost.trim() || undefined;
    this.pythonService.listOllamaModels(hostArg).subscribe({
      next: (res) => {
        this.loadingOllamaModels = false;
        this.ollamaModels = res.models ?? [];
        if (res.error) {
          this.snack.open(res.error, 'Fermer', { duration: 5000 });
        } else if (showSuccessSnack && this.ollamaModels.length) {
          this.snack.open(`${this.ollamaModels.length} modèle(s) Ollama chargé(s)`, 'OK', { duration: 2000 });
        }
      },
      error: (err) => {
        this.loadingOllamaModels = false;
        this.ollamaModels = [];
        const msg = err?.error?.error || err?.message || 'Impossible de charger les modèles';
        this.snack.open(String(msg), 'Fermer', { duration: 4000 });
      }
    });
  }

  loadDocuments() {
    this.loadingDocuments = true;
    this.documentService.list().subscribe({
      next: (docs) => {
        this.documents = docs;
        this.documentsDataSource.data = docs;
        this.loadingDocuments = false;
      },
      error: (err) => {
        console.error('Erreur chargement documents:', err);
        this.loadingDocuments = false;
      }
    });
  }

  onReparseWithAi(documentId: number) {
    if (!documentId) return;
    this.loading = true;
    this.documentService.reparseLines(documentId, true).subscribe({
      next: (r) => {
        this.loading = false;
        if (r.success) {
          this.snack.open('Document re-parsé avec IA avec succès', 'OK', { duration: 2500 });
          this.loadDocuments();
        } else {
          this.snack.open('Erreur lors du re-parsing IA', 'Fermer', { duration: 3000 });
        }
      },
      error: (err) => {
        this.loading = false;
        console.error('Erreur reparse IA:', err);
        this.snack.open('Erreur reparse IA', 'Fermer', { duration: 3000 });
      }
    });
  }

  onFileChange(event: any) {
    this.file = event.target.files[0];
    this.error = null;
    this.parseResult = null;
    this.metadata = null;
    this.itemsDataSource.data = [];
    this.sqlScript = null;
    this.resetAmountSummary();
    this.stats = {
      totalItems: 0,
      itemsWithSku: 0,
      itemsWithEan: 0,
      totalRegelbedrag: 0,
      totalValue: 0,
      totalFromQtyUnit: 0,
      amountMismatchCount: 0
    };
  }

  /**
   * Test du nouvel endpoint /parse (structure auto_invoice_parser)
   */
  testParse() {
    if (!this.file) {
      this.error = 'Veuillez sélectionner un fichier PDF';
      return;
    }

    this.loading = true;
    this.error = null;

    this.pythonService.parsePdf(this.file, false, 'openai', this.forceCatalog, this.returnSql).subscribe({
      next: (result) => {
        this.loading = false;
        
        // Si returnSql=true, le résultat peut être du texte SQL
        if (this.returnSql && typeof result === 'string') {
          this.sqlScript = result;
          this.parseResult = null;
          this.metadata = null;
          this.itemsDataSource.data = [];
          return;
        }
        
        // Type guard: si ce n'est pas une string, c'est un ParseResult
        if (typeof result === 'string') {
          // Ne devrait pas arriver ici si returnSql=false, mais au cas où
          this.error = 'Erreur: résultat inattendu (string)';
          return;
        }
        
        this.applyParseResult(result as ParseResult);
        this.sqlScript = null;
      },
      error: (err) => {
        this.loading = false;
        this.error = this.formatHttpError(err, 'Erreur lors du parsing');
        console.error('Erreur parsing:', err);
        if (err?.error) {
          console.error('Corps erreur:', err.error);
        }
      }
    });
  }

  /**
   * Test du parsing avec IA (endpoint /parse avec use_ai=true)
   * @param provider Fournisseur IA: 'openai', 'gemini' ou 'ollama'
   */
  testParseWithAi(provider: 'openai' | 'gemini' | 'ollama' = 'openai') {
    if (!this.file) {
      this.error = 'Veuillez sélectionner un fichier PDF';
      return;
    }

    this.loading = true;
    this.error = null;

    const ollamaOptions =
      provider === 'ollama'
        ? {
            host: this.ollamaHost.trim() || undefined,
            profile: this.ollamaProfile.trim() || undefined,
            model: this.ollamaModel.trim() || undefined
          }
        : undefined;

    this.pythonService
      .parsePdf(this.file, true, provider, this.forceCatalog, this.returnSql, ollamaOptions)
      .subscribe({
      next: (result) => {
        this.loading = false;
        
        // Si returnSql=true, le résultat peut être du texte SQL
        if (this.returnSql && typeof result === 'string') {
          this.sqlScript = result;
          this.parseResult = null;
          this.metadata = null;
          this.itemsDataSource.data = [];
          const providerName = this.aiProviderLabel(provider);
          this.snack.open(`Script SQL généré avec ${providerName}`, 'OK', { duration: 2500 });
          return;
        }
        
        // Type guard: si ce n'est pas une string, c'est un ParseResult
        if (typeof result === 'string') {
          // Ne devrait pas arriver ici si returnSql=false, mais au cas où
          this.error = 'Erreur: résultat inattendu (string)';
          return;
        }
        
        // Maintenant TypeScript sait que result est ParseResult
        this.applyParseResult(result as ParseResult);
        this.sqlScript = null;
        const providerName = this.aiProviderLabel(provider);
        this.snack.open(`Parsing avec ${providerName} réussi`, 'OK', { duration: 2500 });
      },
      error: (err) => {
        this.loading = false;
        this.error = `Erreur: ${err.message || 'Erreur lors du parsing avec IA'}`;
        console.error('Erreur parsing IA:', err);
        const providerName = this.aiProviderLabel(provider);
        this.snack.open(`Erreur lors du parsing avec ${providerName}`, 'Fermer', { duration: 3000 });
      }
    });
  }

  private aiProviderLabel(provider: 'openai' | 'gemini' | 'ollama'): string {
    switch (provider) {
      case 'openai':
        return 'OpenAI';
      case 'gemini':
        return 'Gemini';
      case 'ollama':
        return 'Ollama';
    }
  }

  /**
   * Test de l'endpoint /extract (existant)
   */
  testExtract() {
    if (!this.file) {
      this.error = 'Veuillez sélectionner un fichier PDF';
      return;
    }

    this.loading = true;
    this.error = null;

    this.pythonService.extractProducts(this.file, true).subscribe({
      next: (items) => {
        this.loading = false;
        const normalized = this.normalizeItems(items || []);
        this.itemsDataSource.data = normalized;
        this.calculateStats(normalized);
        this.resetAmountSummary();
      },
      error: (err) => {
        this.loading = false;
        this.error = `Erreur: ${err.message || 'Erreur lors de l\'extraction'}`;
        console.error('Erreur extraction:', err);
      }
    });
  }

  /**
   * Test de l'endpoint /inspect (existant)
   */
  testInspect() {
    if (!this.file) {
      this.error = 'Veuillez sélectionner un fichier PDF';
      return;
    }

    this.loading = true;
    this.error = null;

    this.pythonService.inspectMetadata(this.file, true).subscribe({
      next: (metadata) => {
        this.loading = false;
        this.metadata = metadata;
      },
      error: (err) => {
        this.loading = false;
        this.error = `Erreur: ${err.message || 'Erreur lors de l\'inspection'}`;
        console.error('Erreur inspection:', err);
      }
    });
  }

  /**
   * Health check du service Python
   */
  testHealth() {
    this.loading = true;
    this.error = null;
    
    console.log('🔍 Test de connexion au service Python...');
    console.log('URL:', 'http://localhost:8000/health');

    this.pythonService.healthCheck().subscribe({
      next: (health) => {
        this.loading = false;
        console.log('✅ Réponse reçue:', health);
        alert(`✅ Service Python accessible!\nStatus: ${health.status}\nService: ${health.service}`);
      },
      error: (err) => {
        this.loading = false;
        console.error('❌ Erreur complète:', err);
        
        // Détails de l'erreur pour debug
        let errorMsg = 'Service Python non disponible\n\n';
        
        if (err.status === 0) {
          errorMsg += '❌ Erreur de connexion réseau (status 0)\n\n';
          errorMsg += 'Solutions:\n';
          errorMsg += '1. Vérifiez que le service Python est démarré\n';
          errorMsg += '2. Utilisez: uvicorn app.main:app --host 0.0.0.0 --port 8000\n';
          errorMsg += '3. Testez dans le navigateur: http://localhost:8000/health\n';
          errorMsg += '4. Vérifiez le firewall Windows';
        } else {
          errorMsg += `Status: ${err.status || 'N/A'}\n`;
          errorMsg += `StatusText: ${err.statusText || 'N/A'}\n`;
          errorMsg += `Message: ${err.message || 'N/A'}\n`;
        }
        
        this.error = errorMsg;
        
        console.error('Détails de l\'erreur:', {
          error: err,
          message: err.message,
          status: err.status,
          statusText: err.statusText,
          url: err.url,
          errorObject: err.error,
          name: err.name
        });
      }
    });
  }

  private applyParseResult(parseResult: ParseResult) {
    this.parseResult = parseResult;
    this.metadata = parseResult.metadata;
    const normalized = this.normalizeItems(parseResult.items || []);
    this.itemsDataSource.data = normalized;
    this.calculateStats(normalized);
    this.updateAmountSummary();
  }

  /** Regelbedrag arrondi 2 déc.; sinon repli qté×PU (peut différer du PDF). */
  lineAmount(item: ParsedItem): number {
    const qty = item.qty ?? 0;
    const unit = item.unit_price ?? 0;
    if (item.line_total != null && item.line_total > 0) {
      return this.roundMoney(item.line_total);
    }
    if (qty > 0 && unit > 0) {
      return this.roundMoney(qty * unit);
    }
    return 0;
  }

  qtyTimesUnit(item: ParsedItem): number | null {
    const qty = item.qty ?? 0;
    const unit = item.unit_price ?? 0;
    if (qty <= 0 || unit <= 0) return null;
    return this.roundMoney(qty * unit);
  }

  hasAmountMismatch(item: ParsedItem): boolean {
    if (item.line_total == null || item.line_total <= 0) return false;
    const calc = this.qtyTimesUnit(item);
    if (calc == null) return false;
    return Math.abs(this.roundMoney(item.line_total) - calc) >= 0.01;
  }

  private normalizeItems(items: ParsedItem[]): ParsedItem[] {
    return items.map((item) => {
      const qty = item.qty ?? 0;
      const unit = item.unit_price ?? 0;
      let lineTotal = item.line_total;
      if (lineTotal != null && lineTotal > 0) {
        lineTotal = this.roundMoney(lineTotal);
      } else if (qty > 0 && unit > 0) {
        lineTotal = this.roundMoney(qty * unit);
      }
      return { ...item, line_total: lineTotal ?? item.line_total };
    });
  }

  private roundMoney(value: number): number {
    return Math.round(value * 100) / 100;
  }

  private calculateStats(items: ParsedItem[]) {
    this.stats.totalItems = items.length;
    this.stats.itemsWithSku = items.filter((i) => i.sku).length;
    this.stats.itemsWithEan = items.filter((i) => i.ean || i.barcode_raw).length;
    let linesSum = 0;
    let regelSum = 0;
    let qtyUnitSum = 0;
    let mismatchCount = 0;
    for (const item of items) {
      linesSum += this.lineAmount(item);
      if (item.line_total != null && item.line_total > 0) {
        regelSum += this.roundMoney(item.line_total);
      }
      const calc = this.qtyTimesUnit(item);
      if (calc != null) qtyUnitSum += calc;
      if (this.hasAmountMismatch(item)) mismatchCount++;
    }
    this.stats.totalRegelbedrag = this.roundMoney(regelSum);
    this.stats.totalValue = this.roundMoney(linesSum);
    this.stats.totalFromQtyUnit = this.roundMoney(qtyUnitSum);
    this.stats.amountMismatchCount = mismatchCount;
  }

  private resetAmountSummary() {
    this.amountSummary = {
      footerHt: null,
      linesSumUi: 0,
      linesSumParser: null,
      discrepancy: null,
      valeurTotale: 0,
      valeurTotaleLabel: ''
    };
  }

  /**
   * HT de référence : somme regelbedrag ; si subtotaal facture un peu plus élevé (≤ 100 €), on prend le pied de page.
   */
  private updateAmountSummary() {
    const footer = this.metadata?.invoice_total_excl_vat;
    const linesMeta = this.metadata?.lines_total_excl_vat;
    const discMeta = this.metadata?.total_discrepancy;
    const footerHt =
      footer != null && footer > 0 ? this.roundMoney(footer) : null;
    const linesSumParser =
      linesMeta != null && linesMeta > 0 ? this.roundMoney(linesMeta) : null;
    const linesSumUi =
      this.stats.totalRegelbedrag > 0
        ? this.stats.totalRegelbedrag
        : this.stats.totalValue;
    const linesSum = linesSumParser ?? linesSumUi;

    let discrepancy: number | null = null;
    if (discMeta != null && !Number.isNaN(discMeta)) {
      discrepancy = this.roundMoney(discMeta);
    } else if (footerHt != null) {
      discrepancy = this.roundMoney(footerHt - linesSum);
    }

    let valeurTotale = linesSum;
    let valeurTotaleLabel = 'Somme des regelbedrag (lignes)';
    if (
      footerHt != null &&
      footerHt > linesSum &&
      footerHt - linesSum <= 100
    ) {
      valeurTotale = footerHt;
      valeurTotaleLabel = 'Subtotaal facture HT (pied de page — référence)';
    } else if (footerHt != null && linesSum <= 0) {
      valeurTotale = footerHt;
      valeurTotaleLabel = 'Subtotaal facture HT';
    }

    this.amountSummary = {
      footerHt,
      linesSumUi,
      linesSumParser,
      discrepancy,
      valeurTotale,
      valeurTotaleLabel
    };
  }

  /** Montants ligne (regelbedrag) — 2 décimales. */
  formatCurrency(value: number | null | undefined): string {
    return this.formatMoney(value, 2);
  }

  /** Prix unitaire net (Pardaen 0,742 / 0,0675…) — 2 à 4 décimales selon la précision réelle. */
  formatUnitPrice(value: number | null | undefined): string {
    if (value == null) return '-';
    return this.formatMoney(value, this.unitPriceFractionDigits(value));
  }

  private unitPriceFractionDigits(value: number): number {
    const round = (decimals: number) =>
      Math.round(value * Math.pow(10, decimals)) / Math.pow(10, decimals);
    if (Math.abs(value - round(2)) < 1e-8) return 2;
    if (Math.abs(value - round(3)) < 1e-8) return 3;
    return 4;
  }

  private formatMoney(value: number | null | undefined, fractionDigits: number): string {
    if (value == null) return '-';
    return new Intl.NumberFormat('fr-BE', {
      style: 'currency',
      currency: 'EUR',
      minimumFractionDigits: fractionDigits,
      maximumFractionDigits: fractionDigits
    }).format(value);
  }

  /**
   * Télécharge le script SQL généré
   */
  downloadSqlScript() {
    if (!this.sqlScript) return;
    
    const blob = new Blob([this.sqlScript], { type: 'text/plain;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `catalog_insert_${new Date().getTime()}.sql`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
    
    this.snack.open('Script SQL téléchargé', 'OK', { duration: 2000 });
  }

  /**
   * Copie le script SQL dans le presse-papier
   */
  copySqlScript() {
    if (!this.sqlScript) return;
    
    navigator.clipboard.writeText(this.sqlScript).then(() => {
      this.snack.open('Script SQL copié dans le presse-papier', 'OK', { duration: 2000 });
    }).catch(err => {
      console.error('Erreur lors de la copie:', err);
      this.snack.open('Erreur lors de la copie', 'Fermer', { duration: 3000 });
    });
  }

  private formatHttpError(err: any, fallback: string): string {
    if (err?.status === 0 || String(err?.message || '').includes('Unknown Error')) {
      return (
        'Impossible de joindre le backend (https://127.0.0.1:7157). ' +
        'Lancez dotnet run --launch-profile https (Backup.Web.Api.Server) et Python sur le port 8000.'
      );
    }

    const body = err?.error;
    if (typeof body === 'string' && body.trim()) {
      return body.length > 400 ? body.slice(0, 400) + '…' : body;
    }
    if (body?.detail) {
      if (typeof body.detail === 'string') {
        return body.detail;
      }
      if (Array.isArray(body.detail) && body.detail[0]?.msg) {
        return body.detail[0].msg;
      }
    }
    if (body?.error) {
      const extra = body.detail ? ` — ${body.detail}` : '';
      return `${body.error}${extra}`;
    }

    return `Erreur (${err?.status ?? '?'}): ${err?.message || fallback}`;
  }
}

