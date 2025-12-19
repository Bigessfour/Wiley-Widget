-- Create required departments for Town of Wiley budget
SET IDENTITY_INSERT Departments ON;

INSERT INTO Departments (Id, DepartmentCode, Name, ParentId) VALUES
(3, 'GENERAL', 'General Government', NULL),
(4, 'HIGHWAYS', 'Highways & Streets', NULL),
(5, 'CULTURE', 'Culture & Recreation', NULL);

SET IDENTITY_INSERT Departments OFF;
