-- Create required departments for Town of Wiley budget
SET IDENTITY_INSERT Departments ON;

INSERT INTO Departments (Id, Code, Name, Fund, ParentDepartmentId) VALUES
(3, 0, 'General Government', 0, NULL), (, (4, 'HIGHWAYS', 1, 0, NULL)), (, (5, 'CULTURE', 9, 0, NULL));

SET IDENTITY_INSERT Departments OFF;
