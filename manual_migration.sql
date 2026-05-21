ALTER TABLE Packages ADD RoiPercentage decimal(18, 2) NOT NULL DEFAULT 0.0;
ALTER TABLE Packages ADD RoiDurationDays int NOT NULL DEFAULT 0;

ALTER TABLE ActivationRequests ADD PackageId int NULL;
ALTER TABLE ActivationRequests ADD CONSTRAINT FK_ActivationRequests_Packages_PackageId FOREIGN KEY (PackageId) REFERENCES Packages(Id) ON DELETE NO ACTION;

CREATE TABLE UserPackages (
    Id int NOT NULL IDENTITY,
    UserId int NOT NULL,
    PackageId int NOT NULL,
    Amount decimal(18, 2) NOT NULL,
    RoiPercentage decimal(18, 2) NOT NULL,
    RoiDurationDays int NOT NULL,
    DaysPaid int NOT NULL DEFAULT 0,
    IsActive bit NOT NULL DEFAULT 1,
    ActivationDate datetime2 NOT NULL,
    CONSTRAINT PK_UserPackages PRIMARY KEY (Id),
    CONSTRAINT FK_UserPackages_Packages_PackageId FOREIGN KEY (PackageId) REFERENCES Packages (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserPackages_Users_UserId FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE INDEX IX_UserPackages_PackageId ON UserPackages (PackageId);
CREATE INDEX IX_UserPackages_UserId ON UserPackages (UserId);
CREATE INDEX IX_ActivationRequests_PackageId ON ActivationRequests (PackageId);
