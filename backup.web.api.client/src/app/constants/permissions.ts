/** Mirrors Backup.Web.Api.Server.Models.Security.Permissions */
export const Permissions = {
  ProductRead: 'Product.Read',
  ProductCreate: 'Product.Create',
  ProductUpdate: 'Product.Update',
  ProductDelete: 'Product.Delete',
  BrandRead: 'Brand.Read',
  BrandCreate: 'Brand.Create',
  BrandUpdate: 'Brand.Update',
  BrandDelete: 'Brand.Delete',
  CategoryRead: 'Category.Read',
  CategoryCreate: 'Category.Create',
  CategoryUpdate: 'Category.Update',
  CategoryDelete: 'Category.Delete',
  ErpChangeRead: 'ErpChange.Read',
  ErpChangeUpdate: 'ErpChange.Update',
  ErpChangeDelete: 'ErpChange.Delete',
  SupplierRead: 'Supplier.Read',
  SupplierCreate: 'Supplier.Create',
  SupplierUpdate: 'Supplier.Update',
  SupplierDelete: 'Supplier.Delete',
  CustomerRead: 'Customer.Read',
  CustomerCreate: 'Customer.Create',
  CustomerUpdate: 'Customer.Update',
  CustomerDelete: 'Customer.Delete',
  QuoteRead: 'Quote.Read',
  QuoteCreate: 'Quote.Create',
  QuoteUpdate: 'Quote.Update',
  QuoteDelete: 'Quote.Delete',
  OrderRead: 'Order.Read',
  OrderCreate: 'Order.Create',
  OrderUpdate: 'Order.Update',
  OrderDelete: 'Order.Delete',
  DeliveryNoteRead: 'DeliveryNote.Read',
  DeliveryNoteCreate: 'DeliveryNote.Create',
  DeliveryNoteDelete: 'DeliveryNote.Delete',
  InvoiceRead: 'Invoice.Read',
  InvoiceCreate: 'Invoice.Create',
  InvoiceUpdate: 'Invoice.Update',
  InvoiceDelete: 'Invoice.Delete',
  PurchaseOrderRead: 'PurchaseOrder.Read',
  PurchaseOrderCreate: 'PurchaseOrder.Create',
  PurchaseOrderUpdate: 'PurchaseOrder.Update',
  PurchaseOrderDelete: 'PurchaseOrder.Delete',
  ReceiptRead: 'Receipt.Read',
  ReceiptCreate: 'Receipt.Create',
  ReceiptDelete: 'Receipt.Delete',
  SupplierInvoiceRead: 'SupplierInvoice.Read',
  SupplierInvoiceCreate: 'SupplierInvoice.Create',
  SupplierInvoiceDelete: 'SupplierInvoice.Delete',
  SalesReturnRead: 'SalesReturn.Read',
  SalesReturnCreate: 'SalesReturn.Create',
  SalesReturnUpdate: 'SalesReturn.Update',
  SupplierCreditNoteRead: 'SupplierCreditNote.Read',
  SupplierCreditNoteCreate: 'SupplierCreditNote.Create',
  SupplierCreditNoteUpdate: 'SupplierCreditNote.Update',
  StockRead: 'Stock.Read',
  StockUpdate: 'Stock.Update',
  CashRead: 'Cash.Read',
  CashManage: 'Cash.Manage',
  AccountingRead: 'Accounting.Read',
  AccountingCreate: 'Accounting.Create',
  NumberingManage: 'Numbering.Manage',
  HelpManage: 'Help.Manage',
  DocumentRead: 'Document.Read',
  DocumentUpload: 'Document.Upload',
  DocumentLink: 'Document.Link',
  UserRead: 'User.Read',
  UserCreate: 'User.Create',
  UserUpdate: 'User.Update',
  UserDelete: 'User.Delete',
  RoleRead: 'Role.Read',
  RoleCreate: 'Role.Create',
  RoleUpdate: 'Role.Update',
  RoleDelete: 'Role.Delete',
  EmailRead: 'Email.Read',
  EmailSend: 'Email.Send',
  EmailSettingsManage: 'Email.Settings',
} as const;

export type PermissionCode = typeof Permissions[keyof typeof Permissions];

/** Route → any of these permissions grants menu/route access */
export const RoutePermissions: Record<string, PermissionCode[]> = {
  '/dashboard': [],
  '/sales': [Permissions.CustomerRead, Permissions.QuoteRead, Permissions.OrderRead, Permissions.InvoiceRead, Permissions.DeliveryNoteRead],
  '/purchases': [Permissions.SupplierRead, Permissions.PurchaseOrderRead, Permissions.ReceiptRead, Permissions.SupplierInvoiceRead],
  '/stock': [Permissions.StockRead],
  '/erp-products': [Permissions.ProductRead],
  '/erp-brands': [Permissions.BrandRead, Permissions.ProductRead],
  '/erp-categories': [Permissions.CategoryRead, Permissions.ProductRead],
  '/erp-changes': [Permissions.ErpChangeRead],
  '/cash': [Permissions.CashRead, Permissions.CashManage],
  '/accounting': [Permissions.AccountingRead, Permissions.AccountingCreate],
  '/numbering': [Permissions.NumberingManage],
  '/upload': [Permissions.DocumentUpload],
  '/recherche': [Permissions.DocumentRead],
  '/compare': [Permissions.DocumentLink],
  '/admin': [Permissions.UserRead, Permissions.RoleRead, Permissions.EmailSettingsManage],
};

/** Sous-groupe de permissions dans une catégorie métier. */
export interface PermissionSection {
  label: string;
  permissions: PermissionCode[];
}

/** Catégorie métier pour l'écran Admin → Rôles. */
export interface PermissionCategory {
  id: string;
  label: string;
  sections: PermissionSection[];
}

/** Regroupement Ventes / Achats / Stock / … pour l'éditeur de permissions. */
export const PermissionCategories: PermissionCategory[] = [
  {
    id: 'sales',
    label: 'admin.perm.cat.sales',
    sections: [
      { label: 'admin.perm.sec.customers', permissions: [Permissions.CustomerRead, Permissions.CustomerCreate, Permissions.CustomerUpdate, Permissions.CustomerDelete] },
      { label: 'admin.perm.sec.quotes', permissions: [Permissions.QuoteRead, Permissions.QuoteCreate, Permissions.QuoteUpdate, Permissions.QuoteDelete] },
      { label: 'admin.perm.sec.orders', permissions: [Permissions.OrderRead, Permissions.OrderCreate, Permissions.OrderUpdate, Permissions.OrderDelete] },
      { label: 'admin.perm.sec.deliveryNotes', permissions: [Permissions.DeliveryNoteRead, Permissions.DeliveryNoteCreate, Permissions.DeliveryNoteDelete] },
      { label: 'admin.perm.sec.invoices', permissions: [Permissions.InvoiceRead, Permissions.InvoiceCreate, Permissions.InvoiceUpdate, Permissions.InvoiceDelete] },
      { label: 'admin.perm.sec.salesReturns', permissions: [Permissions.SalesReturnRead, Permissions.SalesReturnCreate, Permissions.SalesReturnUpdate] },
    ],
  },
  {
    id: 'purchases',
    label: 'admin.perm.cat.purchases',
    sections: [
      { label: 'admin.perm.sec.suppliers', permissions: [Permissions.SupplierRead, Permissions.SupplierCreate, Permissions.SupplierUpdate, Permissions.SupplierDelete] },
      { label: 'admin.perm.sec.purchaseOrders', permissions: [Permissions.PurchaseOrderRead, Permissions.PurchaseOrderCreate, Permissions.PurchaseOrderUpdate, Permissions.PurchaseOrderDelete] },
      { label: 'admin.perm.sec.receipts', permissions: [Permissions.ReceiptRead, Permissions.ReceiptCreate, Permissions.ReceiptDelete] },
      { label: 'admin.perm.sec.supplierInvoices', permissions: [Permissions.SupplierInvoiceRead, Permissions.SupplierInvoiceCreate, Permissions.SupplierInvoiceDelete] },
      { label: 'admin.perm.sec.supplierCreditNotes', permissions: [Permissions.SupplierCreditNoteRead, Permissions.SupplierCreditNoteCreate, Permissions.SupplierCreditNoteUpdate] },
    ],
  },
  {
    id: 'stock',
    label: 'admin.perm.cat.stock',
    sections: [
      { label: 'admin.perm.sec.stock', permissions: [Permissions.StockRead, Permissions.StockUpdate] },
    ],
  },
  {
    id: 'erp',
    label: 'admin.perm.cat.erp',
    sections: [
      { label: 'admin.perm.sec.products', permissions: [Permissions.ProductRead, Permissions.ProductCreate, Permissions.ProductUpdate, Permissions.ProductDelete] },
      { label: 'admin.perm.sec.brands', permissions: [Permissions.BrandRead, Permissions.BrandCreate, Permissions.BrandUpdate, Permissions.BrandDelete] },
      { label: 'admin.perm.sec.categories', permissions: [Permissions.CategoryRead, Permissions.CategoryCreate, Permissions.CategoryUpdate, Permissions.CategoryDelete] },
      { label: 'admin.perm.sec.erpChanges', permissions: [Permissions.ErpChangeRead, Permissions.ErpChangeUpdate, Permissions.ErpChangeDelete] },
    ],
  },
  {
    id: 'documents',
    label: 'admin.perm.cat.documents',
    sections: [
      { label: 'admin.perm.sec.documents', permissions: [Permissions.DocumentRead, Permissions.DocumentUpload, Permissions.DocumentLink] },
    ],
  },
  {
    id: 'cash',
    label: 'admin.perm.cat.cash',
    sections: [
      { label: 'admin.perm.sec.cash', permissions: [Permissions.CashRead, Permissions.CashManage] },
    ],
  },
  {
    id: 'accounting',
    label: 'admin.perm.cat.accounting',
    sections: [
      { label: 'admin.perm.sec.accounting', permissions: [Permissions.AccountingRead, Permissions.AccountingCreate] },
    ],
  },
  {
    id: 'settings',
    label: 'admin.perm.cat.settings',
    sections: [
      { label: 'admin.perm.sec.numbering', permissions: [Permissions.NumberingManage] },
      { label: 'admin.perm.sec.help', permissions: [Permissions.HelpManage] },
      { label: 'admin.perm.sec.email', permissions: [Permissions.EmailRead, Permissions.EmailSend, Permissions.EmailSettingsManage] },
    ],
  },
  {
    id: 'admin',
    label: 'admin.perm.cat.admin',
    sections: [
      { label: 'admin.perm.sec.users', permissions: [Permissions.UserRead, Permissions.UserCreate, Permissions.UserUpdate, Permissions.UserDelete] },
      { label: 'admin.perm.sec.roles', permissions: [Permissions.RoleRead, Permissions.RoleCreate, Permissions.RoleUpdate, Permissions.RoleDelete] },
    ],
  },
];

const PermissionActionLabels: Record<string, string> = {
  Read: 'admin.perm.action.Read',
  Create: 'admin.perm.action.Create',
  Update: 'admin.perm.action.Update',
  Delete: 'admin.perm.action.Delete',
  Manage: 'admin.perm.action.Manage',
  Upload: 'admin.perm.action.Upload',
  Link: 'admin.perm.action.Link',
  Send: 'admin.perm.action.Send',
};
export function permissionActionLabel(code: string): string {
  const action = code.split('.')[1];
  return PermissionActionLabels[action] ?? action ?? code;
}

/** Libellé complet (ex. « Clients — Lecture »). */
export function permissionFullLabel(code: string): string {
  const [entity, action] = code.split('.');
  const section = PermissionCategories
    .flatMap(c => c.sections)
    .find(s => (s.permissions as readonly string[]).includes(code));
  const entityLabel = section?.label ?? entity;
  return `${entityLabel} — ${permissionActionLabel(code)}`;
}

/** Toutes les permissions connues du catalogue métier. */
export function allCatalogPermissions(): PermissionCode[] {
  return PermissionCategories.flatMap(c => c.sections.flatMap(s => s.permissions));
}

/** Catégories filtrées : n'affiche que les permissions présentes dans le catalogue API/local. */
export function buildPermissionCategories(available: string[]): PermissionCategory[] {
  const set = new Set(available);
  return PermissionCategories
    .map(cat => ({
      ...cat,
      sections: cat.sections
        .map(sec => ({ ...sec, permissions: sec.permissions.filter(p => set.has(p)) }))
        .filter(sec => sec.permissions.length > 0),
    }))
    .filter(cat => cat.sections.length > 0);
}
