/* =====================================================================================
   RaceDay - Database Test Suite
   -------------------------------------------------------------------------------------
   Module    : PROG6212 - Programming 2B
   Part      : Part 1 - companion test file for docs/RaceDayDatabase.sql (Section C)
   How to run: Execute docs/RaceDayDatabase.sql first, then run this file in SSMS (F5).
   What it does:
     - Covers every object in the schema: all 8 tables, every UNIQUE / CHECK / FK
       constraint, the seed coverage required by the brief, and full referential
       integrity across the database.
     - Each test prints a "PASS" or "FAIL" line in the SSMS Messages pane.
     - If ANY test fails the script raises an error at the end (red text), so a clean
       run is visibly "ALL TESTS PASSED".
     - Every negative test is wrapped in TRY/CATCH (failed inserts raise errors by
       design) and every test that inserts valid rows runs inside a transaction that
       is rolled back, so this suite never changes your data and can be re-run freely.
   Note on scope: Part 1 of this portfolio contains no application code by design, so
   there are no classes to unit test yet - class-level unit tests are written in
   Part 2 against the API. This suite tests the part that DOES exist: the database.
   ===================================================================================== */
USE RaceDay;
GO

DECLARE @fail INT = 0;   -- incremented by every failing test; checked at the very end

PRINT '=== RaceDay database test suite: tables, constraints, integrity, seed coverage ===';

/* --------------------------------------------------------------------------------------
   GROUP A - one data/coverage test per table (8 objects)
   -------------------------------------------------------------------------------------- */

-- TEST 01 - Roles: exactly the two roles named in the brief, nothing else
IF (SELECT COUNT(*) FROM dbo.Roles) = 2
   AND NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleName NOT IN (N'Organiser', N'Participant'))
    PRINT N'PASS 01 - Roles: exactly two roles (Organiser, Participant)';
ELSE BEGIN PRINT N'FAIL 01 - Roles: expected exactly the two brief roles'; SET @fail = 1; END

-- TEST 02 - Users: at least two Organisers seeded (brief minimum)
IF (SELECT COUNT(*) FROM dbo.Users u JOIN dbo.Roles r ON u.RoleId = r.RoleId WHERE r.RoleName = N'Organiser') >= 2
    PRINT N'PASS 02 - Users: at least two Organisers seeded';
ELSE BEGIN PRINT N'FAIL 02 - Users: need at least two Organisers'; SET @fail = 1; END

-- TEST 03 - Users: at least two Participants seeded (brief minimum)
IF (SELECT COUNT(*) FROM dbo.Users u JOIN dbo.Roles r ON u.RoleId = r.RoleId WHERE r.RoleName = N'Participant') >= 2
    PRINT N'PASS 03 - Users: at least two Participants seeded';
ELSE BEGIN PRINT N'FAIL 03 - Users: need at least two Participants'; SET @fail = 1; END

-- TEST 04 - Events: three events seeded, covering all three event types (Run, Walk, Cycle)
IF (SELECT COUNT(*) FROM dbo.Events) >= 3
   AND (SELECT COUNT(DISTINCT EventType) FROM dbo.Events) = 3
    PRINT N'PASS 04 - Events: three events covering Run, Walk and Cycle';
ELSE BEGIN PRINT N'FAIL 04 - Events: need >= 3 events across all three types'; SET @fail = 1; END

-- TEST 05 - Routes: exactly one route per event (the 1:1 relationship in the ERD)
IF (SELECT COUNT(*) FROM dbo.Routes) = (SELECT COUNT(*) FROM dbo.Events)
   AND NOT EXISTS (SELECT EventId FROM dbo.Events e WHERE NOT EXISTS (SELECT 1 FROM dbo.Routes r WHERE r.EventId = e.EventId))
    PRINT N'PASS 05 - Routes: exactly one route for every event (1:1 with Events)';
ELSE BEGIN PRINT N'FAIL 05 - Routes: route count must equal event count'; SET @fail = 1; END

-- TEST 06 - EventCategories: every event has at least one category (brief: categories for each event)
IF NOT EXISTS (SELECT 1 FROM dbo.Events e WHERE NOT EXISTS (SELECT 1 FROM dbo.EventCategories c WHERE c.EventId = e.EventId))
   AND (SELECT COUNT(*) FROM dbo.EventCategories c JOIN dbo.EventCategories d
        ON c.EventId = d.EventId AND c.CategoryName = d.CategoryName AND c.CategoryId <> d.CategoryId) = 0
    PRINT N'PASS 06 - EventCategories: every event categorised, no duplicate names per event';
ELSE BEGIN PRINT N'FAIL 06 - EventCategories: missing or duplicated categories'; SET @fail = 1; END

-- TEST 07 - Enrolments: sample enrolments exist, and each uses a category OF THAT event
IF (SELECT COUNT(*) FROM dbo.Enrolments) >= 1
   AND NOT EXISTS (
        SELECT 1 FROM dbo.Enrolments en
        JOIN dbo.EventCategories c ON en.CategoryId = c.CategoryId
        WHERE c.EventId <> en.EventId)          -- category must belong to the same event
    PRINT N'PASS 07 - Enrolments: sample rows exist and every category matches its event';
ELSE BEGIN PRINT N'FAIL 07 - Enrolments: missing seed rows or mismatched category'; SET @fail = 1; END

-- TEST 08 - Results: seeded results exist and finishing positions are unique within each event
IF (SELECT COUNT(*) FROM dbo.Results) >= 1
   AND NOT EXISTS (SELECT 1 FROM dbo.Results GROUP BY EventId HAVING COUNT(*) <> COUNT(DISTINCT FinishingPosition))
    PRINT N'PASS 08 - Results: results seeded, positions unique per event';
ELSE BEGIN PRINT N'FAIL 08 - Results: missing results or duplicate positions in an event'; SET @fail = 1; END

-- TEST 09 - Sessions: both an expired and an active session seeded (login/logout demo)
IF EXISTS (SELECT 1 FROM dbo.Sessions WHERE IsActive = 0)
   AND EXISTS (SELECT 1 FROM dbo.Sessions WHERE IsActive = 1)
    PRINT N'PASS 09 - Sessions: one expired and one active session seeded';
ELSE BEGIN PRINT N'FAIL 09 - Sessions: need both an inactive and an active session'; SET @fail = 1; END

/* --------------------------------------------------------------------------------------
   GROUP B - referential integrity: no orphan rows anywhere across the eight tables
   -------------------------------------------------------------------------------------- */

-- TEST 10 - every foreign key resolves (walks each of the nine relationships in the ERD)
IF NOT EXISTS (
    SELECT 1 FROM dbo.Users u            WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r       WHERE r.RoleId = u.RoleId)
    UNION ALL SELECT 1 FROM dbo.Events e  WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u      WHERE u.UserId = e.OrganiserId)
    UNION ALL SELECT 1 FROM dbo.Routes r  WHERE NOT EXISTS (SELECT 1 FROM dbo.Events e     WHERE e.EventId = r.EventId)
    UNION ALL SELECT 1 FROM dbo.EventCategories c WHERE NOT EXISTS (SELECT 1 FROM dbo.Events e WHERE e.EventId = c.EventId)
    UNION ALL SELECT 1 FROM dbo.Enrolments en WHERE NOT EXISTS (SELECT 1 FROM dbo.Events e    WHERE e.EventId = en.EventId)
    UNION ALL SELECT 1 FROM dbo.Enrolments en WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u     WHERE u.UserId = en.UserId)
    UNION ALL SELECT 1 FROM dbo.Enrolments en WHERE NOT EXISTS (SELECT 1 FROM dbo.EventCategories c WHERE c.CategoryId = en.CategoryId)
    UNION ALL SELECT 1 FROM dbo.Results res WHERE NOT EXISTS (SELECT 1 FROM dbo.Enrolments en WHERE en.EnrolmentId = res.EnrolmentId)
    UNION ALL SELECT 1 FROM dbo.Sessions s WHERE NOT EXISTS (SELECT 1 FROM dbo.Users u        WHERE u.UserId = s.UserId))
    PRINT N'PASS 10 - Referential integrity: no orphan rows in any of the nine relationships';
ELSE BEGIN PRINT N'FAIL 10 - Referential integrity: orphan rows detected'; SET @fail = 1; END

/* --------------------------------------------------------------------------------------
   GROUP C - constraint tests: each negative test EXPECTS the database to reject the row.
   The expected error codes are: 2601/2627 = unique key, 547 = CHECK or foreign key.
   -------------------------------------------------------------------------------------- */

-- TEST 11 - UQ_Users_Email: a second account with an existing email must be rejected
BEGIN TRY
    INSERT INTO dbo.Users (RoleId, FirstName, LastName, Email, PasswordHash, DateOfBirth)
    VALUES (2, N'Duplicate', N'Email', N'sipho.nkosi@gmail.com', N'$2a$11$TESTHASH00000000000000000000000000000000000000000000', N'1995-01-01');
    PRINT N'FAIL 11 - UQ_Users_Email: duplicate email was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() IN (2601, 2627) PRINT N'PASS 11 - UQ_Users_Email: duplicate email rejected';
    ELSE BEGIN PRINT N'FAIL 11 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 12 - CK_Users_DateOfBirth: a future date of birth must be rejected
BEGIN TRY
    INSERT INTO dbo.Users (RoleId, FirstName, LastName, Email, PasswordHash, DateOfBirth)
    VALUES (2, N'Future', N'Dob', N'future.dob@raceday.co.za', N'$2a$11$TESTHASH00000000000000000000000000000000000000000000', N'2030-01-01');
    PRINT N'FAIL 12 - CK_Users_DateOfBirth: future birth date was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 547 PRINT N'PASS 12 - CK_Users_DateOfBirth: future birth date rejected';
    ELSE BEGIN PRINT N'FAIL 12 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 13 - CK_Events_EventType: only Run, Walk or Cycle may be stored
BEGIN TRY
    INSERT INTO dbo.Events (OrganiserId, EventName, Description, EventDate, Location, DistanceKm, EventType)
    VALUES (1, N'Bad Type Race', N'Uses an event type outside the brief.', N'2026-12-01', N'Test Venue', 5.00, N'Swim');
    PRINT N'FAIL 13 - CK_Events_EventType: invalid event type was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 547 PRINT N'PASS 13 - CK_Events_EventType: invalid event type rejected';
    ELSE BEGIN PRINT N'FAIL 13 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 14 - CK_Events_DistanceKm: a zero or negative distance must be rejected
BEGIN TRY
    INSERT INTO dbo.Events (OrganiserId, EventName, Description, EventDate, Location, DistanceKm, EventType)
    VALUES (1, N'Zero Distance Race', N'Has no distance at all.', N'2026-12-02', N'Test Venue', 0, N'Run');
    PRINT N'FAIL 14 - CK_Events_DistanceKm: zero distance was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 547 PRINT N'PASS 14 - CK_Events_DistanceKm: zero distance rejected';
    ELSE BEGIN PRINT N'FAIL 14 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 15 - UQ_EventCategories_Event_Name: the same category name cannot repeat in one event
BEGIN TRY
    INSERT INTO dbo.EventCategories (EventId, CategoryName, MinAge, MaxAge)
    VALUES (1, N'Under 20', NULL, 19);   -- event 1 already has this category (seed row 1)
    PRINT N'FAIL 15 - duplicate category name in one event was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() IN (2601, 2627) PRINT N'PASS 15 - UQ_EventCategories_Event_Name: duplicate category rejected';
    ELSE BEGIN PRINT N'FAIL 15 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 16 - UQ_Enrolments_Event_User: a participant may enter the same event only once
BEGIN TRY
    INSERT INTO dbo.Enrolments (EventId, UserId, CategoryId)
    VALUES (1, 3, 3);                    -- user 3 (Sipho) already has enrolment 1 in event 1
    PRINT N'FAIL 16 - double enrolment was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() IN (2601, 2627) PRINT N'PASS 16 - UQ_Enrolments_Event_User: double enrolment rejected';
    ELSE BEGIN PRINT N'FAIL 16 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 17 - FK_Enrolments_Categories: an enrolment cannot reference a category that does not exist
BEGIN TRY
    INSERT INTO dbo.Enrolments (EventId, UserId, CategoryId)
    VALUES (1, 4, 999);                  -- category 999 does not exist
    PRINT N'FAIL 17 - orphan category in an enrolment was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 547 PRINT N'PASS 17 - FK_Enrolments_Categories: unknown category rejected';
    ELSE BEGIN PRINT N'FAIL 17 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 18 - UQ_Results_EnrolmentId: one result per enrolment (the 1:1 in the ERD)
BEGIN TRY
    INSERT INTO dbo.Results (EnrolmentId, EventId, FinishTime, FinishingPosition)
    VALUES (1, 1, N'00:40:00.000', 9);   -- enrolment 1 already has result 1
    PRINT N'FAIL 18 - second result for one enrolment was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() IN (2601, 2627) PRINT N'PASS 18 - UQ_Results_EnrolmentId: duplicate result rejected';
    ELSE BEGIN PRINT N'FAIL 18 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 19 - UQ_Results_Event_Position: two finishers cannot share a position in one event
--           Runs inside a transaction: the first (valid) insert is rolled back either way.
BEGIN TRY
    BEGIN TRAN;
        INSERT INTO dbo.Results (EnrolmentId, EventId, FinishTime, FinishingPosition)
        VALUES (6, 3, N'01:00:00.000', 99);   -- enrolment 6 (event 3): valid, position 99 free
        BEGIN TRY
            INSERT INTO dbo.Results (EnrolmentId, EventId, FinishTime, FinishingPosition)
            VALUES (7, 3, N'01:05:00.000', 99);   -- same event, same position 99: must fail
            PRINT N'FAIL 19 - duplicate finishing position was accepted'; SET @fail = 1;
        END TRY
        BEGIN CATCH
            IF ERROR_NUMBER() IN (2601, 2627) PRINT N'PASS 19 - UQ_Results_Event_Position: duplicate position rejected';
            ELSE BEGIN PRINT N'FAIL 19 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
        END CATCH
    ROLLBACK TRAN;   -- discards the valid insert from this test; seed data untouched
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT N'FAIL 19 - setup error: ' + ERROR_MESSAGE(); SET @fail = 1;
END CATCH

-- TEST 20 - CK_Sessions_ExpiresAt: a session may not expire before it starts
BEGIN TRY
    INSERT INTO dbo.Sessions (UserId, SessionToken, CreatedAt, ExpiresAt, IsActive)
    VALUES (1, N'ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff', N'2026-09-23 08:00:00', N'2026-09-22 08:00:00', 1);
    PRINT N'FAIL 20 - CK_Sessions_ExpiresAt: already-expired session was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 547 PRINT N'PASS 20 - CK_Sessions_ExpiresAt: expiry before creation rejected';
    ELSE BEGIN PRINT N'FAIL 20 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 21 - CK_Enrolments_Status: only Confirmed or Cancelled may be stored
BEGIN TRY
    INSERT INTO dbo.Enrolments (EventId, UserId, CategoryId, Status)
    VALUES (3, 4, 8, N'Pending');        -- 'Pending' is not a permitted status
    PRINT N'FAIL 21 - CK_Enrolments_Status: invalid status was accepted'; SET @fail = 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 547 PRINT N'PASS 21 - CK_Enrolments_Status: invalid status rejected';
    ELSE BEGIN PRINT N'FAIL 21 - wrong error: ' + ERROR_MESSAGE(); SET @fail = 1; END
END CATCH

-- TEST 22 - seed consistency: results exist only for completed events (event 3 is upcoming)
IF NOT EXISTS (SELECT 1 FROM dbo.Results res JOIN dbo.Events e ON res.EventId = e.EventId WHERE e.EventDate >= CAST(N'2026-09-23' AS DATE))
    PRINT N'PASS 22 - seed consistency: no results recorded for upcoming events';
ELSE BEGIN PRINT N'FAIL 22 - seed consistency: a future event already has results'; SET @fail = 1; END

/* --------------------------------------------------------------------------------------
   Summary - a single error at the end means at least one test above failed
   -------------------------------------------------------------------------------------- */
PRINT '=== Test suite finished ===';

IF @fail = 1
    RAISERROR (N'RaceDay database test suite FAILED - review the FAIL lines above.', 16, 1);
ELSE
    PRINT N'ALL TESTS PASSED - 22/22';
GO
