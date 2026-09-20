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
-- 4. Events - created, edited and deleted by Organisers; viewed by both roles
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Events
(
    EventId       INT             IDENTITY(1,1) NOT NULL,
    OrganiserId   INT             NOT NULL,
    EventName     NVARCHAR(100)   NOT NULL,
    Description   NVARCHAR(500)   NOT NULL,
    EventDate     DATE            NOT NULL,
    Location      NVARCHAR(100)   NOT NULL,
    DistanceKm    DECIMAL(6,2)    NOT NULL,
    EventType     NVARCHAR(10)    NOT NULL,
    CONSTRAINT PK_Events PRIMARY KEY CLUSTERED (EventId),
    CONSTRAINT UQ_Events_Name_Date_Location UNIQUE (EventName, EventDate, Location),
    CONSTRAINT CK_Events_DistanceKm CHECK (DistanceKm > 0),
    CONSTRAINT CK_Events_EventType CHECK (EventType IN (N'Run', N'Walk', N'Cycle')),
    CONSTRAINT CK_Events_EventDate CHECK (EventDate >= CAST(N'2020-01-01' AS DATE)),
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES dbo.Users (UserId)
);
GO

-- ---------------------------------------------------------------------------------------------
-- 5. Routes - live route information used by Participants on race day (one route per event)
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Routes
(
    RouteId         INT             IDENTITY(1,1) NOT NULL,
    EventId         INT             NOT NULL,
    StartPoint      NVARCHAR(100)   NOT NULL,
    EndPoint        NVARCHAR(100)   NOT NULL,
    ElevationGainM  INT             NOT NULL CONSTRAINT DF_Routes_ElevationGainM DEFAULT (0),
    GpxTrackUrl     NVARCHAR(255)   NULL,
    CONSTRAINT PK_Routes PRIMARY KEY CLUSTERED (RouteId),
    CONSTRAINT UQ_Routes_EventId UNIQUE (EventId),
    CONSTRAINT CK_Routes_ElevationGainM CHECK (ElevationGainM >= 0),
    CONSTRAINT FK_Routes_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId)
);
GO

-- ---------------------------------------------------------------------------------------------
-- 6. EventCategories - age or distance categories defined by Organisers per event
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.EventCategories
(
    CategoryId    INT           IDENTITY(1,1) NOT NULL,
    EventId       INT           NOT NULL,
    CategoryName  NVARCHAR(50)  NOT NULL,
    MinAge        INT           NULL,
    MaxAge        INT           NULL,
    CONSTRAINT PK_EventCategories PRIMARY KEY CLUSTERED (CategoryId),
    CONSTRAINT UQ_EventCategories_Event_Name UNIQUE (EventId, CategoryName),
    CONSTRAINT CK_EventCategories_MinAge CHECK (MinAge IS NULL OR MinAge >= 0),
    CONSTRAINT CK_EventCategories_MaxAge CHECK (MaxAge IS NULL OR MaxAge >= 0),
    CONSTRAINT CK_EventCategories_AgeRange CHECK (MinAge IS NULL OR MaxAge IS NULL OR MinAge <= MaxAge),
    CONSTRAINT FK_EventCategories_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId)
);
GO

-- ---------------------------------------------------------------------------------------------
-- 7. Enrolments - links a Participant to an Event and the Category they selected
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Enrolments
(
    EnrolmentId    INT           IDENTITY(1,1) NOT NULL,
    EventId        INT           NOT NULL,
    UserId         INT           NOT NULL,
    CategoryId     INT           NOT NULL,
    EnrolmentDate  DATETIME2(0)  NOT NULL CONSTRAINT DF_Enrolments_EnrolmentDate DEFAULT (SYSUTCDATETIME()),
    Status         NVARCHAR(20)  NOT NULL CONSTRAINT DF_Enrolments_Status DEFAULT (N'Confirmed'),
    CONSTRAINT PK_Enrolments PRIMARY KEY CLUSTERED (EnrolmentId),
    CONSTRAINT UQ_Enrolments_Event_User UNIQUE (EventId, UserId),
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN (N'Confirmed', N'Cancelled')),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId),
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.EventCategories (CategoryId)
);
GO

-- ---------------------------------------------------------------------------------------------
