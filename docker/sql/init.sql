-- docker/sql/init.sql
IF NOT EXISTS(SELECT name FROM master.dbo.sysdatabases WHERE name = 'GeoIPDb')
BEGIN
    CREATE DATABASE GeoIPDb;
    PRINT 'Database GeoIPDb created successfully';
END
GO

USE GeoIPDb;
GO

-- Enable snapshot isolation for better performance
ALTER DATABASE GeoIPDb SET ALLOW_SNAPSHOT_ISOLATION ON;
ALTER DATABASE GeoIPDb SET READ_COMMITTED_SNAPSHOT ON;
GO