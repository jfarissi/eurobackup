"""
Script pour parser le CSV Knauf et générer un script SQL pour les produits et variantes.
"""
import csv
import uuid
import re
from datetime import datetime
from typing import Dict, List, Optional
from pathlib import Path
from io import StringIO

# Constantes
COMPANY_ID = "0B470A4F-F073-4B12-B54E-A4C1DC234F67"
DEFAULT_USER_ID = "C3631737-A81C-47F3-8499-A52154A24A01"
TODAY = datetime.now()
SQL_DATETIMEOFFSET_FORMAT = TODAY.strftime("%Y-%m-%d %H:%M:%S +00:00")


def escape_sql(value):
    """Échappe une valeur pour SQL"""
    if value is None or value == "":
        return "NULL"
    if isinstance(value, bool):
        return "1" if value else "0"
    if isinstance(value, (int, float)):
        return str(value)
    # Échapper les apostrophes
    escaped = str(value).replace("'", "''")
    return f"'{escaped}'"


def generate_slug(text: str) -> str:
    """Génère un slug depuis un texte"""
    if not text:
        return ""
    slug = text.lower()
    # Remplacer les caractères accentués
    replacements = {
        'à': 'a', 'á': 'a', 'â': 'a', 'ã': 'a', 'ä': 'a', 'å': 'a',
        'è': 'e', 'é': 'e', 'ê': 'e', 'ë': 'e',
        'ì': 'i', 'í': 'i', 'î': 'i', 'ï': 'i',
        'ò': 'o', 'ó': 'o', 'ô': 'o', 'õ': 'o', 'ö': 'o',
        'ù': 'u', 'ú': 'u', 'û': 'u', 'ü': 'u',
        'ç': 'c', 'ñ': 'n',
        'ß': 'ss'
    }
    for old, new in replacements.items():
        slug = slug.replace(old, new)
    # Remplacer les espaces et caractères spéciaux par des tirets
    slug = re.sub(r'[^a-z0-9]+', '-', slug)
    slug = slug.strip('-')
    if len(slug) > 200:
        slug = slug[:200]
    return slug if slug else "product"


def parse_dimension(value: str) -> Optional[float]:
    """Parse une dimension (mm, cm, m, kg) et retourne en cm ou kg"""
    if not value or value == "" or value == "-" or value == "0":
        return None
    try:
        # Extraire le nombre et l'unité
        match = re.match(r'^(\d+(?:\.\d+)?)\s*(mm|cm|m|kg|KG|TO|pce|pal)?', str(value).strip(), re.IGNORECASE)
        if match:
            num = float(match.group(1))
            unit = match.group(2) or ""
            unit_lower = unit.lower()
            
            if unit_lower in ['mm']:
                return num / 10.0  # Convertir mm en cm
            elif unit_lower in ['cm']:
                return num
            elif unit_lower in ['m']:
                return num * 100  # Convertir m en cm
            elif unit_lower in ['kg', 'kg']:
                return num  # Poids en kg
            else:
                return num  # Par défaut, considérer comme cm
        return None
    except:
        return None


def parse_weight(value: str) -> Optional[float]:
    """Parse un poids et retourne en kg"""
    if not value or value == "" or value == "-":
        return None
    try:
        # Extraire le nombre
        match = re.match(r'^(\d+(?:\.\d+)?)', str(value).strip())
        if match:
            return float(match.group(1))
        return None
    except:
        return None


def parse_csv_to_products(csv_path: str) -> Dict:
    """Parse le CSV Knauf et retourne une structure produits/variantes/catégories"""
    products_dict = {}  # Clé: Product_Key, Valeur: produit avec variantes
    variants_list = []
    categories_dict = {}  # Clé: (level, name_fr), Valeur: catégorie
    category_hierarchy = {}  # Pour stocker la hiérarchie: level1 -> level2 -> level3
    
    # Essayer différents encodages
    encodings = ['utf-8', 'windows-1252', 'iso-8859-1', 'cp1252']
    file_content = None
    encoding_used = None
    
    for enc in encodings:
        try:
            with open(csv_path, 'r', encoding=enc) as f:
                file_content = f.read()
            encoding_used = enc
            break
        except UnicodeDecodeError:
            continue
    
    if file_content is None:
        raise ValueError(f"Impossible de décoder le fichier CSV avec les encodages: {encodings}")
    
    # Utiliser StringIO pour simuler un fichier
    f = StringIO(file_content)
    
    # Lire les 2 premières lignes (ignorer)
    for _ in range(2):
        next(f)
    
    # Lire la ligne 3 qui contient les noms de colonnes techniques
    header_line = next(f).strip()
    column_names = [col.strip() for col in header_line.split(';')]
    
    # Ignorer la ligne 4 (noms en FR/NL)
    next(f)
    
    # Lire le CSV ligne par ligne (pas DictReader car certaines colonnes sont vides)
    for line in f:
        line = line.strip()
        if not line:
            continue
        
        # Parser la ligne manuellement
        row_values = line.split(';')
        
        # Créer un dict avec les colonnes nommées, mais aussi garder les valeurs brutes
        row = {}
        for i, col_name in enumerate(column_names):
            if col_name:  # Ignorer les colonnes vides
                row[col_name] = row_values[i] if i < len(row_values) else ''
        
        # Garder aussi les valeurs brutes pour accéder aux colonnes sans nom
        row['_raw_values'] = row_values
        raw_values = row_values  # Pour utilisation immédiate
        
        # Extraire les catégories (hiérarchie)
        # Colonnes: 0=SortKey1, 1=Hierarchy1_FR, 2=Hierarchy1_NL, 3=SortKey2, 4=Hierarchy2_FR, 5=Hierarchy2_NL, 6=SortKey3, 7=Hierarchy3_FR, 8=Hierarchy3_NL
        hierarchy1_fr = raw_values[1].strip() if len(raw_values) > 1 else ""
        hierarchy1_nl = raw_values[2].strip() if len(raw_values) > 2 else ""
        hierarchy2_fr = raw_values[4].strip() if len(raw_values) > 4 else ""
        hierarchy2_nl = raw_values[5].strip() if len(raw_values) > 5 else ""
        hierarchy3_fr = raw_values[7].strip() if len(raw_values) > 7 else ""
        hierarchy3_nl = raw_values[8].strip() if len(raw_values) > 8 else ""
        
        sort_key1 = raw_values[0].strip() if len(raw_values) > 0 else "0"
        sort_key2 = raw_values[3].strip() if len(raw_values) > 3 else "0"
        sort_key3 = raw_values[6].strip() if len(raw_values) > 6 else "0"
        
        # Créer les catégories de niveau 1
        if hierarchy1_fr and hierarchy1_nl:
            cat1_key = f"1_{hierarchy1_fr}"
            if cat1_key not in categories_dict:
                categories_dict[cat1_key] = {
                    "Id": str(uuid.uuid4()),
                    "Level": 1,
                    "Name_FR": hierarchy1_fr,
                    "Name_NL": hierarchy1_nl,
                    "Name_EN": hierarchy1_fr,
                    "ParentId": None,
                    "SortOrder": int(sort_key1) if sort_key1.isdigit() else 0
                }
                category_hierarchy[cat1_key] = {}
            cat1_id = categories_dict[cat1_key]["Id"]
            
            # Créer les catégories de niveau 2
            if hierarchy2_fr and hierarchy2_nl:
                cat2_key = f"2_{hierarchy2_fr}"
                if cat2_key not in categories_dict:
                    categories_dict[cat2_key] = {
                        "Id": str(uuid.uuid4()),
                        "Level": 2,
                        "Name_FR": hierarchy2_fr,
                        "Name_NL": hierarchy2_nl,
                        "Name_EN": hierarchy2_fr,
                        "ParentId": cat1_id,
                        "SortOrder": int(sort_key2) if sort_key2.isdigit() else 0
                    }
                    category_hierarchy[cat1_key][cat2_key] = {}
                cat2_id = categories_dict[cat2_key]["Id"]
                
                # Créer les catégories de niveau 3
                if hierarchy3_fr and hierarchy3_nl:
                    cat3_key = f"3_{hierarchy3_fr}"
                    if cat3_key not in categories_dict:
                        categories_dict[cat3_key] = {
                            "Id": str(uuid.uuid4()),
                            "Level": 3,
                            "Name_FR": hierarchy3_fr,
                            "Name_NL": hierarchy3_nl,
                            "Name_EN": hierarchy3_fr,
                            "ParentId": cat2_id,
                            "SortOrder": int(sort_key3) if sort_key3.isdigit() else 0
                        }
                    cat3_id = categories_dict[cat3_key]["Id"]
                    category_id = cat3_id  # Utiliser le niveau 3 comme catégorie du produit
                else:
                    category_id = cat2_id  # Utiliser le niveau 2 si pas de niveau 3
            else:
                category_id = cat1_id  # Utiliser le niveau 1 si pas de niveau 2
        else:
            category_id = None
        
        # Extraire les données principales
        product_key = row.get('Product_Key_mdg_pdm_gpdm', '').strip()
        material_number = row.get('Material_Number_sap_pdm_gpdm', '').strip()
        ean = row.get('Ean_Number_sap_pdm_gpdm', '').strip()
        
        if not product_key or not material_number:
            continue
        
        # Récupérer les valeurs brutes pour accéder aux colonnes sans nom
        raw_values = row.get('_raw_values', [])
        
        # Noms produits (FR et NL) - colonnes séparées
        # La colonne Product_Name_desc_pdm_gpdm contient le nom FR (index 11)
        # La colonne suivante (index 12, vide dans les headers) contient le nom NL
        name_fr = row.get('Product_Name_desc_pdm_gpdm', '').strip()
        
        # Pour le nom NL, utiliser l'index directement depuis les valeurs brutes
        try:
            # Product_Name_desc_pdm_gpdm est à l'index 11, NL est à l'index 12
            if len(raw_values) > 12:
                name_nl = raw_values[12].strip()
                if not name_nl:
                    name_nl = name_fr
            else:
                name_nl = name_fr
        except (IndexError, AttributeError):
            name_nl = name_fr
        
        # Descriptions courtes (FR et NL) - colonnes séparées
        # Product_Short_Description_desc_pdm_gpdm est à l'index 13 (FR)
        # La colonne suivante (index 14) contient la description NL
        short_desc_fr = row.get('Product_Short_Description_desc_pdm_gpdm', '').strip()
        try:
            if len(raw_values) > 14:
                short_desc_nl = raw_values[14].strip()
                if not short_desc_nl:
                    short_desc_nl = short_desc_fr
            else:
                short_desc_nl = short_desc_fr
        except (IndexError, AttributeError):
            short_desc_nl = short_desc_fr
        
        # Descriptions longues (utiliser Variant_Specification si disponible)
        # Variant_Specification_desc_pdm_gpdm est vers la fin du fichier
        # On doit trouver son index dans column_names
        variant_spec_fr = row.get('Variant_Specification_desc_pdm_gpdm', '').strip()
        variant_spec_nl = variant_spec_fr
        try:
            # Trouver l'index de Variant_Specification_desc_pdm_gpdm dans column_names
            variant_spec_idx = column_names.index('Variant_Specification_desc_pdm_gpdm')
            if variant_spec_idx + 1 < len(raw_values):
                variant_spec_nl = raw_values[variant_spec_idx + 1].strip()
                if not variant_spec_nl:
                    variant_spec_nl = variant_spec_fr
        except (ValueError, IndexError, AttributeError):
            pass
        
        desc_fr = variant_spec_fr or short_desc_fr
        desc_nl = variant_spec_nl or short_desc_nl
        
        # Dimensions
        length_sap = parse_dimension(row.get('Length_met_sap_pdm_gpdm', ''))
        length_epim = parse_dimension(row.get('Length_met_td_pdm_gpdm', ''))
        length_custom = parse_dimension(row.get('Length_custom_desc_td_pdm_gpdm', ''))
        length = length_sap or length_epim or length_custom
        
        width_sap = parse_dimension(row.get('Width_met_sap_pdm_gpdm', ''))
        width_epim = parse_dimension(row.get('Width_met_td_pdm_gpdm', ''))
        width_custom = parse_dimension(row.get('Width_custom_desc_td_pdm_gpdm', ''))
        width = width_sap or width_epim or width_custom
        
        height_sap = parse_dimension(row.get('Height_met_sap_pdm_gpdm', ''))
        height_epim = parse_dimension(row.get('Height_met_td_pdm_gpdm', ''))
        height_custom = parse_dimension(row.get('Height_custom_desc_td_pdm_gpdm', ''))
        height = height_sap or height_epim or height_custom
        
        # Poids
        weight_sap = parse_weight(row.get('Net_Weight_met_sap_pdm_gpdm', ''))
        weight_epim = parse_weight(row.get('Net_Weight_met_td_pdm_gpdm', ''))
        weight_custom = parse_weight(row.get('Net_Weight_custom_td_pdm_gpdm', ''))
        weight = weight_sap or weight_epim or weight_custom
        
        # Quantité minimale de commande
        min_order_qty = row.get('Minimum_Order_Quantity_met_pdm_gpdm', '').strip()
        try:
            min_order_qty = int(min_order_qty) if min_order_qty else 1
        except:
            min_order_qty = 1
        
        # Si le produit n'existe pas encore, le créer
        if product_key not in products_dict:
            product_id = str(uuid.uuid4())
            products_dict[product_key] = {
                "Id": product_id,
                "Reference": product_key,
                "Sku": product_key,
                "Name_FR": name_fr,
                "Name_NL": name_nl,
                "Name_EN": name_fr,  # Utiliser FR comme EN par défaut
                "ShortDescription_FR": short_desc_fr,
                "ShortDescription_NL": short_desc_nl,
                "ShortDescription_EN": short_desc_fr,
                "Description_FR": desc_fr,
                "Description_NL": desc_nl,
                "Description_EN": desc_fr,
                "MinOrderQuantity": min_order_qty,
                "CategoryId": category_id,  # Associer la catégorie au produit
                "variants": [],
                "_variant_skus": set()  # Pour tracker les SKU déjà ajoutés
            }
        else:
            product_id = products_dict[product_key]["Id"]
            # Mettre à jour la catégorie si elle n'était pas définie
            if not products_dict[product_key].get("CategoryId") and category_id:
                products_dict[product_key]["CategoryId"] = category_id
        
        # Créer la variante (vérifier d'abord si elle existe déjà pour ce produit)
        # Utiliser un set pour tracker les SKU déjà ajoutés pour ce produit
        variant_skus = products_dict[product_key].get("_variant_skus", set())
        
        # Si ce SKU existe déjà pour ce produit, ignorer cette ligne (doublon)
        if material_number in variant_skus:
            continue  # Ignorer les doublons de SKU pour le même produit
        
        # Marquer ce SKU comme utilisé
        variant_skus.add(material_number)
        products_dict[product_key]["_variant_skus"] = variant_skus
        
        # Créer la variante
        variant_id = str(uuid.uuid4())
        variant = {
            "Id": variant_id,
            "ProductId": product_id,
            "Sku": material_number,
            "Barcode": ean if ean else None,
            "Length": length,
            "Width": width,
            "Height": height,
            "Weight": weight,
            "IsActive": True
        }
        
        products_dict[product_key]["variants"].append(variant)
        variants_list.append(variant)
    
    # Nettoyer les données temporaires avant de retourner
    for product in products_dict.values():
        product.pop("_variant_skus", None)
    
    # Convertir en liste de produits
    products_list = list(products_dict.values())
    
    # Convertir en liste de catégories
    categories_list = list(categories_dict.values())
    
    return {
        "products": products_list,
        "variants": variants_list,
        "categories": categories_list
    }


def generate_sql_script(catalog_data: Dict, output_path: str):
    """Génère le script SQL depuis les données du catalogue"""
    products = catalog_data.get("products", [])
    variants = catalog_data.get("variants", [])
    categories = catalog_data.get("categories", [])
    
    sql_lines = []
    sql_lines.append("-- Script SQL généré automatiquement depuis le catalogue Knauf CSV")
    sql_lines.append("-- Tables: ErpCategories, ErpProducts, ErpProductVariants")
    sql_lines.append("")
    sql_lines.append("BEGIN TRANSACTION;")
    sql_lines.append("")
    
    # INSERT ErpCategories (avant les produits)
    if categories:
        sql_lines.append("-- ========================================")
        sql_lines.append("-- INSERT INTO ErpCategories")
        sql_lines.append("-- ========================================")
        sql_lines.append("")
        
        # Trier les catégories par niveau (1, 2, 3) pour créer d'abord les parents
        categories_sorted = sorted(categories, key=lambda x: x.get("Level", 0))
        
        for category in categories_sorted:
            columns = []
            values = []
            
            # Id
            columns.append("[Id]")
            values.append(escape_sql(category.get("Id")))
            
            # CompanyId
            columns.append("[CompanyId]")
            values.append(escape_sql(COMPANY_ID))
            
            # Noms multilingues
            for lang in ["NL", "FR", "EN"]:
                name_key = f"Name_{lang}"
                name_value = category.get(name_key) or category.get("Name_FR") or "Category"
                columns.append(f"[Name_{lang}]")
                values.append(escape_sql(name_value))
            
            # Slugs
            for lang in ["NL", "FR", "EN"]:
                name_key = f"Name_{lang}"
                name_value = category.get(name_key) or category.get("Name_FR") or "Category"
                slug = generate_slug(name_value)
                columns.append(f"[Slug_{lang}]")
                values.append(escape_sql(slug))
            
            # ParentId
            parent_id = category.get("ParentId")
            if parent_id:
                columns.append("[ParentId]")
                values.append(escape_sql(parent_id))
            
            # SortOrder
            columns.append("[SortOrder]")
            values.append(str(category.get("SortOrder", 0)))
            
            # IsActive
            columns.append("[IsActive]")
            values.append("1")
            
            # CreatedBy, UpdatedBy, CreatedAt, UpdatedAt
            columns.append("[CreatedBy]")
            values.append(escape_sql(DEFAULT_USER_ID))
            columns.append("[UpdatedBy]")
            values.append(escape_sql(DEFAULT_USER_ID))
            columns.append("[CreatedAt]")
            values.append(escape_sql(SQL_DATETIMEOFFSET_FORMAT))
            columns.append("[UpdatedAt]")
            values.append("NULL")
            
            # Générer l'INSERT
            columns_str = ", ".join(columns)
            values_str = ", ".join(values)
            sql_lines.append(f"INSERT INTO [dbo].[ErpCategories] ({columns_str})")
            sql_lines.append(f"VALUES ({values_str});")
            sql_lines.append("")
        
        sql_lines.append("")
    
    # INSERT ErpProducts
    sql_lines.append("-- ========================================")
    sql_lines.append("-- INSERT INTO ErpProducts")
    sql_lines.append("-- ========================================")
    sql_lines.append("")
    
    for product in products:
        product_id = product.get("Id")
        ref = product.get("Reference") or product.get("Sku") or ""
        
        # Construire les colonnes et valeurs
        columns = []
        values = []
        
        # Id
        columns.append("[Id]")
        values.append(escape_sql(product_id))
        
        # CompanyId
        columns.append("[CompanyId]")
        values.append(escape_sql(COMPANY_ID))
        
        # Reference
        columns.append("[Reference]")
        values.append(escape_sql(ref))
        
        # Noms multilingues
        for lang in ["NL", "FR", "EN"]:
            name_key = f"Name_{lang}"
            name_value = product.get(name_key) or product.get("Name_FR") or f"Product {ref}"
            columns.append(f"[Name_{lang}]")
            values.append(escape_sql(name_value))
        
        # Slugs
        for lang in ["NL", "FR", "EN"]:
            name_key = f"Name_{lang}"
            name_value = product.get(name_key) or product.get("Name_FR") or f"Product {ref}"
            slug = generate_slug(name_value)
            columns.append(f"[Slug_{lang}]")
            values.append(escape_sql(slug))
        
        # ShortDescription
        for lang in ["NL", "FR", "EN"]:
            short_desc = product.get(f"ShortDescription_{lang}") or product.get(f"Description_{lang}") or product.get(f"Name_{lang}") or ""
            columns.append(f"[ShortDescription_{lang}]")
            values.append(escape_sql(short_desc))
        
        # Description
        for lang in ["NL", "FR", "EN"]:
            desc = product.get(f"Description_{lang}") or product.get(f"ShortDescription_{lang}") or product.get(f"Name_{lang}") or ""
            columns.append(f"[Description_{lang}]")
            values.append(escape_sql(desc))
        
        # MetaTitle
        for lang in ["NL", "FR", "EN"]:
            meta_title = product.get(f"Name_{lang}") or product.get("Name_FR") or f"Product {ref}"
            columns.append(f"[MetaTitle_{lang}]")
            values.append(escape_sql(meta_title))
        
        # MetaDescription
        for lang in ["NL", "FR", "EN"]:
            meta_desc = product.get(f"ShortDescription_{lang}") or product.get(f"Description_{lang}") or product.get(f"Name_{lang}") or ""
            columns.append(f"[MetaDescription_{lang}]")
            values.append(escape_sql(meta_desc))
        
        # CostPrice, SellingPrice
        columns.append("[CostPrice]")
        values.append("0.0")
        columns.append("[SellingPrice]")
        values.append("0.0")
        
        # StockQuantity
        columns.append("[StockQuantity]")
        values.append("0")
        
        # MinOrderQuantity
        min_order = product.get("MinOrderQuantity", 1)
        columns.append("[MinOrderQuantity]")
        values.append(str(min_order))
        
        # Barcode, Sku, Gtin (utiliser la référence du produit)
        columns.append("[Barcode]")
        values.append(escape_sql(ref))
        columns.append("[Sku]")
        values.append(escape_sql(ref))
        columns.append("[Gtin]")
        values.append(escape_sql(ref))
        
        # LengthCm (utiliser la première variante si disponible)
        length_cm = None
        if product.get("variants"):
            length_cm = product["variants"][0].get("Length")
        columns.append("[LengthCm]")
        values.append(escape_sql(length_cm))
        
        # SpecificationsJson
        columns.append("[SpecificationsJson]")
        values.append("'{}'")
        
        # CategoryId - NULLABLE (utiliser la catégorie extraite)
        category_id = product.get("CategoryId")
        if category_id:
            columns.append("[CategoryId]")
            values.append(escape_sql(category_id))
        
        # Availability, Visibility, IsActive, IsPublished
        columns.append("[Availability]")
        values.append("'InStock'")
        columns.append("[Visibility]")
        values.append("'Visible'")
        columns.append("[IsActive]")
        values.append("1")
        columns.append("[IsPublished]")
        values.append("1")
        
        # CreatedBy, UpdatedBy, CreatedAt, UpdatedAt
        columns.append("[CreatedBy]")
        values.append(escape_sql(DEFAULT_USER_ID))
        columns.append("[UpdatedBy]")
        values.append(escape_sql(DEFAULT_USER_ID))
        columns.append("[CreatedAt]")
        values.append(escape_sql(SQL_DATETIMEOFFSET_FORMAT))
        columns.append("[UpdatedAt]")
        values.append("NULL")
        
        # Générer l'INSERT
        columns_str = ", ".join(columns)
        values_str = ", ".join(values)
        sql_lines.append(f"INSERT INTO [dbo].[ErpProducts] ({columns_str})")
        sql_lines.append(f"VALUES ({values_str});")
        sql_lines.append("")
    
    # INSERT ErpProductVariants
    sql_lines.append("-- ========================================")
    sql_lines.append("-- INSERT INTO ErpProductVariants")
    sql_lines.append("-- ========================================")
    sql_lines.append("")
    
    for variant in variants:
        columns = []
        values = []
        
        # Id
        columns.append("[Id]")
        values.append(escape_sql(variant.get("Id")))
        
        # ProductId
        columns.append("[ProductId]")
        values.append(escape_sql(variant.get("ProductId")))
        
        # Sku
        columns.append("[Sku]")
        values.append(escape_sql(variant.get("Sku")))
        
        # Barcode
        columns.append("[Barcode]")
        barcode_value = variant.get("Barcode")
        # Si le code-barres est None ou vide, utiliser '0' car le champ est non-nullable
        if barcode_value is None or barcode_value == "":
            values.append("'0'")
        else:
            values.append(escape_sql(barcode_value))
        
        # StockQuantity
        columns.append("[StockQuantity]")
        values.append("0")
        
        # AttributesJson
        columns.append("[AttributesJson]")
        values.append("'{}'")
        
        # Dimensions
        if variant.get("Length") is not None:
            columns.append("[Length]")
            values.append(escape_sql(variant.get("Length")))
        if variant.get("Width") is not None:
            columns.append("[Width]")
            values.append(escape_sql(variant.get("Width")))
        if variant.get("Height") is not None:
            columns.append("[Height]")
            values.append(escape_sql(variant.get("Height")))
        if variant.get("Weight") is not None:
            columns.append("[Weight]")
            values.append(escape_sql(variant.get("Weight")))
        
        # IsActive
        columns.append("[IsActive]")
        values.append("1" if variant.get("IsActive", True) else "0")
        
        # CreatedBy, UpdatedBy, CreatedAt, UpdatedAt
        columns.append("[CreatedBy]")
        values.append(escape_sql(DEFAULT_USER_ID))
        columns.append("[UpdatedBy]")
        values.append(escape_sql(DEFAULT_USER_ID))
        columns.append("[CreatedAt]")
        values.append(escape_sql(SQL_DATETIMEOFFSET_FORMAT))
        columns.append("[UpdatedAt]")
        values.append(escape_sql(SQL_DATETIMEOFFSET_FORMAT))
        
        # Générer l'INSERT
        columns_str = ", ".join(columns)
        values_str = ", ".join(values)
        sql_lines.append(f"INSERT INTO [dbo].[ErpProductVariants] ({columns_str})")
        sql_lines.append(f"VALUES ({values_str});")
        sql_lines.append("")
    
    sql_lines.append("COMMIT TRANSACTION;")
    
    # Écrire le fichier
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write("\n".join(sql_lines))
    
    print(f"Script SQL généré avec succès: {output_path}")
    print(f"  - {len(categories)} catégories")
    print(f"  - {len(products)} produits")
    print(f"  - {len(variants)} variantes")


if __name__ == "__main__":
    import sys
    import os
    
    # Chemin du CSV (chercher dans le répertoire parent si nécessaire)
    csv_path = "Product Catalogus - Catalogue produits 2025.csv"
    if len(sys.argv) > 1:
        csv_path = sys.argv[1]
    else:
        # Chercher dans le répertoire parent
        parent_csv = os.path.join("..", "..", "Product Catalogus - Catalogue produits 2025.csv")
        if os.path.exists(parent_csv):
            csv_path = parent_csv
    
    if not os.path.exists(csv_path):
        print(f"ERREUR: Fichier CSV introuvable: {csv_path}")
        sys.exit(1)
    
    # Chemin de sortie SQL
    output_path = "knauf_products.sql"
    if len(sys.argv) > 2:
        output_path = sys.argv[2]
    
    # Parser le CSV
    print(f"Parsing du CSV: {csv_path}")
    catalog_data = parse_csv_to_products(csv_path)
    
    # Générer le SQL
    print(f"Génération du script SQL: {output_path}")
    generate_sql_script(catalog_data, output_path)
