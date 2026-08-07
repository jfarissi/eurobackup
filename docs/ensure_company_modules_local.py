#!/usr/bin/env python3
"""Ensure CompanyModules table exists on local backupcontent DB."""
import mysql.connector

DDL = """
CREATE TABLE IF NOT EXISTS `CompanyModules` (
  `Id` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
  `CompanyId` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
  `ModuleCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
  `ModuleName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `ConfigJson` longtext CHARACTER SET utf8mb4 NULL,
  `ActivatedAt` datetime(6) NOT NULL,
  `ExpiresAt` datetime(6) NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `UpdatedAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_CompanyModules_Company_Module` (`CompanyId`, `ModuleCode`),
  KEY `IX_CompanyModules_CompanyId` (`CompanyId`),
  KEY `IX_CompanyModules_ModuleCode` (`ModuleCode`),
  CONSTRAINT `FK_CompanyModules_Companies_CompanyId`
    FOREIGN KEY (`CompanyId`) REFERENCES `Companies` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
"""

conn = mysql.connector.connect(
    host="localhost", port=3306, user="root", password="tata", database="backupcontent"
)
cur = conn.cursor()
cur.execute("SHOW TABLES LIKE 'CompanyModules'")
exists = cur.fetchone()
print("before:", "YES" if exists else "NO")
if not exists:
    cur.execute(DDL)
    print("created CompanyModules")

cur.execute(
    "SELECT Id, Name, EnableErpCatalogSync FROM Companies"
)
companies = cur.fetchall()
print("companies:", companies)

for cid, name, erp in companies:
    # core
    cur.execute(
        """
        INSERT IGNORE INTO CompanyModules
        (Id, CompanyId, ModuleCode, ModuleName, IsActive, ConfigJson, ActivatedAt, CreatedAt)
        VALUES (UUID(), %s, 'core', 'Core ERP', 1, NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
        """,
        (cid,),
    )
    if erp:
        cur.execute(
            """
            INSERT IGNORE INTO CompanyModules
            (Id, CompanyId, ModuleCode, ModuleName, IsActive, ConfigJson, ActivatedAt, CreatedAt)
            VALUES (UUID(), %s, 'erp_catalog_sync', 'Sync catalogue ERP', 1, NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
            """,
            (cid,),
        )
    # auto_parts if company name suggests pieces auto OR no erp sync
    if not erp:
        cur.execute(
            """
            INSERT IGNORE INTO CompanyModules
            (Id, CompanyId, ModuleCode, ModuleName, IsActive, ConfigJson, ActivatedAt, CreatedAt)
            VALUES (UUID(), %s, 'auto_parts', 'Pièces auto', 1, NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
            """,
            (cid,),
        )

cur.execute(
    """
    INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    VALUES ('20260807120000_AddCompanyModules', '9.0.0')
    """
)
conn.commit()
cur.execute("SELECT ModuleCode, CompanyId FROM CompanyModules")
print("modules:", cur.fetchall())
cur.close()
conn.close()
print("OK")
