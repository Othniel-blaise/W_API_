-- ============================================================
-- Script : création de la table Sys_IngestionLog
-- Base   : AQManager_Logs  (base séparée, PAS AQManagerData)
-- Exécuter une seule fois en tant qu'administrateur SQL Server
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = N'AQManager_Logs')
BEGIN
    CREATE DATABASE [AQManager_Logs];
    PRINT 'Base AQManager_Logs créée.';
END
GO

USE [AQManager_Logs];
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[Sys_IngestionLog]')
      AND type = 'U'
)
BEGIN
    CREATE TABLE [dbo].[Sys_IngestionLog] (
        [Id]             INT            IDENTITY(1,1)  NOT NULL,
        [BatchId]        NVARCHAR(20)   NOT NULL,
        [FileName]       NVARCHAR(500)  NOT NULL,
        [NumeroExtrait]  NVARCHAR(100)  NULL,
        [TypeDetecte]    NVARCHAR(100)  NULL,
        [TableCible]     NVARCHAR(255)  NULL,
        [RecordId]       INT            NULL,
        [Statut]         NVARCHAR(20)   NOT NULL,   -- rattache | doublon | a_verifier | erreur
        [Message]        NVARCHAR(MAX)  NULL,
        [ProcessedAt]    DATETIME       NOT NULL  DEFAULT GETDATE(),
        CONSTRAINT [PK_Sys_IngestionLog] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_IngestionLog_BatchId]
        ON [dbo].[Sys_IngestionLog] ([BatchId]);

    CREATE NONCLUSTERED INDEX [IX_IngestionLog_Statut]
        ON [dbo].[Sys_IngestionLog] ([Statut]);

    PRINT 'Table Sys_IngestionLog créée.';
END
ELSE
BEGIN
    PRINT 'Table Sys_IngestionLog existe déjà — aucune modification.';
END
GO

-- Vue de synthèse par lot
IF NOT EXISTS (SELECT 1 FROM sys.views WHERE name = 'v_IngestionSummary')
EXEC ('
CREATE VIEW [dbo].[v_IngestionSummary] AS
SELECT
    BatchId,
    MIN(ProcessedAt)                                    AS DebutLot,
    MAX(ProcessedAt)                                    AS FinLot,
    COUNT(*)                                            AS Total,
    SUM(CASE WHEN Statut = ''rattache''   THEN 1 ELSE 0 END) AS Rattaches,
    SUM(CASE WHEN Statut = ''doublon''    THEN 1 ELSE 0 END) AS Doublons,
    SUM(CASE WHEN Statut = ''a_verifier'' THEN 1 ELSE 0 END) AS AVerifier,
    SUM(CASE WHEN Statut = ''erreur''     THEN 1 ELSE 0 END) AS Erreurs
FROM [dbo].[Sys_IngestionLog]
GROUP BY BatchId
');
PRINT 'Vue v_IngestionSummary créée.';
GO
