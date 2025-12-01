// Disable NUnit parallel test execution to keep docking and WinForms UI tests deterministic
[assembly: NUnit.Framework.Parallelizable(NUnit.Framework.ParallelScope.None)]
