namespace Backup.Web.Api.Server.Services.Email
{
    public static class EmailTemplateCodes
    {
        public const string QuoteClient = "DEVIS_CLIENT";
        public const string QuoteReminder = "DEVIS_RELANCE";
        public const string OrderConfirmation = "CMD_CONFIRMATION";
        public const string DeliveryShipped = "BL_EXPEDITION";
        public const string InvoiceIssued = "FACTURE_EMISSION";
        public const string CreditNoteIssued = "AVOIR_EMISSION";
        public const string PurchaseOrder = "CDF_EMISSION";
        public const string PaymentReminderN1 = "RELANCE_N1";
        public const string PaymentReminderN2 = "RELANCE_N2";
        public const string PaymentReminderN3 = "RELANCE_N3";
        public const string StockCriticalAlert = "ALERTE_STOCK_CRITIQUE";
    }

    public sealed class EmailTemplateDefinition
    {
        public string Code { get; init; } = string.Empty;
        public string SubjectPattern { get; init; } = string.Empty;
        public string BodyHtmlPattern { get; init; } = string.Empty;
    }

    public static class EmailTemplateCatalog
    {
        public static EmailTemplateDefinition Get(string code) =>
            All.TryGetValue(code, out var t) ? t : Default;

        public static IReadOnlyDictionary<string, EmailTemplateDefinition> All { get; } =
            new Dictionary<string, EmailTemplateDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [EmailTemplateCodes.QuoteClient] = new()
                {
                    Code = EmailTemplateCodes.QuoteClient,
                    SubjectPattern = "[{societe.nom}] Devis {document.numero} - {client.nom}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Veuillez trouver ci-joint notre devis <strong>{document.numero}</strong> du {document.date}.</p>
                        <p>Montant TTC : <strong>{document.montant_ttc}</strong></p>
                        <p>Validité : {document.echeance}</p>
                        <p>Cordialement,<br/>{societe.nom}<br/>{societe.telephone}</p>
                        """
                },
                [EmailTemplateCodes.QuoteReminder] = new()
                {
                    Code = EmailTemplateCodes.QuoteReminder,
                    SubjectPattern = "[{societe.nom}] Relance devis {document.numero} - {client.nom}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Nous nous permettons de revenir vers vous concernant notre devis <strong>{document.numero}</strong> du {document.date}.</p>
                        <p>Montant TTC : <strong>{document.montant_ttc}</strong> — Validité : {document.echeance}</p>
                        <p>Restons à votre disposition pour toute question. Le devis est joint à cet email.</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.InvoiceIssued] = new()
                {
                    Code = EmailTemplateCodes.InvoiceIssued,
                    SubjectPattern = "[{societe.nom}] Facture {document.numero} - {client.nom}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Veuillez trouver ci-joint la facture <strong>{document.numero}</strong> du {document.date}.</p>
                        <p>Montant TTC : <strong>{document.montant_ttc}</strong> — Échéance : {document.echeance}</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.DeliveryShipped] = new()
                {
                    Code = EmailTemplateCodes.DeliveryShipped,
                    SubjectPattern = "[{societe.nom}] Bon de livraison {document.numero} - {client.nom}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Votre commande a été expédiée. Bon de livraison <strong>{document.numero}</strong> en pièce jointe.</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.OrderConfirmation] = new()
                {
                    Code = EmailTemplateCodes.OrderConfirmation,
                    SubjectPattern = "[{societe.nom}] Commande {document.numero} - {client.nom}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Confirmation de votre commande <strong>{document.numero}</strong> du {document.date}.</p>
                        <p>Montant TTC : <strong>{document.montant_ttc}</strong></p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.CreditNoteIssued] = new()
                {
                    Code = EmailTemplateCodes.CreditNoteIssued,
                    SubjectPattern = "[{societe.nom}] Avoir {document.numero} - {client.nom}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Veuillez trouver ci-joint l'avoir <strong>{document.numero}</strong>.</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.PurchaseOrder] = new()
                {
                    Code = EmailTemplateCodes.PurchaseOrder,
                    SubjectPattern = "[{societe.nom}] Commande fournisseur {document.numero} - {fournisseur.nom}",
                    BodyHtmlPattern = """
                        <p>Bonjour,</p>
                        <p>Veuillez trouver ci-joint notre commande <strong>{document.numero}</strong> du {document.date}.</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.PaymentReminderN1] = new()
                {
                    Code = EmailTemplateCodes.PaymentReminderN1,
                    SubjectPattern = "[{societe.nom}] Rappel — Facture {document.numero} échue",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Sauf erreur de notre part, la facture <strong>{document.numero}</strong> du {document.date} d'un montant de <strong>{document.montant_ttc}</strong> reste impayée.</p>
                        <p>Échéance : {document.echeance} — Retard : {document.jours_retard} jour(s) — Reste dû : <strong>{document.reste_du}</strong></p>
                        <p>Merci de procéder au règlement dans les meilleurs délais. La facture est jointe à cet email.</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.PaymentReminderN2] = new()
                {
                    Code = EmailTemplateCodes.PaymentReminderN2,
                    SubjectPattern = "[{societe.nom}] 2e relance — Facture {document.numero}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p>Malgré notre premier rappel, la facture <strong>{document.numero}</strong> ({document.montant_ttc}) demeure impayée depuis {document.jours_retard} jour(s).</p>
                        <p>Reste dû : <strong>{document.reste_du}</strong> — Échéance initiale : {document.echeance}</p>
                        <p>Nous vous prions de régulariser votre situation rapidement.</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.PaymentReminderN3] = new()
                {
                    Code = EmailTemplateCodes.PaymentReminderN3,
                    SubjectPattern = "[{societe.nom}] Dernière relance — Facture {document.numero}",
                    BodyHtmlPattern = """
                        <p>Bonjour {client.nom},</p>
                        <p><strong>Dernière relance</strong> concernant la facture <strong>{document.numero}</strong> échue le {document.echeance}.</p>
                        <p>Montant TTC : {document.montant_ttc} — Reste dû : <strong>{document.reste_du}</strong> — Retard : {document.jours_retard} jour(s).</p>
                        <p>À défaut de paiement sous 7 jours, nous nous réservons le droit d'engager une procédure de recouvrement.</p>
                        <p>Cordialement,<br/>{societe.nom}</p>
                        """
                },
                [EmailTemplateCodes.StockCriticalAlert] = new()
                {
                    Code = EmailTemplateCodes.StockCriticalAlert,
                    SubjectPattern = "[{societe.nom}] Alerte stock — {produit.cle}",
                    BodyHtmlPattern = """
                        <p>Bonjour,</p>
                        <p>Le stock du produit <strong>{produit.cle}</strong> est sous le seuil minimum.</p>
                        <ul>
                          <li>Disponible : <strong>{produit.disponible}</strong></li>
                          <li>Seuil minimum : <strong>{produit.min}</strong></li>
                          <li>En stock : {produit.quantite}</li>
                          <li>Réservé : {produit.reserve}</li>
                        </ul>
                        <p>{societe.nom}</p>
                        """
                }
            };

        private static readonly EmailTemplateDefinition Default = new()
        {
            Code = "DEFAULT",
            SubjectPattern = "[{societe.nom}] Document {document.numero}",
            BodyHtmlPattern = "<p>Bonjour,</p><p>Veuillez trouver le document {document.numero} en pièce jointe.</p><p>{societe.nom}</p>"
        };
    }
}
