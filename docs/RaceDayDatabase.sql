/* =====================================================================================
   RaceDay - Full Database Schema and Seed Data
   Module      : PROG6212 - Programming 2B
   Part        : Part 1 - System Planning and Database (Section C)
   Platform    : SQL Server (SQL Server Management Studio - SSMS)
   Notes       :
     - This script must run without errors on a clean SQL Server instance.
     - The schema matches docs/RaceDay_ERD.png exactly.
     - PasswordHash stores a hash only; passwords are never stored in plain text.
       Part 2 hashes passwords with BCrypt before persistence.
   ===================================================================================== */

-- ---------------------------------------------------------------------------------------------
-- 0. Create and select the database
-- ---------------------------------------------------------------------------------------------
IF DB_ID(N'RaceDay') IS NULL
BEGIN
    CREATE DATABASE RaceDay;
END
GO

USE RaceDay;
GO

-- ---------------------------------------------------------------------------------------------
-- 1. Drop tables in reverse dependency order so the script is re-runnable
-- ---------------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Results',     N'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID(N'dbo.Enrolments',  N'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID(N'dbo.Sessions',    N'U') IS NOT NULL DROP TABLE dbo.Sessions;
IF OBJECT_ID(N'dbo.EventCategories', N'U') IS NOT NULL DROP TABLE dbo.EventCategories;
IF OBJECT_ID(N'dbo.Routes',      N'U') IS NOT NULL DROP TABLE dbo.Routes;
IF OBJECT_ID(N'dbo.Events',      N'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID(N'dbo.Users',       N'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID(N'dbo.Roles',       N'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

-- ---------------------------------------------------------------------------------------------
-- 2. Roles - the two distinct user roles of the system
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Roles
(
    RoleId      INT           IDENTITY(1,1) NOT NULL,
    RoleName    NVARCHAR(20)  NOT NULL,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId),
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName),
    CONSTRAINT CK_Roles_RoleName CHECK (RoleName IN (N'Organiser', N'Participant'))
);
GO

-- ---------------------------------------------------------------------------------------------
-- 3. Users - every account in the system (Organisers and Participants share one table)
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Users
(
    UserId        INT            IDENTITY(1,1) NOT NULL,
    RoleId        INT            NOT NULL,
    FirstName     NVARCHAR(50)   NOT NULL,
    LastName      NVARCHAR(50)   NOT NULL,
    Email         NVARCHAR(100)  NOT NULL,
    PasswordHash  NVARCHAR(255)  NOT NULL,
    PhoneNumber   NVARCHAR(20)   NULL,
    DateOfBirth   DATE           NOT NULL,
    IsActive      BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAt     DATETIME2(0)   NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Email CHECK (Email LIKE N'%_@_%.__%'),
    CONSTRAINT CK_Users_DateOfBirth CHECK (DateOfBirth < SYSUTCDATETIME()),
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)
);
GO

-- ---------------------------------------------------------------------------------------------
