/* =====================================================================================
   RaceDay - Full Database Schema and Seed Data
   -------------------------------------------------------------------------------------
   Module      : PROG6212 - Programming 2B
   Part        : Part 1 - System Planning and Database (Section C)
   Platform    : SQL Server (SQL Server Management Studio - SSMS)
   Design docs : docs/RaceDay_ERD.png (Section A) and docs/api-endpoint-plan.md (Section B)

   Notes:
     - This script must run without errors on a clean SQL Server instance. It is also
       re-runnable: section 1 drops any previously created RaceDay tables first.
     - The schema matches docs/RaceDay_ERD.png exactly (8 entities).
     - PasswordHash stores a hash only; passwords are never stored in plain text.
       Part 2 hashes passwords with BCrypt before persistence and the value stored
       here is a placeholder that Part 2 replaces with a real BCrypt hash.
     - Every table uses IDENTITY surrogate keys so that API endpoints in Part 2 can
       reference records by a stable integer id (e.g. /api/events/{id}).
     - GO is the SSMS batch separator (not part of T-SQL). It is required after
       CREATE DATABASE and is used between statements so SSMS compiles each batch
       cleanly; never remove the GO lines when editing this script.
     - Naming convention: PK_ / UQ_ / CK_ / FK_ / DF_ prefixes for primary keys,
       unique constraints, check constraints, foreign keys and defaults respectively,
       so every constraint is identifiable at a glance in SSMS Object Explorer.
   ===================================================================================== */

-- ---------------------------------------------------------------------------------------------
-- 0. Create and select the database
--     The IF DB_ID guard means the script can be executed repeatedly on the same instance
--     without failing when the RaceDay database already exists.
-- ---------------------------------------------------------------------------------------------
IF DB_ID(N'RaceDay') IS NULL            -- guard: skip creation if the database already exists
BEGIN
    CREATE DATABASE RaceDay;            -- creates the database on the connected SQL Server instance
END
GO                                      -- batch separator: CREATE DATABASE must be alone in its batch

USE RaceDay;                            -- switches context so every object below lands in RaceDay
GO                                      -- batch separator: USE must complete before CREATE TABLE runs

-- ---------------------------------------------------------------------------------------------
-- 1. Drop tables in reverse dependency order so the script is re-runnable
--     Children are dropped before parents (Results before Enrolments before Events ...) because
--     SQL Server refuses to drop a table that still has foreign keys pointing at it.
--     Each IF OBJECT_ID guard means "drop only if it exists", so a first-ever run on a clean
--     instance skips this section silently instead of erroring.
-- ---------------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Results',     N'U') IS NOT NULL DROP TABLE dbo.Results;        -- 1st: references Enrolments + Events
IF OBJECT_ID(N'dbo.Enrolments',  N'U') IS NOT NULL DROP TABLE dbo.Enrolments;     -- 2nd: references Events/Users/Categories
IF OBJECT_ID(N'dbo.Sessions',    N'U') IS NOT NULL DROP TABLE dbo.Sessions;       -- 3rd: references Users
IF OBJECT_ID(N'dbo.EventCategories', N'U') IS NOT NULL DROP TABLE dbo.EventCategories; -- 4th: references Events
IF OBJECT_ID(N'dbo.Routes',      N'U') IS NOT NULL DROP TABLE dbo.Routes;         -- 5th: references Events
IF OBJECT_ID(N'dbo.Events',      N'U') IS NOT NULL DROP TABLE dbo.Events;         -- 6th: referenced by five tables above
IF OBJECT_ID(N'dbo.Users',       N'U') IS NOT NULL DROP TABLE dbo.Users;          -- 7th: referenced by Events/Sessions/Enrolments
IF OBJECT_ID(N'dbo.Roles',       N'U') IS NOT NULL DROP TABLE dbo.Roles;          -- 8th: referenced by Users (parent of all)
GO

-- ---------------------------------------------------------------------------------------------
-- 2. Roles - the two distinct user roles of the system
--     Modelled as a lookup table rather than a bare CHECK constraint on Users so that:
--       (a) the assignment's "two distinct user roles" are explicit entities in the ERD, and
--       (b) Part 2 can resolve role names through a join when enforcing authorisation.
--     A CHECK constraint is still applied as defence-in-depth so only the two permitted
--     values can ever be stored, even if a row is inserted by hand in SSMS.
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Roles
(
    RoleId      INT           IDENTITY(1,1) NOT NULL,   -- surrogate PK, referenced by Users.RoleId
    RoleName    NVARCHAR(20)  NOT NULL,                 -- 'Organiser' or 'Participant'
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId),         -- clustered index: fast lookups by id
    CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName),             -- prevents duplicate role rows
    CONSTRAINT CK_Roles_RoleName CHECK (RoleName IN (N'Organiser', N'Participant'))  -- only the two brief roles
);
GO

-- ---------------------------------------------------------------------------------------------
-- 3. Users - every account in the system
--     Design decisions:
--       * Both roles share ONE table (discriminated by RoleId) instead of two separate
--         Organiser/Participant tables - a participant and an organiser are both people with
--         the same personal details, and profile endpoints in Part 2 are role-agnostic.
--       * Email is UNIQUE because it is the login credential used by /api/auth/login.
--       * PasswordHash is NOT NULL and never holds plain text (requirement from Part 2).
--       * DateOfBirth is required because organisers define age categories (Under 20, Senior)
--         and Part 2 validates a participant's age against the category limits.
--       * IsActive supports deactivating an account without deleting its history
--         (deleting a user would cascade into enrolments and results).
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Users
(
    UserId        INT            IDENTITY(1,1) NOT NULL,                      -- surrogate PK for all API routes
    RoleId        INT            NOT NULL,                                    -- FK -> Roles: organiser or participant
    FirstName     NVARCHAR(50)   NOT NULL,                                    -- profile + results display name
    LastName      NVARCHAR(50)   NOT NULL,                                    -- profile + results display name
    Email         NVARCHAR(100)  NOT NULL,                                    -- login credential, unique per account
    PasswordHash  NVARCHAR(255)  NOT NULL,                                    -- BCrypt-style hash only, never plain text
    PhoneNumber   NVARCHAR(20)   NULL,                                        -- optional contact detail
    DateOfBirth   DATE           NOT NULL,                                    -- needed for age-category validation
    IsActive      BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),   -- 1 = account in good standing
    CreatedAt     DATETIME2(0)   NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),  -- audit: when registered (UTC)
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId),                       -- clustered index: fast lookups by id
    CONSTRAINT UQ_Users_Email UNIQUE (Email),                                 -- one account per email address
    CONSTRAINT CK_Users_Email CHECK (Email LIKE N'%_@_%.__%'),                -- cheap format sanity check
    CONSTRAINT CK_Users_DateOfBirth CHECK (DateOfBirth < SYSUTCDATETIME()),  -- no future birth dates
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)  -- every user has exactly one role
);
GO

-- ---------------------------------------------------------------------------------------------
-- 4. Events - created, edited and deleted by Organisers; viewed by both roles
--     Design decisions:
--       * OrganiserId is the ownership pointer - Part 2 authorises PUT/DELETE on an event by
--         comparing the session user id with this column (only the owning organiser may edit).
--       * EventType is constrained to exactly the three types required by Part 2:
--         'Run', 'Walk' or 'Cycle'.
--       * DistanceKm must be positive - a zero-distance race is always a data error.
--       * The composite unique key stops an organiser from accidentally publishing the same
--         race twice with the same name, date and venue.
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Events
(
    EventId       INT             IDENTITY(1,1) NOT NULL,              -- surrogate PK for /api/events/{id}
    OrganiserId   INT             NOT NULL,                            -- FK -> Users: the owning organiser
    EventName     NVARCHAR(100)   NOT NULL,                            -- display name, e.g. 'Durban Beachfront 10K'
    Description   NVARCHAR(500)   NOT NULL,                            -- event card blurb shown to participants
    EventDate     DATE            NOT NULL,                            -- race day; drives the upcoming-events list
    Location      NVARCHAR(100)   NOT NULL,                            -- venue/city in South Africa
    DistanceKm    DECIMAL(6,2)    NOT NULL,                            -- e.g. 5.00, 10.00, 21.10, 40.00
    EventType     NVARCHAR(10)    NOT NULL,                            -- Run | Walk | Cycle
    CONSTRAINT PK_Events PRIMARY KEY CLUSTERED (EventId),                          -- clustered index: fast lookups by id
    CONSTRAINT UQ_Events_Name_Date_Location UNIQUE (EventName, EventDate, Location),  -- no duplicate race listings
    CONSTRAINT CK_Events_DistanceKm CHECK (DistanceKm > 0),                       -- a race must have a distance
    CONSTRAINT CK_Events_EventType CHECK (EventType IN (N'Run', N'Walk', N'Cycle')), -- exactly the three brief types
    CONSTRAINT CK_Events_EventDate CHECK (EventDate >= CAST(N'2020-01-01' AS DATE)), -- guards fat-fingered years
    CONSTRAINT FK_Events_Users FOREIGN KEY (OrganiserId) REFERENCES dbo.Users (UserId)  -- only organiser rows reach here (API-enforced)
);
GO

-- ---------------------------------------------------------------------------------------------
-- 5. Routes - live route information used by Participants on race day (one route per event)
--     The brief states participants "prepare for race day using live weather and route
--     information", so route data is a first-class entity rather than a text column on Events.
--     UNIQUE(EventId) enforces the 1:1 relationship shown in the ERD: exactly one route per event.
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Routes
(
    RouteId         INT             IDENTITY(1,1) NOT NULL,               -- surrogate PK
    EventId         INT             NOT NULL,                             -- FK -> Events (one route per event)
    StartPoint      NVARCHAR(100)   NOT NULL,                             -- where the race begins (km 0)
    EndPoint        NVARCHAR(100)   NOT NULL,                             -- where the race finishes
    ElevationGainM  INT             NOT NULL CONSTRAINT DF_Routes_ElevationGainM DEFAULT (0),  -- metres, 0 for flat
    GpxTrackUrl     NVARCHAR(255)   NULL,                                 -- optional link to the GPX track
    CONSTRAINT PK_Routes PRIMARY KEY CLUSTERED (RouteId),                             -- clustered index: fast lookups by id
    CONSTRAINT UQ_Routes_EventId UNIQUE (EventId),                                    -- enforces the 1:1 with Events
    CONSTRAINT CK_Routes_ElevationGainM CHECK (ElevationGainM >= 0),                  -- elevation cannot be negative
    CONSTRAINT FK_Routes_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId) -- route belongs to one event
);
GO

-- ---------------------------------------------------------------------------------------------
-- 6. EventCategories - age or distance categories defined by Organisers per event
--     Part 2 requirement: "Organisers must be able to define age or distance categories for
--     each event (e.g. Under 20, Senior, 10km, 21km)."
--       * Age-based categories fill in MinAge/MaxAge (e.g. Under 20 -> MaxAge 19).
--       * Distance-based categories (e.g. 10km Open) leave both bounds NULL.
--       * MinAge/MaxAge are nullable precisely because of that split, and the CHECK clauses
--         keep any supplied values sane (non-negative, MinAge <= MaxAge).
--       * UNIQUE(EventId, CategoryName) stops duplicate category names inside one event.
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.EventCategories
(
    CategoryId    INT           IDENTITY(1,1) NOT NULL,          -- surrogate PK for /api/categories/{id}
    EventId       INT           NOT NULL,                        -- FK -> Events: categories belong to an event
    CategoryName  NVARCHAR(50)  NOT NULL,                        -- e.g. 'Under 20', 'Senior', '10km Open'
    MinAge        INT           NULL,                            -- NULL for distance-based categories
    MaxAge        INT           NULL,                            -- NULL = no upper age limit
    CONSTRAINT PK_EventCategories PRIMARY KEY CLUSTERED (CategoryId),                 -- clustered index: fast lookups by id
    CONSTRAINT UQ_EventCategories_Event_Name UNIQUE (EventId, CategoryName),          -- no duplicate names per event
    CONSTRAINT CK_EventCategories_MinAge CHECK (MinAge IS NULL OR MinAge >= 0),       -- ages are never negative
    CONSTRAINT CK_EventCategories_MaxAge CHECK (MaxAge IS NULL OR MaxAge >= 0),       -- ages are never negative
    CONSTRAINT CK_EventCategories_AgeRange CHECK (MinAge IS NULL OR MaxAge IS NULL OR MinAge <= MaxAge),  -- MinAge <= MaxAge
    CONSTRAINT FK_EventCategories_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId)  -- category is defined by an organiser's event
);
GO

-- ---------------------------------------------------------------------------------------------
-- 7. Enrolments - links a Participant to an Event and the Category they selected
--     This is the associative (junction) entity that resolves the many-to-many between
--     Users and Events, and it also carries the chosen category, exactly as Part 2 requires:
--     "The system must record the link between the Participant, the event, and the selected
--     category."
--       * UNIQUE(EventId, UserId) - a participant may enter a given event only once; the API
--         returns 409 Conflict when this constraint fires.
--       * Status allows a participant to withdraw (Cancelled) without destroying the record,
--         which keeps audit history intact.
--       * CHECK that CategoryId actually belongs to EventId is intentionally NOT a table
--         constraint - cross-table rules cannot be expressed as CHECK in SQL Server; Part 2
--         validates it in the API layer (400 when the category is not offered by the event).
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Enrolments
(
    EnrolmentId    INT           IDENTITY(1,1) NOT NULL,           -- surrogate PK for /api/enrolments/{id}
    EventId        INT           NOT NULL,                         -- FK -> Events
    UserId         INT           NOT NULL,                         -- FK -> Users (the participant)
    CategoryId     INT           NOT NULL,                         -- FK -> EventCategories (chosen category)
    EnrolmentDate  DATETIME2(0)  NOT NULL CONSTRAINT DF_Enrolments_EnrolmentDate DEFAULT (SYSUTCDATETIME()),  -- when they entered (UTC)
    Status         NVARCHAR(20)  NOT NULL CONSTRAINT DF_Enrolments_Status DEFAULT (N'Confirmed'),             -- Confirmed | Cancelled
    CONSTRAINT PK_Enrolments PRIMARY KEY CLUSTERED (EnrolmentId),                  -- clustered index: fast lookups by id
    CONSTRAINT UQ_Enrolments_Event_User UNIQUE (EventId, UserId),                  -- one entry per participant per event
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN (N'Confirmed', N'Cancelled')), -- only the two workflow states
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId),          -- entry belongs to one event
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId),              -- entry belongs to one participant
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.EventCategories (CategoryId)  -- category chosen at entry
);
GO

-- ---------------------------------------------------------------------------------------------
-- 8. Results - finish time and finishing position captured by Organisers after an event
--     Design decisions:
--       * UNIQUE(EnrolmentId) implements the 1:1 in the ERD - one result per enrolment, so a
--         participant cannot be recorded twice in the same race (409 in the API).
--       * EventId is denormalised from Enrolments for one specific reason: SQL Server cannot
--         enforce "one gold position per event" across a join, so UQ(EventId, FinishingPosition)
--         guarantees positions 1, 2, 3 ... are unique within each event.
--       * TIME(3) gives millisecond precision, matching electronic chip timing.
--       * FinishingPosition must be a positive number (no position 0 or negatives).
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Results
(
    ResultId          INT           IDENTITY(1,1) NOT NULL,          -- surrogate PK
    EnrolmentId       INT           NOT NULL,                        -- FK -> Enrolments (1:1, unique)
    EventId           INT           NOT NULL,                        -- FK -> Events (denormalised, see note above)
    FinishTime        TIME(3)       NOT NULL,                        -- chip time, e.g. 00:39:12.500
    FinishingPosition INT           NOT NULL,                        -- 1 = winner in the event
    CapturedAt        DATETIME2(0)  NOT NULL CONSTRAINT DF_Results_CapturedAt DEFAULT (SYSUTCDATETIME()),  -- when the organiser captured it (UTC)
    CONSTRAINT PK_Results PRIMARY KEY CLUSTERED (ResultId),                                -- clustered index: fast lookups by id
    CONSTRAINT UQ_Results_EnrolmentId UNIQUE (EnrolmentId),                                -- one result per enrolment (1:1)
    CONSTRAINT UQ_Results_Event_Position UNIQUE (EventId, FinishingPosition),              -- unique position per event
    CONSTRAINT CK_Results_FinishingPosition CHECK (FinishingPosition > 0),                 -- no position 0 or negatives
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments (EnrolmentId),  -- result belongs to one entry
    CONSTRAINT FK_Results_Events FOREIGN KEY (EventId) REFERENCES dbo.Events (EventId)     -- supports the per-event position key
);
GO

-- ---------------------------------------------------------------------------------------------
-- 9. Sessions - server-side session state used for role-based authentication (Part 2)
--     Part 2 requires session management: "The application must make use of session management
--     to maintain the user's authenticated state and role for all subsequent requests."
--       * SessionToken is the opaque bearer token returned by /api/auth/login; UNIQUE so two
--         sessions can never share a token.
--       * ExpiresAt must be after CreatedAt (CHECK) so sessions always expire - a session
--         cannot be issued that is already stale.
--       * IsActive lets /api/auth/logout invalidate a session without deleting the row,
--         preserving an audit trail of who was signed in and when.
-- ---------------------------------------------------------------------------------------------
CREATE TABLE dbo.Sessions
(
    SessionId    INT            IDENTITY(1,1) NOT NULL,             -- surrogate PK
    UserId       INT            NOT NULL,                           -- FK -> Users (whose session this is)
    SessionToken NVARCHAR(64)   NOT NULL,                           -- opaque token stored in the cookie/header
    CreatedAt    DATETIME2(0)   NOT NULL CONSTRAINT DF_Sessions_CreatedAt DEFAULT (SYSUTCDATETIME()),  -- sign-in time (UTC)
    ExpiresAt    DATETIME2(0)   NOT NULL,                           -- hard expiry, checked on every request
    IsActive     BIT            NOT NULL CONSTRAINT DF_Sessions_IsActive DEFAULT (1),  -- 0 = logged out / revoked
    CONSTRAINT PK_Sessions PRIMARY KEY CLUSTERED (SessionId),                     -- clustered index: fast lookups by id
    CONSTRAINT UQ_Sessions_SessionToken UNIQUE (SessionToken),                    -- tokens are globally unique
    CONSTRAINT CK_Sessions_ExpiresAt CHECK (ExpiresAt > CreatedAt),               -- every session has a future expiry
    CONSTRAINT FK_Sessions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)  -- session belongs to one account
);
GO

-- ---------------------------------------------------------------------------------------------
-- 10. Supporting indexes for common Part 2 queries
--      Each index maps to a query the API plan (docs/api-endpoint-plan.md) promises to run:
--        IX_Users_RoleId        -> filter users by role (participants of an event, etc.)
--        IX_Events_EventDate    -> GET /api/events orders/filters by upcoming race date
--        IX_Events_OrganiserId  -> organiser dashboard: "my events"
--        IX_Enrolments_UserId   -> GET /api/enrolments/me (a participant's own entries)
--        IX_Enrolments_Category -> per-category entry counts for an organiser
--        IX_Sessions_UserId     -> session lookup on every authenticated request
--      The sessions index is filtered (WHERE IsActive = 1) so it stays small: only the tiny
--      fraction of rows representing live sessions are indexed.
--      Each CREATE INDEX statement runs on its own so one failure cannot hide the others.
-- ---------------------------------------------------------------------------------------------
CREATE NONCLUSTERED INDEX IX_Users_RoleId        ON dbo.Users (RoleId);             -- role lookups
CREATE NONCLUSTERED INDEX IX_Events_EventDate    ON dbo.Events (EventDate);         -- upcoming-events ordering
CREATE NONCLUSTERED INDEX IX_Events_OrganiserId  ON dbo.Events (OrganiserId);       -- "my events" per organiser
CREATE NONCLUSTERED INDEX IX_Enrolments_UserId   ON dbo.Enrolments (UserId);        -- participant's own entries
CREATE NONCLUSTERED INDEX IX_Enrolments_Category ON dbo.Enrolments (CategoryId);    -- category entry counts
CREATE NONCLUSTERED INDEX IX_Sessions_UserId     ON dbo.Sessions (UserId) WHERE IsActive = 1;  -- only live sessions
GO

/* =====================================================================================
   SEED DATA
   -------------------------------------------------------------------------------------
   Realistic South African sample data so the API in Part 2 can be demonstrated without
   any manual data entry. Coverage against the Part 1 requirement:
     - two (2) Organisers ................: users 1-2   (Thabo Mokoena, Ayesha Patel)
     - two (2) Participants ..............: users 3-6   (exceeds the minimum of two)
     - three (3) Events ..................: events 1-3   (one Run, one Walk, one Cycle)
     - categories for each event .........: 9 rows       (age + distance category per event)
     - sample enrolments .................: 7 rows       (across all three events)
   Event dates are deliberately mixed: events 1 and 2 are already in the past so they carry
   results, while event 3 is still upcoming so participants can still enrol - this lets the
   Part 2 demo show both the "enter an event" and "capture a result" flows.

   IDENTITY_INSERT is used throughout so that the seed rows have predictable ids (1, 2, 3 ...)
   which makes the foreign-key relationships between these INSERTs easy to follow, and keeps
   the demo data stable between runs. Every SET IDENTITY_INSERT ... ON must be paired with
   OFF straight after the INSERT (SQL Server only permits explicit id values in between).
   ===================================================================================== */

-- Roles ---------------------------------------------------------------------------------------------
SET IDENTITY_INSERT dbo.Roles ON;   -- allow explicit RoleId values for the seed rows below
INSERT INTO dbo.Roles (RoleId, RoleName) VALUES
    (1, N'Organiser'),              -- role 1: may create/manage events, categories and results
    (2, N'Participant');            -- role 2: may browse, enrol and view own results
SET IDENTITY_INSERT dbo.Roles OFF;  -- back to normal identity behaviour
GO

-- Users --------------------------------------------------------------------------------------------
-- PasswordHash values are placeholder hashes with the BCrypt cost prefix ($2a$11$) so the
-- column shape matches what Part 2 will write; Part 2 replaces these with real BCrypt hashes
-- generated from the demo passwords at registration time.
SET IDENTITY_INSERT dbo.Users ON;   -- allow explicit UserId values so FK references below are stable
INSERT INTO dbo.Users (UserId, RoleId, FirstName, LastName, Email, PasswordHash, PhoneNumber, DateOfBirth, IsActive, CreatedAt) VALUES
    (1, 1, N'Thabo',   N'Mokoena', N'thabo.mokoena@raceday.co.za',   N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000000', N'082 555 0141', N'1985-03-14', 1, N'2026-06-01 08:00:00'),  -- organiser
    (2, 1, N'Ayesha',  N'Patel',   N'ayesha.patel@raceday.co.za',    N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000001', N'083 555 0198', N'1990-07-22', 1, N'2026-06-03 10:15:00'),  -- organiser
    (3, 2, N'Sipho',   N'Nkosi',   N'sipho.nkosi@gmail.com',         N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000002', N'071 555 0173', N'1999-11-02', 1, N'2026-06-10 17:40:00'),  -- participant, age 26 at race day
    (4, 2, N'Lerato',  N'Khumalo', N'lerato.khumalo@gmail.com',      N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000003', N'072 555 0119', N'2003-05-19', 1, N'2026-06-11 09:25:00'),  -- participant, entered both foot races
    (5, 2, N'Naledi',  N'Sithole', N'naledi.sithole@outlook.com',    N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000004', N'074 555 0166', N'2005-01-30', 1, N'2026-06-12 12:05:00'),  -- participant, youngest entrant
    (6, 2, N'Riaan',   N'van Wyk', N'riaan.vanwyk@webmail.co.za',    N'$2a$11$PLACEHOLDERHASH000000000000000000000000000000000005', N'076 555 0127', N'1992-09-08', 1, N'2026-06-14 19:30:00');  -- participant, cyclist and walker
SET IDENTITY_INSERT dbo.Users OFF;  -- back to normal identity behaviour
GO

-- Events --------------------------------------------------------------------------------------------
SET IDENTITY_INSERT dbo.Events ON;  -- allow explicit EventId values referenced by the tables after this
INSERT INTO dbo.Events (EventId, OrganiserId, EventName, Description, EventDate, Location, DistanceKm, EventType) VALUES
    (1, 1, N'Durban Beachfront 10K',       N'A flat and fast 10km along the Durban promenade, ideal for personal bests.', N'2026-08-15', N'Durban Beachfront, KwaZulu-Natal', 10.00, N'Run'),    -- completed: has results
    (2, 2, N'Freedom Day Charity Walk',    N'A family-friendly 5km charity walk raising funds for local schools.',        N'2026-04-26', N'Freedom Park, Pretoria',            5.00,  N'Walk'),   -- completed: has results
    (3, 1, N'Table Bay Cycle Challenge',   N'A scenic 40km cycling route around Table Bay with two category climbs.',      N'2026-11-07', N'Cape Town Waterfront, Western Cape', 40.00, N'Cycle'); -- upcoming: enrolments open
SET IDENTITY_INSERT dbo.Events OFF; -- back to normal identity behaviour
GO

-- Routes --------------------------------------------------------------------------------------------
-- One route per event (UNIQUE EventId mirrors the 1:1 relationship in the ERD).
SET IDENTITY_INSERT dbo.Routes ON;  -- allow explicit RouteId values
INSERT INTO dbo.Routes (RouteId, EventId, StartPoint, EndPoint, ElevationGainM, GpxTrackUrl) VALUES
    (1, 1, N'uShaka Beach Car Park',   N'uShaka Beach Car Park',   15,  N'https://raceday.co.za/gpx/durban-10k.gpx'),      -- flat out-and-back
    (2, 2, N'Freedom Park Entrance',   N'Freedom Park Entrance',   20,  N'https://raceday.co.za/gpx/freedom-walk.gpx'),     -- short loop, wheelchair friendly
    (3, 3, N'V&A Waterfront Clock',    N'V&A Waterfront Clock',    320, N'https://raceday.co.za/gpx/table-bay-40k.gpx');    -- two climbs, hence the elevation
SET IDENTITY_INSERT dbo.Routes OFF; -- back to normal identity behaviour
GO

-- Event categories ---------------------------------------------------------------------------------
-- Three categories per event: an age category pair (Under 20 / Senior) plus a distance category,
-- covering both category styles the brief requires. MinAge/MaxAge are NULL for distance rows.
SET IDENTITY_INSERT dbo.EventCategories ON;  -- allow explicit CategoryId values referenced by enrolments
INSERT INTO dbo.EventCategories (CategoryId, EventId, CategoryName, MinAge, MaxAge) VALUES
    (1, 1, N'Under 20',   NULL, 19),   -- age-based: up to and including 19
    (2, 1, N'Senior',     20,   NULL), -- age-based: 20 and over
    (3, 1, N'10km Open',  NULL, NULL), -- distance-based: no age bounds
    (4, 2, N'Under 20',   NULL, 19),
    (5, 2, N'Senior',     20,   NULL),
    (6, 2, N'5km Family', NULL, NULL), -- distance-based
    (7, 3, N'Under 20',   NULL, 19),
    (8, 3, N'Senior',     20,   NULL),
    (9, 3, N'40km Elite', NULL, NULL); -- distance-based
SET IDENTITY_INSERT dbo.EventCategories OFF; -- back to normal identity behaviour
GO

-- Enrolments ---------------------------------------------------------------------------------------
-- Spread across all three events; the two completed events hold the enrolments that carry results.
SET IDENTITY_INSERT dbo.Enrolments ON;  -- allow explicit EnrolmentId values referenced by results
INSERT INTO dbo.Enrolments (EnrolmentId, EventId, UserId, CategoryId, EnrolmentDate, Status) VALUES
    (1, 1, 3, 3, N'2026-07-02 18:22:00', N'Confirmed'),  -- Sipho  -> Durban 10K, 10km Open
    (2, 1, 4, 2, N'2026-07-04 07:10:00', N'Confirmed'),  -- Lerato -> Durban 10K, Senior
    (3, 1, 5, 1, N'2026-07-05 21:45:00', N'Confirmed'),  -- Naledi -> Durban 10K, Under 20 (born 2005)
    (4, 2, 4, 6, N'2026-03-18 11:30:00', N'Confirmed'),  -- Lerato -> Freedom Walk, 5km Family
    (5, 2, 6, 5, N'2026-03-19 16:05:00', N'Confirmed'),  -- Riaan  -> Freedom Walk, Senior
    (6, 3, 3, 9, N'2026-09-12 09:15:00', N'Confirmed'),  -- Sipho  -> Cycle Challenge, 40km Elite
    (7, 3, 6, 8, N'2026-09-14 20:40:00', N'Confirmed');  -- Riaan  -> Cycle Challenge, Senior
SET IDENTITY_INSERT dbo.Enrolments OFF; -- back to normal identity behaviour
GO

-- Results (only for events already completed: Event 1 and Event 2) --------------------------------
-- Positions are unique within each event, satisfying UQ_Results_Event_Position; each result maps
-- 1:1 to an enrolment via the unique EnrolmentId.
SET IDENTITY_INSERT dbo.Results ON;  -- allow explicit ResultId values
INSERT INTO dbo.Results (ResultId, EnrolmentId, EventId, FinishTime, FinishingPosition, CapturedAt) VALUES
    (1, 1, 1, N'00:39:12.500', 1, N'2026-08-15 09:45:00'),  -- Durban 10K podium: gold
    (2, 2, 1, N'00:44:03.750', 2, N'2026-08-15 09:50:00'),  -- Durban 10K podium: silver
    (3, 3, 1, N'00:47:55.250', 3, N'2026-08-15 09:55:00'),  -- Durban 10K podium: bronze
    (4, 4, 2, N'00:31:20.000', 1, N'2026-04-26 08:30:00'),  -- Freedom Walk: first finisher
    (5, 5, 2, N'00:34:48.500', 2, N'2026-04-26 08:35:00');  -- Freedom Walk: second finisher
SET IDENTITY_INSERT dbo.Results OFF; -- back to normal identity behaviour
GO

-- Sessions (sample sessions for the Part 2 session-management flow) -------------------------------
-- One expired/inactive session (demonstrates re-authentication) and one live session.
SET IDENTITY_INSERT dbo.Sessions ON;  -- allow explicit SessionId values
INSERT INTO dbo.Sessions (SessionId, UserId, SessionToken, CreatedAt, ExpiresAt, IsActive) VALUES
    (1, 3, N'a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90', N'2026-09-20 18:00:00', N'2026-09-21 18:00:00', 0),  -- expired
    (2, 1, N'b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90a1', N'2026-09-22 07:30:00', N'2026-09-23 07:30:00', 1);  -- active organiser session
SET IDENTITY_INSERT dbo.Sessions OFF; -- back to normal identity behaviour
GO

-- Final confirmation message shown in the SSMS Messages pane after a successful run ---------------
PRINT N'RaceDay database created and seeded successfully.';
GO
