-- =============================================
-- Tamircim - İlk Veritabanı Şeması ve Seed Data
-- Beyaz Eşya / Telefon / Elektronik Tamircisi
-- İlk container oluşturulduğunda otomatik çalışır
-- =============================================

SET client_encoding = 'UTF8';

-- Türkçe karakter normalizasyonu
CREATE OR REPLACE FUNCTION turkish_lower(input text) RETURNS text AS $$
SELECT LOWER(
  REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
    input,
    chr(304), 'i'),
    chr(305), 'i'),
    chr(350), chr(351)),
    chr(286), chr(287)),
    chr(220), chr(252)),
    chr(214), chr(246)),
    chr(199), chr(231))
)
$$ LANGUAGE SQL IMMUTABLE STRICT;

-- =============================================
-- KULLANICILAR
-- =============================================
CREATE TABLE "Users" (
    "Id"           int4 GENERATED ALWAYS AS IDENTITY(START 1 INCREMENT 1) NOT NULL,
    "FirstName"    varchar(100) NOT NULL,
    "LastName"     varchar(100) NOT NULL,
    "Title"        varchar(200) NULL,
    "Email"        varchar(256) NOT NULL,
    "PasswordHash" text NOT NULL,
    "PasswordSalt" text NOT NULL,
    "IsActive"     bool DEFAULT true NOT NULL,
    "CreatedAt"    timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "UpdatedAt"    timestamp DEFAULT timezone('utc', now()) NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);
CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");

-- =============================================
-- REFRESH TOKENS
-- =============================================
CREATE TABLE "RefreshTokens" (
    "Id"               int4 GENERATED ALWAYS AS IDENTITY(START 1 INCREMENT 1) NOT NULL,
    "UserId"           int4 NOT NULL,
    "Token"            varchar(256) NOT NULL,
    "ExpiresAt"        timestamp NOT NULL,
    "CreatedAt"        timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "CreatedByIp"      varchar(45) NULL,
    "RevokedAt"        timestamp NULL,
    "RevokedByIp"      varchar(45) NULL,
    "ReplacedByToken"  varchar(256) NULL,
    "RevokeReason"     varchar(256) NULL,
    CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RefreshTokens_Users" FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX "IX_RefreshTokens_Token" ON "RefreshTokens" ("Token");
CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
CREATE INDEX "IX_RefreshTokens_ExpiresAt" ON "RefreshTokens" ("ExpiresAt");

-- =============================================
-- MÜŞTERİLER
-- =============================================
CREATE TABLE "Customers" (
    "Id"                int4 GENERATED ALWAYS AS IDENTITY(START 1 INCREMENT 1) NOT NULL,
    "FirstName"         varchar(100) NOT NULL,
    "LastName"          varchar(100) NOT NULL,
    "NationalId"        varchar(11) NULL,
    "Address"           text NULL,
    "Email"             varchar(256) NULL,
    "Phone1"            varchar(20) NOT NULL,
    "Phone2"            varchar(20) NULL,
    "Notes"             text NULL,
    "CreatedAt"         timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "UpdatedAt"         timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "CreatedByUserId"   int4 NULL,
    "UpdatedByUserId"   int4 NULL,
    "IsDeleted"         bool DEFAULT false NOT NULL,
    "DeletedAt"         timestamp NULL,
    "DeletedByUserId"   int4 NULL,
    CONSTRAINT "PK_Customers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Customers_CreatedBy" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Customers_UpdatedBy" FOREIGN KEY ("UpdatedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Customers_DeletedBy" FOREIGN KEY ("DeletedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL
);
CREATE INDEX "IX_Customers_Phone1" ON "Customers" ("Phone1");
CREATE INDEX "IX_Customers_NationalId" ON "Customers" ("NationalId") WHERE "NationalId" IS NOT NULL;
CREATE INDEX "IX_Customers_IsDeleted" ON "Customers" ("IsDeleted") WHERE "IsDeleted" = false;

-- =============================================
-- CİHAZLAR
-- DeviceType: 0=Beyaz Eşya, 1=Telefon, 2=Elektronik, 3=Diğer
-- =============================================
CREATE TABLE "Devices" (
    "Id"                int4 GENERATED ALWAYS AS IDENTITY(START 1 INCREMENT 1) NOT NULL,
    "CustomerId"        int4 NOT NULL,
    "DeviceType"        int4 NOT NULL DEFAULT 3,
    "Brand"             varchar(100) NOT NULL,
    "Model"             varchar(200) NOT NULL,
    "SerialNumber"      varchar(100) NULL,
    "FaultDescription"  text NOT NULL,
    "ReceivedAt"        timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "DeliveryDate"      timestamp NULL,
    "Notes"             text NULL,
    "CreatedAt"         timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "UpdatedAt"         timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "CreatedByUserId"   int4 NULL,
    "UpdatedByUserId"   int4 NULL,
    "IsDeleted"         bool DEFAULT false NOT NULL,
    "DeletedAt"         timestamp NULL,
    "DeletedByUserId"   int4 NULL,
    CONSTRAINT "PK_Devices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Devices_Customers" FOREIGN KEY ("CustomerId") REFERENCES "Customers"("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Devices_CreatedBy" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Devices_UpdatedBy" FOREIGN KEY ("UpdatedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Devices_DeletedBy" FOREIGN KEY ("DeletedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL
);
CREATE INDEX "IX_Devices_CustomerId" ON "Devices" ("CustomerId");
CREATE INDEX "IX_Devices_DeviceType" ON "Devices" ("DeviceType");
CREATE INDEX "IX_Devices_ReceivedAt" ON "Devices" ("ReceivedAt");
CREATE INDEX "IX_Devices_IsDeleted" ON "Devices" ("IsDeleted") WHERE "IsDeleted" = false;

-- =============================================
-- ARIZA KAYITLARI
-- Status: 0=Beklemede, 1=Onarıldı, 2=Onarılmadı
-- =============================================
CREATE TABLE "RepairRecords" (
    "Id"                  int4 GENERATED ALWAYS AS IDENTITY(START 1 INCREMENT 1) NOT NULL,
    "DeviceId"            int4 NOT NULL,
    "Status"              int4 NOT NULL DEFAULT 0,
    "WorkDone"            text NULL,
    "NotRepairedReason"   text NULL,
    "WaitingReason"       text NULL,
    "CompletedAt"         timestamp NULL,
    "Notes"               text NULL,
    "CreatedAt"           timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "UpdatedAt"           timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "CreatedByUserId"     int4 NULL,
    "UpdatedByUserId"     int4 NULL,
    "IsDeleted"           bool DEFAULT false NOT NULL,
    "DeletedAt"           timestamp NULL,
    "DeletedByUserId"     int4 NULL,
    CONSTRAINT "PK_RepairRecords" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RepairRecords_Devices" FOREIGN KEY ("DeviceId") REFERENCES "Devices"("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_RepairRecords_CreatedBy" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_RepairRecords_UpdatedBy" FOREIGN KEY ("UpdatedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_RepairRecords_DeletedBy" FOREIGN KEY ("DeletedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL
);
CREATE INDEX "IX_RepairRecords_DeviceId" ON "RepairRecords" ("DeviceId");
CREATE INDEX "IX_RepairRecords_Status" ON "RepairRecords" ("Status");
CREATE INDEX "IX_RepairRecords_CreatedAt" ON "RepairRecords" ("CreatedAt");
CREATE INDEX "IX_RepairRecords_IsDeleted" ON "RepairRecords" ("IsDeleted") WHERE "IsDeleted" = false;

-- =============================================
-- DENETİM LOGU
-- =============================================
CREATE TABLE "AuditLogs" (
    "Id"              int4 GENERATED ALWAYS AS IDENTITY(START 1 INCREMENT 1) NOT NULL,
    "EntityType"      varchar(100) NOT NULL,
    "EntityId"        int4 NOT NULL,
    "Action"          varchar(20) NOT NULL,
    "UserId"          int4 NULL,
    "Timestamp"       timestamp DEFAULT timezone('utc', now()) NOT NULL,
    "ChangedFields"   varchar(2000) NULL,
    "IsDeleted"       bool DEFAULT false NOT NULL,
    "DeletedAt"       timestamp NULL,
    "DeletedByUserId" int4 NULL,
    CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_AuditLogs_Users" FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_AuditLogs_DeletedBy" FOREIGN KEY ("DeletedByUserId") REFERENCES "Users"("Id") ON DELETE SET NULL
);
CREATE INDEX "IX_AuditLogs_EntityType" ON "AuditLogs" ("EntityType");
CREATE INDEX "IX_AuditLogs_Action" ON "AuditLogs" ("Action");
CREATE INDEX "IX_AuditLogs_UserId" ON "AuditLogs" ("UserId");
CREATE INDEX "IX_AuditLogs_Timestamp" ON "AuditLogs" ("Timestamp");
CREATE INDEX "IX_AuditLogs_EntityType_EntityId" ON "AuditLogs" ("EntityType", "EntityId");
CREATE INDEX "IX_AuditLogs_IsDeleted" ON "AuditLogs" ("IsDeleted") WHERE "IsDeleted" = false;

-- =============================================
-- SEED DATA: Varsayılan Admin Kullanıcı
-- Şifre: Admin123! (BCrypt hash)
-- Prodüksiyonda mutlaka değiştirin!
-- =============================================
INSERT INTO "Users" ("FirstName", "LastName", "Email", "PasswordHash", "PasswordSalt", "IsActive")
VALUES (
    'Admin',
    'Kullanıcı',
    'admin@tamircim.local',
    '$2a$11$rBnNhl5T3XDZKzxMDSnXfOYimhF5c7wVTR3hb5lqSMRvJF6cGb5iu',
    '$2a$11$rBnNhl5T3XDZKzxMDSnXfO',
    true
);
