"""
Module de gestion du catalogue produit pour l'identification des produits.
Permet de charger un catalogue depuis une base de données ou un fichier JSON
et de matcher les produits extraits des factures avec le catalogue.
"""
import json
import os
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from pathlib import Path


@dataclass
class ProductCatalogItem:
    """Représente un produit dans le catalogue"""
    id: Optional[int] = None
    reference: Optional[str] = None
    sku: Optional[str] = None
    barcode: Optional[str] = None
    gtin: Optional[str] = None
    name_en: Optional[str] = None
    name_fr: Optional[str] = None
    name_nl: Optional[str] = None
    description_en: Optional[str] = None
    description_fr: Optional[str] = None
    description_nl: Optional[str] = None
    selling_price: Optional[float] = None
    cost_price: Optional[float] = None
    stock_quantity: Optional[int] = None
    weight_kg: Optional[float] = None
    brand_id: Optional[int] = None
    category_id: Optional[int] = None
    is_active: bool = True
    
    # Variantes du produit
    variants: List[Dict] = None
    
    def __post_init__(self):
        if self.variants is None:
            self.variants = []
    
    def get_name(self, lang: str = 'nl') -> Optional[str]:
        """Retourne le nom dans la langue demandée"""
        if lang.lower() == 'en':
            return self.name_en
        elif lang.lower() == 'fr':
            return self.name_fr
        elif lang.lower() == 'nl':
            return self.name_nl
        return self.name_nl or self.name_fr or self.name_en
    
    def get_description(self, lang: str = 'nl') -> Optional[str]:
        """Retourne la description dans la langue demandée"""
        if lang.lower() == 'en':
            return self.description_en
        elif lang.lower() == 'fr':
            return self.description_fr
        elif lang.lower() == 'nl':
            return self.description_nl
        return self.description_nl or self.description_fr or self.description_en
    
    def has_identifier(self, identifier: str) -> bool:
        """Vérifie si le produit a cet identifiant (SKU, EAN, Barcode, GTIN)"""
        identifier = identifier.strip().upper()
        return (
            (self.sku and self.sku.upper() == identifier) or
            (self.barcode and self.barcode.upper() == identifier) or
            (self.gtin and self.gtin.upper() == identifier) or
            (self.reference and self.reference.upper() == identifier) or
            any(v.get('sku', '').upper() == identifier or 
                v.get('barcode', '').upper() == identifier 
                for v in self.variants)
        )


class ProductCatalog:
    """Gestionnaire de catalogue produit"""
    
    def __init__(self, catalog_data: Optional[List[Dict]] = None):
        """
        Initialise le catalogue
        
        Args:
            catalog_data: Liste de dictionnaires représentant les produits
        """
        self.products: List[ProductCatalogItem] = []
        self._index_by_sku: Dict[str, List[ProductCatalogItem]] = {}
        self._index_by_ean: Dict[str, List[ProductCatalogItem]] = {}
        self._index_by_barcode: Dict[str, List[ProductCatalogItem]] = {}
        self._index_by_gtin: Dict[str, List[ProductCatalogItem]] = {}
        self._index_by_reference: Dict[str, List[ProductCatalogItem]] = {}
        
        if catalog_data:
            self.load_from_data(catalog_data)
    
    def load_from_data(self, catalog_data: List[Dict]):
        """Charge le catalogue depuis une liste de dictionnaires"""
        self.products = []
        
        for item_data in catalog_data:
            product = ProductCatalogItem(
                id=item_data.get('Id'),
                reference=item_data.get('Reference'),
                sku=item_data.get('Sku'),
                barcode=item_data.get('Barcode'),
                gtin=item_data.get('Gtin'),
                name_en=item_data.get('Name_EN'),
                name_fr=item_data.get('Name_FR'),
                name_nl=item_data.get('Name_NL'),
                description_en=item_data.get('Description_EN'),
                description_fr=item_data.get('Description_FR'),
                description_nl=item_data.get('Description_NL'),
                selling_price=item_data.get('SellingPrice'),
                cost_price=item_data.get('CostPrice'),
                stock_quantity=item_data.get('StockQuantity'),
                weight_kg=item_data.get('WeightKg'),
                brand_id=item_data.get('BrandId'),
                category_id=item_data.get('CategoryId'),
                is_active=item_data.get('IsActive', True),
                variants=item_data.get('variants', [])
            )
            
            self.products.append(product)
        
        self._build_indexes()
    
    def load_from_json(self, json_path: str):
        """Charge le catalogue depuis un fichier JSON"""
        with open(json_path, 'r', encoding='utf-8') as f:
            catalog_data = json.load(f)
        self.load_from_data(catalog_data)
    
    def load_from_sql(self, connection_string: str):
        """
        Charge le catalogue depuis une base de données SQL
        Nécessite pyodbc ou pymssql
        """
        try:
            import pyodbc
        except ImportError:
            try:
                import pymssql
            except ImportError:
                raise ImportError("pyodbc ou pymssql est requis pour charger depuis SQL")
        
        # TODO: Implémenter la connexion SQL et le chargement
        # Pour l'instant, on suppose qu'on a déjà les données
        pass
    
    def _build_indexes(self):
        """Construit les index pour la recherche rapide"""
        self._index_by_sku.clear()
        self._index_by_ean.clear()
        self._index_by_barcode.clear()
        self._index_by_gtin.clear()
        self._index_by_reference.clear()
        
        for product in self.products:
            if not product.is_active:
                continue
            
            # Index par SKU
            if product.sku:
                sku_upper = product.sku.upper().strip()
                if sku_upper not in self._index_by_sku:
                    self._index_by_sku[sku_upper] = []
                self._index_by_sku[sku_upper].append(product)
            
            # Index par EAN/Barcode (13 chiffres)
            if product.barcode and len(product.barcode) == 13:
                barcode_upper = product.barcode.upper().strip()
                if barcode_upper not in self._index_by_ean:
                    self._index_by_ean[barcode_upper] = []
                self._index_by_ean[barcode_upper].append(product)
            
            # Index par Barcode
            if product.barcode:
                barcode_upper = product.barcode.upper().strip()
                if barcode_upper not in self._index_by_barcode:
                    self._index_by_barcode[barcode_upper] = []
                self._index_by_barcode[barcode_upper].append(product)
            
            # Index par GTIN
            if product.gtin:
                gtin_upper = product.gtin.upper().strip()
                if gtin_upper not in self._index_by_gtin:
                    self._index_by_gtin[gtin_upper] = []
                self._index_by_gtin[gtin_upper].append(product)
            
            # Index par Reference
            if product.reference:
                ref_upper = product.reference.upper().strip()
                if ref_upper not in self._index_by_reference:
                    self._index_by_reference[ref_upper] = []
                self._index_by_reference[ref_upper].append(product)
            
            # Index par variantes
            for variant in product.variants:
                variant_sku = variant.get('Sku')
                variant_barcode = variant.get('Barcode')
                
                if variant_sku:
                    sku_upper = variant_sku.upper().strip()
                    if sku_upper not in self._index_by_sku:
                        self._index_by_sku[sku_upper] = []
                    self._index_by_sku[sku_upper].append(product)
                
                if variant_barcode:
                    barcode_upper = variant_barcode.upper().strip()
                    if len(barcode_upper) == 13:
                        if barcode_upper not in self._index_by_ean:
                            self._index_by_ean[barcode_upper] = []
                        self._index_by_ean[barcode_upper].append(product)
                    if barcode_upper not in self._index_by_barcode:
                        self._index_by_barcode[barcode_upper] = []
                    self._index_by_barcode[barcode_upper].append(product)
    
    def find_by_sku(self, sku: str) -> List[ProductCatalogItem]:
        """Trouve les produits par SKU"""
        if not sku:
            return []
        sku_upper = sku.upper().strip()
        return self._index_by_sku.get(sku_upper, [])
    
    def find_by_ean(self, ean: str) -> List[ProductCatalogItem]:
        """Trouve les produits par EAN (13 chiffres)"""
        if not ean or len(ean) != 13:
            return []
        ean_upper = ean.upper().strip()
        return self._index_by_ean.get(ean_upper, [])
    
    def find_by_barcode(self, barcode: str) -> List[ProductCatalogItem]:
        """Trouve les produits par code-barres"""
        if not barcode:
            return []
        barcode_upper = barcode.upper().strip()
        return self._index_by_barcode.get(barcode_upper, [])
    
    def find_by_gtin(self, gtin: str) -> List[ProductCatalogItem]:
        """Trouve les produits par GTIN"""
        if not gtin:
            return []
        gtin_upper = gtin.upper().strip()
        return self._index_by_gtin.get(gtin_upper, [])
    
    def find_by_reference(self, reference: str) -> List[ProductCatalogItem]:
        """Trouve les produits par référence"""
        if not reference:
            return []
        ref_upper = reference.upper().strip()
        return self._index_by_reference.get(ref_upper, [])
    
    def match_product(self, 
                     sku: Optional[str] = None,
                     ean: Optional[str] = None,
                     barcode: Optional[str] = None,
                     gtin: Optional[str] = None,
                     reference: Optional[str] = None,
                     description: Optional[str] = None) -> Optional[ProductCatalogItem]:
        """
        Trouve un produit dans le catalogue en utilisant les identifiants disponibles.
        Priorité: EAN > SKU > Barcode > GTIN > Reference > Description (fuzzy)
        
        Returns:
            Le produit trouvé ou None
        """
        # Priorité 1: EAN (13 chiffres)
        if ean and len(ean) == 13:
            matches = self.find_by_ean(ean)
            if matches:
                return matches[0]
        
        # Priorité 2: SKU
        if sku:
            matches = self.find_by_sku(sku)
            if matches:
                return matches[0]
        
        # Priorité 3: Barcode
        if barcode:
            matches = self.find_by_barcode(barcode)
            if matches:
                return matches[0]
        
        # Priorité 4: GTIN
        if gtin:
            matches = self.find_by_gtin(gtin)
            if matches:
                return matches[0]
        
        # Priorité 5: Reference
        if reference:
            matches = self.find_by_reference(reference)
            if matches:
                return matches[0]
        
        # Priorité 6: Description (fuzzy match - recherche partielle)
        if description:
            description_lower = description.lower().strip()
            # Chercher dans les noms et descriptions
            for product in self.products:
                if not product.is_active:
                    continue
                
                # Vérifier les noms
                for name in [product.name_nl, product.name_fr, product.name_en]:
                    if name and description_lower in name.lower():
                        return product
                
                # Vérifier les descriptions
                for desc in [product.description_nl, product.description_fr, product.description_en]:
                    if desc and description_lower in desc.lower():
                        return product
        
        return None
    
    def enrich_product(self, extracted_product: Dict) -> Dict:
        """
        Enrichit un produit extrait avec les informations du catalogue
        
        Args:
            extracted_product: Produit extrait de la facture avec les champs:
                - sku, ean, barcode, gtin, reference, description
        
        Returns:
            Le produit enrichi avec les champs du catalogue:
                - catalog_id, catalog_name, catalog_description, catalog_price, etc.
        """
        matched = self.match_product(
            sku=extracted_product.get('sku'),
            ean=extracted_product.get('ean'),
            barcode=extracted_product.get('barcode'),
            gtin=extracted_product.get('gtin'),
            reference=extracted_product.get('reference'),
            description=extracted_product.get('description')
        )
        
        if matched:
            extracted_product['catalog_id'] = matched.id
            extracted_product['catalog_reference'] = matched.reference
            extracted_product['catalog_sku'] = matched.sku
            extracted_product['catalog_name_nl'] = matched.name_nl
            extracted_product['catalog_name_fr'] = matched.name_fr
            extracted_product['catalog_name_en'] = matched.name_en
            extracted_product['catalog_description_nl'] = matched.description_nl
            extracted_product['catalog_description_fr'] = matched.description_fr
            extracted_product['catalog_description_en'] = matched.description_en
            extracted_product['catalog_selling_price'] = matched.selling_price
            extracted_product['catalog_cost_price'] = matched.cost_price
            extracted_product['catalog_stock_quantity'] = matched.stock_quantity
            extracted_product['catalog_weight_kg'] = matched.weight_kg
            extracted_product['catalog_brand_id'] = matched.brand_id
            extracted_product['catalog_category_id'] = matched.category_id
            extracted_product['catalog_matched'] = True
        else:
            extracted_product['catalog_matched'] = False
        
        return extracted_product


# Instance globale du catalogue (chargée au démarrage)
_global_catalog: Optional[ProductCatalog] = None


def get_catalog() -> Optional[ProductCatalog]:
    """Retourne l'instance globale du catalogue"""
    return _global_catalog


def load_catalog(json_path: Optional[str] = None, catalog_data: Optional[List[Dict]] = None):
    """
    Charge le catalogue global
    
    Args:
        json_path: Chemin vers le fichier JSON du catalogue
        catalog_data: Données du catalogue (liste de dictionnaires)
    """
    global _global_catalog
    
    if json_path:
        _global_catalog = ProductCatalog()
        _global_catalog.load_from_json(json_path)
    elif catalog_data:
        _global_catalog = ProductCatalog(catalog_data)
    else:
        # Chercher un fichier catalogue par défaut
        default_path = Path(__file__).parent.parent / 'data' / 'product_catalog.json'
        if default_path.exists():
            _global_catalog = ProductCatalog()
            _global_catalog.load_from_json(str(default_path))
        else:
            _global_catalog = ProductCatalog()  # Catalogue vide
    
    print(f"[PRODUCT CATALOG] Catalogue chargé: {len(_global_catalog.products)} produits")


def enrich_products_with_catalog(products: List[Dict]) -> List[Dict]:
    """
    Enrichit une liste de produits avec les informations du catalogue
    
    Args:
        products: Liste de produits extraits
    
    Returns:
        Liste de produits enrichis
    """
    catalog = get_catalog()
    if not catalog:
        return products
    
    enriched = []
    for product in products:
        enriched.append(catalog.enrich_product(product))
    
    return enriched
