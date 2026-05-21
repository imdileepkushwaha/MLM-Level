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

            // Create tables if not exist
            CreateNewTablesIfNotExist(context);

            // Seed settings & packages & announcements if they don't exist
            SeedNewSystemData(context);

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

                        -- Fetch custom level commission rates from settings
                        DECLARE @L1 DECIMAL(18,2), @L2 DECIMAL(18,2), @L3 DECIMAL(18,2), @L4 DECIMAL(18,2), @L5 DECIMAL(18,2);
                        SELECT TOP 1 
                            @L1 = Level1Commission, 
                            @L2 = Level2Commission, 
                            @L3 = Level3Commission, 
                            @L4 = Level4Commission, 
                            @L5 = Level5Commission 
                        FROM MlmSettings;

                        -- Fallback to default if no settings exist
                        SET @L1 = ISNULL(@L1, 10.0);
                        SET @L2 = ISNULL(@L2, 5.0);
                        SET @L3 = ISNULL(@L3, 3.0);
                        SET @L4 = ISNULL(@L4, 2.0);
                        SET @L5 = ISNULL(@L5, 1.0);

                        WHILE @Level <= 5 AND @CurrentSponsorId IS NOT NULL
                        BEGIN
                            SET @CommissionPercent = CASE @Level
                                WHEN 1 THEN @L1
                                WHEN 2 THEN @L2
                                WHEN 3 THEN @L3
                                WHEN 4 THEN @L4
                                WHEN 5 THEN @L5
                                ELSE 0.0
                            END;

                            SET @CommissionAmount = (@PackageAmount * @CommissionPercent) / 100.0;

                            SELECT @SponsorActive = IsActive FROM Users WHERE Id = @CurrentSponsorId;

                            -- Active sponsor gets payout, or if the sponsor is the Admin
                            IF @SponsorActive = 1 OR EXISTS (SELECT 1 FROM Users WHERE Id = @CurrentSponsorId AND IsAdmin = 1)
                            BEGIN
                                UPDATE Users 
                                SET IncomeWallet = IncomeWallet + @CommissionAmount 
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

        private static void CreateNewTablesIfNotExist(ApplicationDbContext context)
        {
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MlmSettings' AND xtype='U')
                BEGIN
                    CREATE TABLE MlmSettings (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Level1Commission DECIMAL(18,2) NOT NULL DEFAULT 10.00,
                        Level2Commission DECIMAL(18,2) NOT NULL DEFAULT 5.00,
                        Level3Commission DECIMAL(18,2) NOT NULL DEFAULT 3.00,
                        Level4Commission DECIMAL(18,2) NOT NULL DEFAULT 2.00,
                        Level5Commission DECIMAL(18,2) NOT NULL DEFAULT 1.00,
                        MinWithdrawalLimit DECIMAL(18,2) NOT NULL DEFAULT 500.00,
                        WithdrawalFeePercent DECIMAL(18,2) NOT NULL DEFAULT 5.00,
                        CompanyQrCodeUrl NVARCHAR(500) NOT NULL DEFAULT '',
                        BankDetails NVARCHAR(1000) NOT NULL DEFAULT ''
                    );
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Packages' AND xtype='U')
                BEGIN
                    CREATE TABLE Packages (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Name NVARCHAR(100) NOT NULL,
                        Price DECIMAL(18,2) NOT NULL,
                        Description NVARCHAR(500) NOT NULL DEFAULT '',
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedDate DATETIME NOT NULL DEFAULT GETUTCDATE()
                    );
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Announcements' AND xtype='U')
                BEGIN
                    CREATE TABLE Announcements (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Title NVARCHAR(200) NOT NULL,
                        Content NVARCHAR(MAX) NOT NULL,
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedDate DATETIME NOT NULL DEFAULT GETUTCDATE()
                    );
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF COL_LENGTH('ActivationRequests', 'PaymentSlipUrl') IS NULL
                BEGIN
                    ALTER TABLE ActivationRequests
                    ADD PaymentSlipUrl NVARCHAR(255) NULL;
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF COL_LENGTH('ActivationRequests', 'RejectionReason') IS NULL
                BEGIN
                    ALTER TABLE ActivationRequests
                    ADD RejectionReason NVARCHAR(500) NULL;
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF COL_LENGTH('WithdrawalRequests', 'RejectionReason') IS NULL
                BEGIN
                    ALTER TABLE WithdrawalRequests
                    ADD RejectionReason NVARCHAR(500) NULL;
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PasswordResetTokens' AND xtype='U')
                BEGIN
                    CREATE TABLE PasswordResetTokens (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NOT NULL,
                        Token NVARCHAR(128) NOT NULL,
                        ExpiresAt DATETIME2 NOT NULL,
                        IsUsed BIT NOT NULL DEFAULT 0,
                        CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        CONSTRAINT FK_PasswordResetTokens_Users_UserId
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IX_PasswordResetTokens_Token ON PasswordResetTokens(Token);
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UX_ActivationRequests_TransactionReference_Active' AND object_id = OBJECT_ID('ActivationRequests'))
                BEGIN
                    CREATE UNIQUE NONCLUSTERED INDEX UX_ActivationRequests_TransactionReference_Active
                    ON ActivationRequests (TransactionReference)
                    WHERE Status IN ('Pending', 'Approved');
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MemberRewards' AND xtype='U')
                BEGIN
                    CREATE TABLE MemberRewards (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        UserId INT NULL,
                        Title NVARCHAR(200) NOT NULL,
                        Description NVARCHAR(MAX) NOT NULL,
                        Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
                        RewardType NVARCHAR(50) NOT NULL DEFAULT 'Bonus',
                        IsActive BIT NOT NULL DEFAULT 1,
                        CreatedDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                        CONSTRAINT FK_MemberRewards_Users_UserId
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
                    );
                END
            ");

            context.Database.ExecuteSqlRaw(@"
                IF COL_LENGTH('KycDetails', 'AadharNumber') IS NULL ALTER TABLE KycDetails ADD AadharNumber NVARCHAR(12) NOT NULL DEFAULT '';
                IF COL_LENGTH('KycDetails', 'AadharFrontUrl') IS NULL ALTER TABLE KycDetails ADD AadharFrontUrl NVARCHAR(255) NOT NULL DEFAULT '';
                IF COL_LENGTH('KycDetails', 'AadharBackUrl') IS NULL ALTER TABLE KycDetails ADD AadharBackUrl NVARCHAR(255) NOT NULL DEFAULT '';
                IF COL_LENGTH('KycDetails', 'PanNumber') IS NULL ALTER TABLE KycDetails ADD PanNumber NVARCHAR(10) NOT NULL DEFAULT '';
                IF COL_LENGTH('KycDetails', 'BankAccountHolderName') IS NULL ALTER TABLE KycDetails ADD BankAccountHolderName NVARCHAR(100) NOT NULL DEFAULT '';
                IF COL_LENGTH('KycDetails', 'BankAccountNumber') IS NULL ALTER TABLE KycDetails ADD BankAccountNumber NVARCHAR(30) NOT NULL DEFAULT '';
                IF COL_LENGTH('KycDetails', 'BankIfsc') IS NULL ALTER TABLE KycDetails ADD BankIfsc NVARCHAR(11) NOT NULL DEFAULT '';
                IF COL_LENGTH('KycDetails', 'BankName') IS NULL ALTER TABLE KycDetails ADD BankName NVARCHAR(100) NOT NULL DEFAULT '';
            ");
        }

        private static void SeedNewSystemData(ApplicationDbContext context)
        {
            if (!context.MlmSettings.Any())
            {
                context.MlmSettings.Add(new MlmSetting
                {
                    Level1Commission = 10.00m,
                    Level2Commission = 5.00m,
                    Level3Commission = 3.00m,
                    Level4Commission = 2.00m,
                    Level5Commission = 1.00m,
                    MinWithdrawalLimit = 500.00m,
                    WithdrawalFeePercent = 5.00m,
                    CompanyQrCodeUrl = "",
                    BankDetails = "Bank: Cyber Bank India\r\nA/c Name: Elite MLM Technologies\r\nA/c No: 990184719047209\r\nIFSC: CYBR0001084"
                });
                context.SaveChanges();
            }

            if (!context.Packages.Any())
            {
                context.Packages.AddRange(
                    new Package { Name = "Starter Plan", Price = 1000.00m, Description = "Access level 1-5 commissions, binary node placement", IsActive = true },
                    new Package { Name = "Silver Plan", Price = 2500.00m, Description = "Standard growth package with binary nodes unlocked", IsActive = true },
                    new Package { Name = "Gold Plan", Price = 5000.00m, Description = "Premium level unlocks higher capping", IsActive = true }
                );
                context.SaveChanges();
            }

            if (!context.Announcements.Any())
            {
                context.Announcements.Add(new Announcement
                {
                    Title = "Welcome to Elite MLM Network!",
                    Content = "We are thrilled to launch the new dashboard upgrades. Start expanding your network and tracking your earnings today!",
                    IsActive = true
                });
                context.SaveChanges();
            }
        }
    }
}
