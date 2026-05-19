using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MLM_Level.Models;

namespace MLM_Level.Data
{
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Ensure database is created
            context.Database.EnsureCreated();

            // Register Stored Procedures
            RegisterStoredProcedures(context);

            // Check if any users exist. If not, seed admin and root users
            if (context.Users.Any())
            {
                return; // DB has been seeded
            }

            var passwordHasher = new PasswordHasher<User>();

            // 1. Seed Admin User
            var admin = new User
            {
                Username = "admin",
                Email = "admin@mlm.com",
                FullName = "System Administrator",
                Phone = "9999999999",
                JoinedDate = DateTime.UtcNow.AddDays(-10),
                ActivationDate = DateTime.UtcNow.AddDays(-10),
                IsActive = true,
                IsAdmin = true,
                WalletBalance = 0.00m,
                ReferralCode = "ADMINCODE"
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin@123");
            context.Users.Add(admin);
            context.SaveChanges();

            // 2. Seed Root Member (Top level active user in the binary/placement tree)
            var root = new User
            {
                Username = "root",
                Email = "root@mlm.com",
                FullName = "Root Member",
                Phone = "8888888888",
                SponsorId = admin.Id, // Admin is sponsor
                ParentId = admin.Id,  // Admin is parent
                JoinedDate = DateTime.UtcNow.AddDays(-9),
                ActivationDate = DateTime.UtcNow.AddDays(-9),
                IsActive = true,
                IsAdmin = false,
                WalletBalance = 0.00m,
                ReferralCode = "ROOT123"
            };
            root.PasswordHash = passwordHasher.HashPassword(root, "Root@123");
            context.Users.Add(root);
            context.SaveChanges();
        }

        private static void RegisterStoredProcedures(ApplicationDbContext context)
        {
            // 1. sp_DistributeCommission
            context.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_DistributeCommission')
                    DROP PROCEDURE sp_DistributeCommission;
            ");
            context.Database.ExecuteSqlRaw(@"
                CREATE PROCEDURE sp_DistributeCommission
                    @ActivatedUserId INT,
                    @PackageAmount DECIMAL(18,2)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    BEGIN TRANSACTION;
                    BEGIN TRY
                        DECLARE @CurrentSponsorId INT;
                        SELECT @CurrentSponsorId = SponsorId FROM Users WHERE Id = @ActivatedUserId;

                        DECLARE @Level INT = 1;
                        DECLARE @CommissionPercent DECIMAL(5,2);
                        DECLARE @CommissionAmount DECIMAL(18,2);
                        DECLARE @SponsorActive BIT;

                        WHILE @Level <= 5 AND @CurrentSponsorId IS NOT NULL
                        BEGIN
                            SET @CommissionPercent = CASE @Level
                                WHEN 1 THEN 10.0
                                WHEN 2 THEN 5.0
                                WHEN 3 THEN 3.0
                                WHEN 4 THEN 2.0
                                WHEN 5 THEN 1.0
                                ELSE 0.0
                            END;

                            SET @CommissionAmount = (@PackageAmount * @CommissionPercent) / 100.0;

                            SELECT @SponsorActive = IsActive FROM Users WHERE Id = @CurrentSponsorId;

                            -- Active sponsor gets payout, or if the sponsor is the Admin
                            IF @SponsorActive = 1 OR EXISTS (SELECT 1 FROM Users WHERE Id = @CurrentSponsorId AND IsAdmin = 1)
                            BEGIN
                                UPDATE Users 
                                SET WalletBalance = WalletBalance + @CommissionAmount 
                                WHERE Id = @CurrentSponsorId;

                                INSERT INTO CommissionTrans (UserId, FromUserId, Amount, Level, Timestamp, Description)
                                VALUES (
                                    @CurrentSponsorId, 
                                    @ActivatedUserId, 
                                    @CommissionAmount, 
                                    @Level, 
                                    GETUTCDATE(), 
                                    'Level ' + CAST(@Level AS VARCHAR(2)) + ' Commission from User: ' + (SELECT Username FROM Users WHERE Id = @ActivatedUserId)
                                );
                            END

                            -- Traverse up the sponsor line
                            SELECT @CurrentSponsorId = SponsorId FROM Users WHERE Id = @CurrentSponsorId;
                            SET @Level = @Level + 1;
                        END

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH
                END
            ");

            // 2. sp_GetDownlineTree
            context.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetDownlineTree')
                    DROP PROCEDURE sp_GetDownlineTree;
            ");
            context.Database.ExecuteSqlRaw(@"
                CREATE PROCEDURE sp_GetDownlineTree
                    @StartUserId INT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    WITH DownlineCTE AS (
                        SELECT 
                            Id, 
                            Username, 
                            FullName, 
                            SponsorId, 
                            ParentId, 
                            IsActive, 
                            JoinedDate,
                            1 AS Level
                        FROM Users
                        WHERE ParentId = @StartUserId

                        UNION ALL

                        SELECT 
                            u.Id, 
                            u.Username, 
                            u.FullName, 
                            u.SponsorId, 
                            u.ParentId, 
                            u.IsActive, 
                            u.JoinedDate,
                            d.Level + 1 AS Level
                        FROM Users u
                        INNER JOIN DownlineCTE d ON u.ParentId = d.Id
                    )
                    SELECT 
                        d.Id, 
                        d.Username, 
                        d.FullName, 
                        d.SponsorId, 
                        d.ParentId, 
                        d.Level, 
                        d.IsActive, 
                        d.JoinedDate,
                        sp.Username AS SponsorName,
                        p.Username AS ParentName
                    FROM DownlineCTE d
                    LEFT JOIN Users sp ON d.SponsorId = sp.Id
                    LEFT JOIN Users p ON d.ParentId = p.Id
                    ORDER BY d.Level, d.JoinedDate;
                END
            ");

            // 3. sp_GetTeamStats
            context.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetTeamStats')
                    DROP PROCEDURE sp_GetTeamStats;
            ");
            context.Database.ExecuteSqlRaw(@"
                CREATE PROCEDURE sp_GetTeamStats
                    @UserId INT,
                    @TotalTeam INT OUTPUT,
                    @ActiveTeam INT OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    
                    WITH DownlineCTE AS (
                        SELECT Id, IsActive FROM Users WHERE ParentId = @UserId
                        UNION ALL
                        SELECT u.Id, u.IsActive FROM Users u
                        INNER JOIN DownlineCTE d ON u.ParentId = d.Id
                    )
                    SELECT 
                        @TotalTeam = COUNT(*),
                        @ActiveTeam = ISNULL(SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END), 0)
                    FROM DownlineCTE;
                END
            ");

            // 4. sp_TransferWalletFunds
            context.Database.ExecuteSqlRaw(@"
                IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_TransferWalletFunds')
                    DROP PROCEDURE sp_TransferWalletFunds;
            ");
            context.Database.ExecuteSqlRaw(@"
                CREATE PROCEDURE sp_TransferWalletFunds
                    @SenderId INT,
                    @ReceiverUsername NVARCHAR(50),
                    @Amount DECIMAL(18,2),
                    @Success BIT OUTPUT,
                    @Message NVARCHAR(250) OUTPUT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    SET @Success = 0;

                    -- Verify receiver exists
                    DECLARE @ReceiverId INT;
                    SELECT @ReceiverId = Id FROM Users WHERE Username = @ReceiverUsername;

                    IF @ReceiverId IS NULL
                    BEGIN
                        SET @Message = 'Receiver username does not exist.';
                        RETURN;
                    END

                    IF @SenderId = @ReceiverId
                    BEGIN
                        SET @Message = 'You cannot transfer funds to yourself.';
                        RETURN;
                    END

                    -- Verify amount is positive
                    IF @Amount <= 0
                    BEGIN
                        SET @Message = 'Transfer amount must be greater than zero.';
                        RETURN;
                    END

                    -- Start Transaction
                    BEGIN TRANSACTION;
                    BEGIN TRY
                        -- Verify sender balance
                        DECLARE @SenderBalance DECIMAL(18,2);
                        SELECT @SenderBalance = WalletBalance FROM Users WHERE Id = @SenderId;

                        IF @SenderBalance < @Amount
                        BEGIN
                            ROLLBACK TRANSACTION;
                            SET @Message = 'Insufficient wallet balance.';
                            RETURN;
                        END

                        -- Deduct from sender
                        UPDATE Users 
                        SET WalletBalance = WalletBalance - @Amount 
                        WHERE Id = @SenderId;

                        -- Add to receiver
                        UPDATE Users 
                        SET WalletBalance = WalletBalance + @Amount 
                        WHERE Id = @ReceiverId;

                        -- Log to transaction ledger (for Sender as Debit transfer)
                        INSERT INTO CommissionTrans (UserId, FromUserId, Amount, Level, Timestamp, Description)
                        VALUES (
                            @SenderId, 
                            @ReceiverId, 
                            -@Amount,
                            0,
                            GETUTCDATE(), 
                            'P2P Wallet Transfer to User: ' + @ReceiverUsername
                        );

                        -- Log to transaction ledger (for Receiver as Credit transfer)
                        INSERT INTO CommissionTrans (UserId, FromUserId, Amount, Level, Timestamp, Description)
                        VALUES (
                            @ReceiverId, 
                            @SenderId, 
                            @Amount,
                            0,
                            GETUTCDATE(), 
                            'P2P Wallet Transfer from User: ' + (SELECT Username FROM Users WHERE Id = @SenderId)
                        );

                        COMMIT TRANSACTION;
                        SET @Success = 1;
                        SET @Message = 'Wallet funds transferred successfully.';
                    END TRY
                    BEGIN CATCH
                        ROLLBACK TRANSACTION;
                        SET @Message = 'Error occurred during transfer: ' + ERROR_MESSAGE();
                    END CATCH
                END
            ");
        }
    }
}
