using System;
using System.IO;
using FluentAssertions;
using Xunit;
using WileyWidget.WinForms.Plugins;

namespace WileyWidget.WinForms.Tests.Unit.Plugins
{
    public class FileSystemToolsTests
    {
        private static string CreateTestDirectory(out string relativeDir)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            var testDir = Path.Combine(baseDir, "FileSystemToolsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testDir);
            relativeDir = Path.GetRelativePath(baseDir, testDir);
            return testDir;
        }

        [Fact]
        public void ReadFile_ReturnsContent()
        {
            var testDir = CreateTestDirectory(out var relativeDir);
            try
            {
                var filename = Path.Combine(testDir, "sample.txt");
                const string expected = "hello world";
                File.WriteAllText(filename, expected);

                var sut = new FileSystemTools();
                var actual = sut.ReadFile(Path.Combine(relativeDir, "sample.txt"));
                actual.Should().Be(expected);
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        [Fact]
        public void ReadFile_ThrowsWhenPathPointsToDirectory()
        {
            var testDir = CreateTestDirectory(out var relativeDir);
            try
            {
                var sut = new FileSystemTools();
                Action act = () => sut.ReadFile(relativeDir);
                act.Should().Throw<InvalidOperationException>().WithMessage("*directory*");
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        [Fact]
        public void ReadOrList_ReturnsFileContentForFile()
        {
            var testDir = CreateTestDirectory(out var relativeDir);
            try
            {
                var filename = Path.Combine(testDir, "sample.txt");
                const string expected = "hello world";
                File.WriteAllText(filename, expected);

                var sut = new FileSystemTools();
                var actual = sut.ReadOrList(Path.Combine(relativeDir, "sample.txt"));
                actual.Should().Be(expected);
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        [Fact]
        public void ReadOrList_ReturnsListingForDirectory()
        {
            var testDir = CreateTestDirectory(out var relativeDir);
            try
            {
                File.WriteAllText(Path.Combine(testDir, "a.txt"), "a");
                Directory.CreateDirectory(Path.Combine(testDir, "subdir"));

                var sut = new FileSystemTools();
                var result = sut.ReadOrList(relativeDir);
                var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
                var expectedA = Path.GetRelativePath(baseDir, Path.Combine(testDir, "a.txt"));
                var expectedSub = Path.GetRelativePath(baseDir, Path.Combine(testDir, "subdir"));

                result.Should().Contain(expectedA);
                result.Should().Contain(expectedSub);
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        [Fact]
        public void ListDirectory_ReturnsEntriesForDirectory()
        {
            var testDir = CreateTestDirectory(out var relativeDir);
            try
            {
                File.WriteAllText(Path.Combine(testDir, "a.txt"), "a");
                Directory.CreateDirectory(Path.Combine(testDir, "subdir"));

                var sut = new FileSystemTools();
                var items = sut.ListDirectory(relativeDir);

                var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
                var expectedA = Path.GetRelativePath(baseDir, Path.Combine(testDir, "a.txt"));
                var expectedSub = Path.GetRelativePath(baseDir, Path.Combine(testDir, "subdir"));

                items.Should().Contain(expectedA);
                items.Should().Contain(expectedSub);
            }
            finally
            {
                try { Directory.Delete(testDir, true); } catch { }
            }
        }

        [Fact]
        public void ReadFile_ThrowsForPathOutsideBaseDir()
        {
            var sut = new FileSystemTools();
            Action act = () => sut.ReadFile("..\\somefile.txt");
            act.Should().Throw<ArgumentException>().WithMessage("*outside the application root*");
        }
    }
}
