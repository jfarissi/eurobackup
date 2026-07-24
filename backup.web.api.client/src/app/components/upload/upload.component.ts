import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DocumentService } from '../../services/document.service';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MaterialModule } from '../../material.module';
import { Document } from '../../models/document';
import { Router, RouterModule } from '@angular/router';
import { MatTableDataSource } from '@angular/material/table';
import { AppI18nService } from '../../services/app-i18n.service';
import { TPipe } from '../../pipes/t.pipe';

@Component({
  selector: 'app-upload',
  templateUrl: './upload.component.html',
  styleUrls: ['./upload.component.css'],
  standalone: true,
  imports: [CommonModule, FormsModule, MaterialModule, RouterModule, TPipe]
})
export class UploadComponent implements OnInit {
  file: File | null = null;
  typeDocument = 'Facture';
  numero = '';
  client = '';
  supplier = '';
  dateDocument = '';
  
  // Documents non associés du fournisseur
  unlinkedDocuments: Document[] = [];
  unlinkedDocumentsDataSource = new MatTableDataSource<Document>([]);
  displayedColumns: string[] = ['id', 'type', 'numero', 'client', 'supplier', 'date', 'actions'];
  suppliers: string[] = [];
  loading = false;
  isDragOver = false;
  recentDocuments: Document[] = [];
  totalDocuments = 0;
  metricsPercent = 0;

  constructor(
    private docs: DocumentService,
    private snack: MatSnackBar,
    private router: Router,
    private i18n: AppI18nService
  ) {}

  ngOnInit(): void {
    this.loadRecentDocuments();
  }

  loadRecentDocuments() {
    this.docs.list().subscribe({
      next: (docs) => {
        this.totalDocuments = docs.length;
        this.metricsPercent = Math.min(100, Math.round((docs.length / Math.max(docs.length, 50)) * 85));
        this.recentDocuments = [...docs]
          .sort((a, b) => new Date(b.dateAdded).getTime() - new Date(a.dateAdded).getTime())
          .slice(0, 5);
        const suppliersSet = new Set<string>();
        docs.forEach(d => { if (d.supplier) suppliersSet.add(d.supplier); });
        this.suppliers = Array.from(suppliersSet).sort();
      },
      error: () => {
        this.recentDocuments = [];
        this.totalDocuments = 0;
        this.metricsPercent = 0;
      }
    });
  }

  formatRelativeDate(dateStr: string): string {
    if (!dateStr) return '-';
    const diff = Date.now() - new Date(dateStr).getTime();
    const hours = Math.floor(diff / 3600000);
    if (hours < 1) return this.i18n.t('upload.relative.justNow');
    if (hours < 24) return this.i18n.t('upload.relative.hoursAgo', { hours });
    const days = Math.floor(hours / 24);
    return this.i18n.t('upload.relative.daysAgo', { days });
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragOver = false;
    const file = event.dataTransfer?.files?.[0];
    if (file) this.processFile(file);
  }

  onFileChange(event: any) {
    const file = event.target.files?.[0];
    if (file) this.processFile(file);
  }

  processFile(file: File) {
    this.file = file;
    this.loading = true;
    this.docs.inspect(this.file).subscribe({
      next: (r) => {
          if (r?.typeDocument) this.typeDocument = r.typeDocument;
          if (r?.numero) this.numero = r.numero;
          if (r?.client) this.client = r.client;
          if (r?.supplier) {
            // Ajouter le fournisseur à la liste s'il n'existe pas déjà
            if (!this.suppliers.includes(r.supplier)) {
              this.suppliers.push(r.supplier);
              this.suppliers.sort();
            }
            this.supplier = r.supplier;
            this.loadUnlinkedDocuments();
          }
          if (r?.dateDocument) {
            this.dateDocument = (r.dateDocument.length > 10 
              ? r.dateDocument.substring(0, 10) 
              : r.dateDocument);
          }
          this.loading = false;
        },
        error: _ => {
          this.loading = false;
        }
      });
  }

  loadSuppliers() {
    this.loadRecentDocuments();
  }

  onSupplierChange() {
    this.loadUnlinkedDocuments();
  }

  loadUnlinkedDocuments() {
    if (!this.supplier) {
      this.unlinkedDocuments = [];
      this.unlinkedDocumentsDataSource.data = [];
      return;
    }
    
    this.docs.list().subscribe(allDocs => {
      this.docs.relations().subscribe(relations => {
        const linkedIds = new Set<number>();
        relations.forEach(r => {
          linkedIds.add(r.invoiceId);
          linkedIds.add(r.deliveryId);
        });
        
        this.unlinkedDocuments = allDocs.filter(d => 
          d.supplier === this.supplier && 
          !linkedIds.has(d.id)
        );
        this.unlinkedDocumentsDataSource.data = this.unlinkedDocuments;
      });
    });
  }

  upload() {
    if (!this.file) {
      this.snack.open(this.i18n.t('upload.snack.selectFile'), this.i18n.t('common.ok'), { duration: 2000 });
      return;
    }
    
    this.loading = true;
    this.docs.upload(this.file, this.typeDocument, this.numero, this.client, this.dateDocument, this.supplier)
      .subscribe({
        next: (doc) => {
          this.loading = false;
          
          // Ajouter le fournisseur à la liste s'il n'existe pas déjà
          if (doc.supplier && !this.suppliers.includes(doc.supplier)) {
            this.suppliers.push(doc.supplier);
            this.suppliers.sort();
          }
          
          this.snack.open(this.i18n.t('upload.snack.success'), this.i18n.t('common.ok'), { duration: 2000 });
          this.loadRecentDocuments();
          
          // Si c'est un BL, rechercher les factures correspondantes
          if (doc.typeDocument === 'BonLivraison' && doc.numero) {
            this.searchMatchingInvoices(doc.numero, doc.id);
          } else {
            // Recharger les documents non associés
            this.loadUnlinkedDocuments();
            // Réinitialiser le formulaire
            this.resetForm();
          }
        },
        error: (err) => {
          this.loading = false;
          
          // Vérifier si c'est une erreur de doublon
          if (err.status === 409 || (err.error && err.error.isDuplicate)) {
            const errorMessage = err.error?.error || this.i18n.t('upload.snack.duplicate');
            this.snack.open(errorMessage, this.i18n.t('common.close'), { 
              duration: 5000,
              panelClass: ['error-snackbar']
            });
          } else {
            const errorMessage = err.error?.error || this.i18n.t('upload.snack.uploadError');
            this.snack.open(errorMessage, this.i18n.t('common.close'), { duration: 3000 });
          }
          console.error(err);
        }
      });
  }

  searchMatchingInvoices(blNumber: string, blId: number) {
    this.docs.findInvoicesByBlNumber(blNumber).subscribe({
      next: (invoices) => {
        // Filtrer par fournisseur si spécifié
        let filteredInvoices = invoices;
        if (this.supplier) {
          filteredInvoices = invoices.filter(inv => inv.supplier === this.supplier);
        }
        
        if (filteredInvoices.length === 1) {
          // Une seule facture trouvée, proposer l'association
          const invoice = filteredInvoices[0];
          if (confirm(this.i18n.t('upload.confirm.linkInvoice', { numero: invoice.numero || invoice.id }))) {
            this.docs.link(invoice.id, blId).subscribe({
              next: () => {
                this.snack.open(this.i18n.t('upload.snack.linkSuccess'), this.i18n.t('common.ok'), { duration: 2000 });
                this.loadUnlinkedDocuments();
                this.resetForm();
              },
              error: (err) => {
                this.snack.open(this.i18n.t('upload.snack.linkError'), this.i18n.t('common.close'), { duration: 3000 });
                console.error(err);
              }
            });
          } else {
            // Si l'utilisateur refuse, rediriger vers la page de comparaison avec toutes les factures du fournisseur
            this.router.navigate(['/compare'], { 
              queryParams: { 
                blId: blId, 
                blNumber: blNumber,
                supplier: this.supplier
              } 
            });
          }
        } else if (filteredInvoices.length > 1) {
          // Plusieurs factures trouvées, rediriger vers la page de comparaison
          this.snack.open(
            this.i18n.t('upload.snack.invoicesFound', { count: filteredInvoices.length }),
            this.i18n.t('common.ok'),
            { duration: 3000 }
          );
          this.router.navigate(['/compare'], { 
            queryParams: { 
              blId: blId, 
              blNumber: blNumber,
              supplier: this.supplier
            } 
          });
        } else {
          // Aucune facture trouvée avec ce numéro, afficher toutes les factures du fournisseur
          this.snack.open(this.i18n.t('upload.snack.noInvoice'), this.i18n.t('common.ok'), { duration: 3000 });
          this.router.navigate(['/compare'], { 
            queryParams: { 
              blId: blId, 
              blNumber: blNumber,
              supplier: this.supplier
            } 
          });
        }
      },
      error: (err) => {
        console.error(err);
        // En cas d'erreur, rediriger quand même vers la page de comparaison avec toutes les factures du fournisseur
        this.router.navigate(['/compare'], { 
          queryParams: { 
            blId: blId, 
            blNumber: blNumber,
            supplier: this.supplier
          } 
        });
      }
    });
  }

  resetForm() {
    this.file = null;
    this.numero = '';
    this.client = '';
    this.dateDocument = '';
    // Ne pas réinitialiser le supplier et typeDocument pour faciliter les uploads multiples
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    if (fileInput) {
      fileInput.value = '';
    }
  }

  navigateToCompare() {
    this.router.navigate(['/compare']);
  }

  download(id: number) {
    this.docs.download(id).subscribe(blob => {
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'document.pdf';
      a.click();
      window.URL.revokeObjectURL(url);
    });
  }
}

