using System;
using System.Collections.Generic;

namespace Backup.Web.Api.Server.Models.Security
{
    public class Permission
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Code { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Module { get; set; }
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }

    public class RolePermission
    {
        public Guid RoleId { get; set; }
        public Guid PermissionId { get; set; }
        public Permission? Permission { get; set; }
    }

    /// <summary>Constantes de permissions au format Module.Action.</summary>
    public static class Permissions
    {
        // Produits
        public const string ProductRead      = "Product.Read";
        public const string ProductCreate    = "Product.Create";
        public const string ProductUpdate    = "Product.Update";
        public const string ProductDelete    = "Product.Delete";

        // Marques catalogue
        public const string BrandRead        = "Brand.Read";
        public const string BrandCreate      = "Brand.Create";
        public const string BrandUpdate      = "Brand.Update";
        public const string BrandDelete      = "Brand.Delete";

        // Catégories catalogue
        public const string CategoryRead     = "Category.Read";
        public const string CategoryCreate   = "Category.Create";
        public const string CategoryUpdate   = "Category.Update";
        public const string CategoryDelete   = "Category.Delete";

        // Changements ERP (journal sync / import)
        public const string ErpChangeRead    = "ErpChange.Read";
        public const string ErpChangeUpdate  = "ErpChange.Update";
        public const string ErpChangeDelete  = "ErpChange.Delete";

        // Fournisseurs
        public const string SupplierRead     = "Supplier.Read";
        public const string SupplierCreate   = "Supplier.Create";
        public const string SupplierUpdate   = "Supplier.Update";
        public const string SupplierDelete   = "Supplier.Delete";

        // Clients
        public const string CustomerRead     = "Customer.Read";
        public const string CustomerCreate   = "Customer.Create";
        public const string CustomerUpdate   = "Customer.Update";
        public const string CustomerDelete   = "Customer.Delete";

        // Devis
        public const string QuoteRead        = "Quote.Read";
        public const string QuoteCreate      = "Quote.Create";
        public const string QuoteUpdate      = "Quote.Update";
        public const string QuoteDelete      = "Quote.Delete";

        // Commandes vente
        public const string OrderRead        = "Order.Read";
        public const string OrderCreate      = "Order.Create";
        public const string OrderUpdate      = "Order.Update";
        public const string OrderDelete      = "Order.Delete";

        // Bons de livraison vente
        public const string DeliveryNoteRead   = "DeliveryNote.Read";
        public const string DeliveryNoteCreate = "DeliveryNote.Create";
        public const string DeliveryNoteDelete = "DeliveryNote.Delete";

        // Retours client (BRC — RG-BR1-5)
        public const string SalesReturnRead    = "SalesReturn.Read";
        public const string SalesReturnCreate  = "SalesReturn.Create";
        public const string SalesReturnUpdate  = "SalesReturn.Update";

        // Factures vente
        public const string InvoiceRead      = "Invoice.Read";
        public const string InvoiceCreate    = "Invoice.Create";
        public const string InvoiceUpdate    = "Invoice.Update";
        public const string InvoiceDelete    = "Invoice.Delete";

        // Commandes achat
        public const string PurchaseOrderRead   = "PurchaseOrder.Read";
        public const string PurchaseOrderCreate = "PurchaseOrder.Create";
        public const string PurchaseOrderUpdate = "PurchaseOrder.Update";
        public const string PurchaseOrderDelete = "PurchaseOrder.Delete";

        // Réceptions achat
        public const string ReceiptRead      = "Receipt.Read";
        public const string ReceiptCreate    = "Receipt.Create";
        public const string ReceiptDelete    = "Receipt.Delete";

        // Factures fournisseur
        public const string SupplierInvoiceRead   = "SupplierInvoice.Read";
        public const string SupplierInvoiceCreate = "SupplierInvoice.Create";
        public const string SupplierInvoiceDelete = "SupplierInvoice.Delete";

        // Avoirs fournisseur (AF — RG-AF1-5)
        public const string SupplierCreditNoteRead   = "SupplierCreditNote.Read";
        public const string SupplierCreditNoteCreate = "SupplierCreditNote.Create";
        public const string SupplierCreditNoteUpdate = "SupplierCreditNote.Update";

        // Stock
        public const string StockRead        = "Stock.Read";
        public const string StockUpdate      = "Stock.Update";

        // Caisse
        public const string CashRead         = "Cash.Read";
        public const string CashManage       = "Cash.Manage";

        // Comptabilité
        public const string AccountingRead   = "Accounting.Read";
        public const string AccountingCreate = "Accounting.Create";

        // Numérotation (admin)
        public const string NumberingManage  = "Numbering.Manage";

        // Aide métier (CMS + analytics)
        public const string HelpManage       = "Help.Manage";

        // Documents (upload / association)
        public const string DocumentRead     = "Document.Read";
        public const string DocumentUpload   = "Document.Upload";
        public const string DocumentLink     = "Document.Link";

        // Administration
        public const string UserRead         = "User.Read";
        public const string UserCreate       = "User.Create";
        public const string UserUpdate       = "User.Update";
        public const string UserDelete       = "User.Delete";
        public const string RoleRead         = "Role.Read";
        public const string RoleCreate       = "Role.Create";
        public const string RoleUpdate       = "Role.Update";
        public const string RoleDelete       = "Role.Delete";

        // Email
        public const string EmailRead           = "Email.Read";
        public const string EmailSend           = "Email.Send";
        public const string EmailSettingsManage = "Email.Settings";

        public static IReadOnlyList<string> All => new[]
        {
            ProductRead, ProductCreate, ProductUpdate, ProductDelete,
            BrandRead, BrandCreate, BrandUpdate, BrandDelete,
            CategoryRead, CategoryCreate, CategoryUpdate, CategoryDelete,
            ErpChangeRead, ErpChangeUpdate, ErpChangeDelete,
            SupplierRead, SupplierCreate, SupplierUpdate, SupplierDelete,
            CustomerRead, CustomerCreate, CustomerUpdate, CustomerDelete,
            QuoteRead, QuoteCreate, QuoteUpdate, QuoteDelete,
            OrderRead, OrderCreate, OrderUpdate, OrderDelete,
            DeliveryNoteRead, DeliveryNoteCreate, DeliveryNoteDelete,
            SalesReturnRead, SalesReturnCreate, SalesReturnUpdate,
            InvoiceRead, InvoiceCreate, InvoiceUpdate, InvoiceDelete,
            PurchaseOrderRead, PurchaseOrderCreate, PurchaseOrderUpdate, PurchaseOrderDelete,
            ReceiptRead, ReceiptCreate, ReceiptDelete,
            SupplierInvoiceRead, SupplierInvoiceCreate, SupplierInvoiceDelete,
            SupplierCreditNoteRead, SupplierCreditNoteCreate, SupplierCreditNoteUpdate,
            StockRead, StockUpdate,
            CashRead, CashManage,
            AccountingRead, AccountingCreate,
            NumberingManage,
            HelpManage,
            DocumentRead, DocumentUpload, DocumentLink,
            UserRead, UserCreate, UserUpdate, UserDelete,
            RoleRead, RoleCreate, RoleUpdate, RoleDelete,
            EmailRead, EmailSend, EmailSettingsManage,
        };
    }
}
