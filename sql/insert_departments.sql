-- Create required departments for Town of Wiley budget
SET IDENTITY_INSERT Departments ON;

INSERT INTO Departments (Id, Code, Name, Fund, ParentDepartmentId) VALUES
(3, 'GENERAL', 'General Government', 'General', NULL),
(4, 'HIGHWAYS', 'Highways & Streets', 'General', NULL),
(5, 'CULTURE', 'Culture & Recreation', 'General', NULL);

SET IDENTITY_INSERT Departments OFF;
