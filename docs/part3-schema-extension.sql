-- =============================================================================
-- RaceDay — Part 3 schema extension
-- -----------------------------------------------------------------------------
-- Azure Blob Storage integration (Part 3) adds two nullable URL columns to the
-- Part 1 model. The original Part 1 script and ERD are preserved unchanged; this
-- addendum is the documented, deliberate difference between the original schema
-- and the final Code-First model. EF Core's EnsureCreated() creates the columns
-- automatically on a fresh database; run this script only on a database created
-- from docs/RaceDayDatabase.sql.
-- =============================================================================

USE RaceDay;
GO

IF COL_LENGTH('dbo.Events', 'BannerImageUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Events ADD BannerImageUrl NVARCHAR(500) NULL;
END;
GO

IF COL_LENGTH('dbo.Users', 'ProfilePictureUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD ProfilePictureUrl NVARCHAR(500) NULL;
END;
GO

-- Verification: both columns should be present after the script runs.
SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE (TABLE_NAME = 'Events'  AND COLUMN_NAME = 'BannerImageUrl')
   OR (TABLE_NAME = 'Users'   AND COLUMN_NAME = 'ProfilePictureUrl');
GO
