-- Create required departments for Town of Wiley budget
SET IDENTITY_INSERT Departments ON;

INSERT INTO Departments (Id, DepartmentCode, Name, ParentId) VALUES
(1, 'ADMIN', 'Administration', NULL),
(2, 'DPW', 'Public Works', NULL),
(3, 'CULT', 'Culture and Recreation', NULL),
(4, 'SAN', 'Sanitation', 2),
(5, 'UTIL', 'Utilities', NULL),
(6, 'COMM', 'Community Center', NULL),
(7, 'CONS', 'Conservation', NULL),
(8, 'REC', 'Recreation', NULL);

SET IDENTITY_INSERT Departments OFF;
