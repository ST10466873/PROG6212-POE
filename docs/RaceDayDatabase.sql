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
-- 8. Results - finish time and finishing position captured by Organisers after an event
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Results
(
    ResultId          INT           IDENTITY(1,1) NOT NULL,
    EnrolmentId       INT           NOT NULL,
    EventId           INT           NOT NULL,
    FinishTime        TIME(3)       NOT NULL,
    FinishingPosition INT           NOT NULL,
    CapturedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_Results_CapturedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_Results PRIMARY KEY CLUSTERED (ResultId),
    CONSTRAINT UQ_Results_EnrolmentId UNIQUE (EnrolmentId),
    CONSTRAINT UQ_Results_Event_Position UNIQUE (EventId, FinishingPosition),
    CONSTRAINT CK_Results_FinishingPosition CHECK (FinishingPosition > 0),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments (EnrolmentId),
    CONSTRAINT FK_Results_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId)
);
GO

-- ---------------------------------------------------------------------------------------------
-- 9. Sessions - server-side session state used for role-based authentication (Part 2)
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Sessions
(
    SessionId    INT            IDENTITY(1,1) NOT NULL,
    UserId       INT            NOT NULL,
    SessionToken NVARCHAR(64)   NOT NULL,
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_Sessions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ExpiresAt    DATETIME2(0)   NOT NULL,
    IsActive     BIT            NOT NULL CONSTRAINT DF_Sessions_IsActive DEFAULT (1),
    CONSTRAINT PK_Sessions PRIMARY KEY CLUSTERED (SessionId),
    CONSTRAINT UQ_Sessions_SessionToken UNIQUE (SessionToken),
    CONSTRAINT CK_Sessions_ExpiresAt CHECK (ExpiresAt > CreatedAt),
    CONSTRAINT FK_Sessions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
);
GO

-- ---------------------------------------------------------------------------------------------
-- 10. Supporting indexes for common Part 2 queries
-- ---------------------------------------------------------------------------------------------
CREATE NONCLUSTERED INDEX IX_Users_RoleId        ON dbo.Users (RoleId);
CREATE NONCLUSTERED INDEX IX_Events_EventDate    ON dbo.Events (EventDate);
CREATE NONCLUSTERED INDEX IX_Events_OrganiserId  ON dbo.Events (OrganiserId);
CREATE NONCLUSTERED INDEX IX_Enrolments_UserId   ON dbo.Enrolments (UserId);
CREATE NONCLUSTERED INDEX IX_Enrolments_Category ON dbo.Enrolments (CategoryId);
CREATE NONCLUSTERED INDEX IX_Sessions_UserId     ON dbo.Sessions (UserId) WHERE IsActive = 1;
GO

/* =====================================================================================
   SEED DATA
   - 2 Organisers, 4 Participants
   - 3 Events (one per event type: Run, Walk, Cycle) with a route each
   - Categories for every event
   - Sample enrolments, and results for the two events already completed
   ===================================================================================== */

-- Roles ---------------------------------------------------------------------------------------------
SET IDENTITY_INSERT dbo.Roles ON;
INSERT INTO dbo.Roles (RoleId, RoleName) VALUES
    (1, N'Organiser'),
    (2, N'Participant');
SET IDENTITY_INSERT dbo.Roles OFF;
GO

-- Users --------------------------------------------------------------------------------------------
-- PasswordHash values are placeholder hashes (Part 2 replaces these with BCrypt hashes).
SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, RoleId, FirstName, LastName, Email, PasswordHash, PhoneNumber, DateOfBirth, IsActive, CreatedAt) VALUES
    (1, 1, N'Thabo',   N'Mokoena', N'thabo.mokoena@raceday.co.za',   N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000000', N'082 555 0141', N'1985-03-14', 1, N'2026-06-01 08:00:00'),
    (2, 1, N'Ayesha',  N'Patel',   N'ayesha.patel@raceday.co.za',    N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000001', N'083 555 0198', N'1990-07-22', 1, N'2026-06-03 10:15:00'),
    (3, 2, N'Sipho',   N'Nkosi',   N'sipho.nkosi@gmail.com',         N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000002', N'071 555 0173', N'1999-11-02', 1, N'2026-06-10 17:40:00'),
    (4, 2, N'Lerato',  N'Khumalo', N'lerato.khumalo@gmail.com',      N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000003', N'072 555 0119', N'2003-05-19', 1, N'2026-06-11 09:25:00'),
    (5, 2, N'Naledi',  N'Sithole', N'naledi.sithole@outlook.com',    N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000004', N'074 555 0166', N'2005-01-30', 1, N'2026-06-12 12:05:00'),
    (6, 2, N'Riaan',   N'van Wyk', N'riaan.vanwyk@webmail.co.za',    N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000005', N'076 555 0127', N'1992-09-08', 1, N'2026-06-14 19:30:00');
SET IDENTITY_INSERT dbo.Users OFF;
GO

-- Events --------------------------------------------------------------------------------------------
SET IDENTITY_INSERT dbo.Events ON;
INSERT INTO dbo.Events (EventId, OrganiserId, EventName, Description, EventDate, Location, DistanceKm, EventType) VALUES
    (1, 1, N'Durban Beachfront 10K',       N'A flat and fast 10km along the Durban promenade, ideal for personal bests.', N'2026-08-15', N'Durban Beachfront, KwaZulu-Natal', 10.00, N'Run'),
    (2, 2, N'Freedom Day Charity Walk',    N'A family-friendly 5km charity walk raising funds for local schools.',        N'2026-04-26', N'Freedom Park, Pretoria',            5.00,  N'Walk'),
    (3, 1, N'Table Bay Cycle Challenge',   N'A scenic 40km cycling route around Table Bay with two category climbs.',      N'2026-11-07', N'Cape Town Waterfront, Western Cape', 40.00, N'Cycle');
SET IDENTITY_INSERT dbo.Events OFF;
GO

-- Routes --------------------------------------------------------------------------------------------
SET IDENTITY_INSERT dbo.Routes ON;
INSERT INTO dbo.Routes (RouteId, EventId, StartPoint, EndPoint, ElevationGainM, GpxTrackUrl) VALUES
    (1, 1, N'uShaka Beach Car Park',   N'uShaka Beach Car Park',   15,  N'https://raceday.co.za/gpx/durban-10k.gpx'),
    (2, 2, N'Freedom Park Entrance',   N'Freedom Park Entrance',   20,  N'https://raceday.co.za/gpx/freedom-walk.gpx'),
    (3, 3, N'V&A Waterfront Clock',    N'V&A Waterfront Clock',    320, N'https://raceday.co.za/gpx/table-bay-40k.gpx');
SET IDENTITY_INSERT dbo.Routes OFF;
GO

-- Event categories ---------------------------------------------------------------------------------
SET IDENTITY_INSERT dbo.EventCategories ON;
INSERT INTO dbo.EventCategories (CategoryId, EventId, CategoryName, MinAge, MaxAge) VALUES
    (1, 1, N'Under 20',   NULL, 19),
    (2, 1, N'Senior',     20,   NULL),
    (3, 1, N'10km Open',  NULL, NULL),
    (4, 2, N'Under 20',   NULL, 19),
    (5, 2, N'Senior',     20,   NULL),
    (6, 2, N'5km Family', NULL, NULL),
    (7, 3, N'Under 20',   NULL, 19),
    (8, 3, N'Senior',     20,   NULL),
    (9, 3, N'40km Elite', NULL, NULL);
SET IDENTITY_INSERT dbo.EventCategories OFF;
GO

