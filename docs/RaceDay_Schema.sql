-- =============================================
-- RaceDay System - Database Creation Script
-- =============================================

-- Create the database (Uncomment if needed)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'RaceDayDB')
BEGIN
    EXEC('CREATE DATABASE RaceDayDB');
END
GO
USE RaceDayDB;
GO

-- Drop tables if they exist to avoid errors on rerun
DROP TABLE IF EXISTS Results;
DROP TABLE IF EXISTS Enrolments;
DROP TABLE IF EXISTS EventCategories;
DROP TABLE IF EXISTS Events;
DROP TABLE IF EXISTS Categories;
DROP TABLE IF EXISTS Participants;
DROP TABLE IF EXISTS Organisers;

-- 1. Create Organisers Table
CREATE TABLE Organisers (
    OrganiserID INT IDENTITY(1,1) PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL
);

-- 2. Create Participants Table
CREATE TABLE Participants (
    ParticipantID INT IDENTITY(1,1) PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    Age INT NOT NULL,
    Gender VARCHAR(20) NOT NULL
);

-- 3. Create Events Table
CREATE TABLE Events (
    EventID INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID INT NOT NULL,
    Name VARCHAR(200) NOT NULL,
    EventDate DATE NOT NULL,
    Location VARCHAR(200) NOT NULL,
    Description TEXT,
    CONSTRAINT FK_Events_Organisers FOREIGN KEY (OrganiserID) REFERENCES Organisers(OrganiserID)
);

-- 4. Create Categories Table
CREATE TABLE Categories (
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Description TEXT
);

-- 5. Create EventCategories Table (Many-to-Many resolution between Events and Categories)
CREATE TABLE EventCategories (
    EventID INT NOT NULL,
    CategoryID INT NOT NULL,
    PRIMARY KEY (EventID, CategoryID),
    CONSTRAINT FK_EventCategories_Events FOREIGN KEY (EventID) REFERENCES Events(EventID),
    CONSTRAINT FK_EventCategories_Categories FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID)
);

-- 6. Create Enrolments Table
CREATE TABLE Enrolments (
    EnrolmentID INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID INT NOT NULL,
    EventID INT NOT NULL,
    CategoryID INT NOT NULL,
    Status VARCHAR(50) NOT NULL DEFAULT 'Registered',
    RegistrationDate DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_Enrolments_Participants FOREIGN KEY (ParticipantID) REFERENCES Participants(ParticipantID),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventID) REFERENCES Events(EventID),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID)
);

-- 7. Create Results Table
CREATE TABLE Results (
    ResultID INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID INT NOT NULL UNIQUE,
    FinishTime TIME(0) NOT NULL,
    Position INT,
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID) REFERENCES Enrolments(EnrolmentID)
);

-- =============================================
-- Insert Sample Data
-- =============================================

-- Insert 2 Organisers
INSERT INTO Organisers (Name, Email, PasswordHash) VALUES 
('John Doe Athletics', 'johndoe@example.com', 'hash123'),
('City Marathons Inc', 'citymarathons@example.com', 'hash456');

-- Insert 2 Participants
INSERT INTO Participants (Name, Email, PasswordHash, Age, Gender) VALUES 
('Alice Smith', 'alice@example.com', 'hash789', 28, 'Female'),
('Bob Johnson', 'bob@example.com', 'hash101', 35, 'Male');

-- Insert Categories
INSERT INTO Categories (Name, Description) VALUES 
('5K Run', 'A 5 kilometer run suitable for beginners.'),
('10K Run', 'A 10 kilometer run for intermediate runners.'),
('Half Marathon', '21.0975 kilometers.'),
('Full Marathon', '42.195 kilometers.');

-- Insert 3 Events
INSERT INTO Events (OrganiserID, Name, EventDate, Location, Description) VALUES 
(1, 'Spring 5K Dash', '2027-04-15', 'Central Park', 'Annual spring dash in the park.'),
(2, 'City Annual Marathon', '2027-09-10', 'Downtown City', 'The biggest marathon in the city.'),
(1, 'Winter Charity 10K', '2027-12-05', 'Riverside Trail', 'Charity run for local hospitals.');

-- Associate Categories with Events
INSERT INTO EventCategories (EventID, CategoryID) VALUES 
(1, 1), -- Spring 5K Dash has 5K Run
(2, 3), -- City Annual Marathon has Half Marathon
(2, 4), -- City Annual Marathon has Full Marathon
(3, 2); -- Winter Charity 10K has 10K Run

-- Insert Enrolments
INSERT INTO Enrolments (ParticipantID, EventID, CategoryID, Status) VALUES 
(1, 1, 1, 'Registered'),
(2, 2, 4, 'Registered'),
(1, 3, 2, 'Registered');

-- Insert Results
INSERT INTO Results (EnrolmentID, FinishTime, Position) VALUES 
(1, '00:25:30', 1),
(3, '00:55:10', 3);

-- Index for Email
CREATE INDEX IX_Organisers_Email ON Organisers(Email);
-- Sample Query
-- SELECT * FROM Events;
-- End of script
-- Checked
