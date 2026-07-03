# Clés de traduction pour la validation des documents

## Clés ajoutées dans `SupplierDocumentViewModel.cs`

Toutes les clés suivantes doivent être ajoutées aux fichiers de ressources de traduction (fichiers `.resx` ou équivalents) pour chaque langue supportée (FR, NL, EN).

### Messages de validation - Raisons de non-validation

| Clé | Français (FR) | Néerlandais (NL) | Anglais (EN) |
|-----|---------------|------------------|--------------|
| `SupplierDocument_ValidationReason_SupplierMissing` | • Le fournisseur n'est pas associé au document | • De leverancier is niet gekoppeld aan het document | • The supplier is not associated with the document |
| `SupplierDocument_ValidationReason_DocumentNotMatched` | • Le document actuel ({DocumentType}) n'est pas encore matché (les produits doivent être associés) | • Het huidige document ({DocumentType}) is nog niet gekoppeld (producten moeten worden gekoppeld) | • The current document ({DocumentType}) is not yet matched (products must be associated) |
| `SupplierDocument_ValidationReason_LinkedDocumentNotMatched` | • Le document lié ({DocumentType}) n'est pas encore matché | • Het gekoppelde document ({DocumentType}) is nog niet gekoppeld | • The linked document ({DocumentType}) is not yet matched |
| `SupplierDocument_ValidationReason_AlreadyPosted` | • Le document a déjà été comptabilisé | • Het document is al geboekt | • The document has already been posted |
| `SupplierDocument_ValidationReason_Rejected` | • Le document a été rejeté | • Het document is afgewezen | • The document has been rejected |
| `SupplierDocument_ValidationReason_HasErrors` | • Des erreurs bloquantes sont présentes (voir l'onglet Écarts) | • Er zijn blokkerende fouten aanwezig (zie het tabblad Afwijkingen) | • Blocking errors are present (see the Issues tab) |

### Messages de validation - En-têtes et instructions

| Clé | Français (FR) | Néerlandais (NL) | Anglais (EN) |
|-----|---------------|------------------|--------------|
| `SupplierDocument_ValidationCannotValidateHeader` | Le document ne peut pas être validé pour les raisons suivantes : | Het document kan niet worden gevalideerd om de volgende redenen: | The document cannot be validated for the following reasons: |
| `SupplierDocument_ValidationInstructionsHeader` | Pour valider un document : | Om een document te valideren: | To validate a document: |
| `SupplierDocument_ValidationInstruction1` | 1. Associer un fournisseur si manquant | 1. Koppel een leverancier indien ontbreekt | 1. Associate a supplier if missing |
| `SupplierDocument_ValidationInstruction2` | 2. Tous les documents (actuel et liés) doivent être matchés (les produits doivent être trouvés dans l'ERP) | 2. Alle documenten (huidig en gekoppeld) moeten worden gekoppeld (producten moeten worden gevonden in de ERP) | 2. All documents (current and linked) must be matched (products must be found in the ERP) |
| `SupplierDocument_ValidationInstruction3` | 3. Corriger toutes les erreurs dans l'onglet 'Écarts' | 3. Corrigeer alle fouten in het tabblad 'Afwijkingen' | 3. Correct all errors in the 'Issues' tab |
| `SupplierDocument_ValidationCannotValidateTitle` | Validation impossible | Validatie onmogelijk | Validation impossible |

### Messages de validation - Actions

| Clé | Français (FR) | Néerlandais (NL) | Anglais (EN) |
|-----|---------------|------------------|--------------|
| `SupplierDocument_ValidationAction_CreateInvoice` | • Création d'une facture fournisseur avec toutes les lignes | • Aanmaak van een leveranciersfactuur met alle regels | • Creation of a supplier invoice with all lines |
| `SupplierDocument_ValidationAction_InvoiceAccounting` | • Les montants seront comptabilisés dans les comptes fournisseurs | • De bedragen worden geboekt op de leveranciersrekeningen | • Amounts will be posted to supplier accounts |
| `SupplierDocument_ValidationAction_CreateReceipt` | • Création d'une réception | • Aanmaak van een ontvangst | • Creation of a receipt |
| `SupplierDocument_ValidationAction_CreateReceiptFromLinked` | • Création d'une réception depuis le bon de livraison lié | • Aanmaak van een ontvangst vanaf het gekoppelde leveringsbon | • Creation of a receipt from the linked delivery note |
| `SupplierDocument_ValidationAction_UpdateStock` | • Mise à jour des quantités en stock pour chaque produit | • Bijwerken van de voorraadhoeveelheden voor elk product | • Update stock quantities for each product |
| `SupplierDocument_ValidationAction_CreateStockMovements` | • Création des mouvements de stock correspondants | • Aanmaak van de bijbehorende voorraadbewegingen | • Creation of corresponding stock movements |
| `SupplierDocument_ValidationAction_CreateInvoiceFromLinked` | • Création d'une facture fournisseur depuis la facture liée | • Aanmaak van een leveranciersfactuur vanaf de gekoppelde factuur | • Creation of a supplier invoice from the linked invoice |

### Messages de validation - Confirmation

| Clé | Français (FR) | Néerlandais (NL) | Anglais (EN) |
|-----|---------------|------------------|--------------|
| `SupplierDocument_ValidationConfirmMessage` | Êtes-vous sûr de vouloir valider et comptabiliser ce document ? | Weet u zeker dat u dit document wilt valideren en boeken? | Are you sure you want to validate and post this document? |
| `SupplierDocument_ValidationActionWillDo` | Cette action va : | Deze actie zal: | This action will: |
| `SupplierDocument_ValidationDocumentWillBePosted` | Le document sera marqué comme 'Posté' et ne pourra plus être modifié. | Het document wordt gemarkeerd als 'Geboekt' en kan niet meer worden gewijzigd. | The document will be marked as 'Posted' and can no longer be modified. |
| `SupplierDocument_ValidationConfirmTitle` | Confirmation - Valider et comptabiliser | Bevestiging - Valideren en boeken | Confirmation - Validate and post |

### Messages de validation - Résultats

| Clé | Français (FR) | Néerlandais (NL) | Anglais (EN) |
|-----|---------------|------------------|--------------|
| `SupplierDocument_ValidationSuccessMessage` | Document validé et comptabilisé avec succès. | Document succesvol gevalideerd en geboekt. | Document validated and posted successfully. |
| `SupplierDocument_ValidationSuccessTitle` | Succès | Succes | Success |
| `SupplierDocument_ValidationErrorMessage` | Erreur lors de la validation: {ErrorMessage} | Fout bij validatie: {ErrorMessage} | Error during validation: {ErrorMessage} |
| `SupplierDocument_ValidationErrorTitle` | Erreur | Fout | Error |
| `SupplierDocument_AssociationErrorMessage` | Erreur lors de l'association: {ErrorMessage} | Fout bij koppeling: {ErrorMessage} | Error during association: {ErrorMessage} |
| `SupplierDocument_ErrorTitle` | Erreur | Fout | Error |

## Notes importantes

1. **Placeholders** : Les clés contenant `{DocumentType}` ou `{ErrorMessage}` doivent être remplacées dynamiquement dans le code avant affichage.

2. **Format** : Les messages utilisent `\n` pour les sauts de ligne. Assurez-vous que votre système de traduction gère correctement les sauts de ligne.

3. **Ordre des actions** : Les messages d'actions sont construits dynamiquement selon le type de document et la présence de documents liés. L'ordre dans lequel ils apparaissent est important.

4. **Fallback** : Toutes les clés ont un fallback en français dans le code. Si une clé n'est pas trouvée dans les fichiers de traduction, le message français sera affiché.

## Fichiers à modifier

Les fichiers de ressources de traduction doivent être mis à jour pour chaque langue supportée. Généralement, ces fichiers se trouvent dans :
- `Resources/Resources.fr.resx` (Français)
- `Resources/Resources.nl.resx` (Néerlandais)
- `Resources/Resources.en.resx` (Anglais)

Ou dans un dossier similaire selon l'organisation du projet.

## Exemple d'utilisation

```csharp
var message = _localization.GetString("SupplierDocument_ValidationConfirmMessage") 
    ?? "Êtes-vous sûr de vouloir valider et comptabiliser ce document ?";
```

Le système de traduction utilisera automatiquement la langue sélectionnée par l'utilisateur.
