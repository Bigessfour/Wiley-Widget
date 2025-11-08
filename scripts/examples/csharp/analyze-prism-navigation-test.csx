#!/usr/bin/env dotnet-script
#r "nuget: Prism.Core, 9.0.537"
#r "nuget: Moq, 4.20.72"
#r "nuget: xunit, 2.9.2"

using System;
using System.Linq.Expressions;
using Moq;
using Prism.Navigation.Regions;

/*
 * C# MCP Script: Analyze Prism Navigation Testing Pattern
 *
 * Purpose: Demonstrate how to properly mock and verify Prism's RequestNavigate
 * extension method in unit tests, since Moq cannot verify extension methods directly.
 *
 * Key Insight: RequestNavigate is an extension method on IRegionManager that
 * internally accesses regions and their navigation services. We must mock the
 * underlying implementation, not the extension.
 */

Console.WriteLine("=== Prism Navigation Testing Analysis ===\n");

// Problem: This fails because RequestNavigate is an extension method
Console.WriteLine("❌ INCORRECT APPROACH:");
Console.WriteLine(@"
mockNavigationService.Verify(
    ns => ns.RequestNavigate(It.IsAny<Uri>(), It.IsAny<Action<NavigationResult>>()),
    Times.Once);
");
Console.WriteLine("Error: Moq cannot verify extension methods\n");

// Solution 1: Verify the underlying implementation
Console.WriteLine("✅ SOLUTION 1: Verify Region Access");
Console.WriteLine(@"
// Setup
var mockRegion = new Mock<IRegion>();
var mockRegionCollection = new Mock<IRegionCollection>();
mockRegionCollection.Setup(rc => rc[""MainRegion""]).Returns(mockRegion.Object);

// Verify the region was accessed (what the extension method does)
mockRegionCollection.Verify(rc => rc[""MainRegion""], Times.AtLeastOnce);
");

// Solution 2: Mock IRegionManager.RequestNavigate directly if possible
Console.WriteLine("\n✅ SOLUTION 2: Setup for Successful Navigation");
Console.WriteLine(@"
// Setup complete navigation chain so extension method succeeds
var mockNavigationService = new Mock<IRegionNavigationService>();
var mockRegion = new Mock<IRegion>();
mockRegion.SetupGet(r => r.NavigationService).Returns(mockNavigationService.Object);

var mockRegionCollection = new Mock<IRegionCollection>();
mockRegionCollection.Setup(rc => rc[""MainRegion""]).Returns(mockRegion.Object);
mockRegionCollection.Setup(rc => rc.ContainsRegionWithName(""MainRegion"")).Returns(true);

_mockRegionManager.Setup(rm => rm.Regions).Returns(mockRegionCollection.Object);

// Test executes - navigation completes without exception
// Verify by checking side effects or region access
");

// Solution 3: Use actual implementation with test doubles
Console.WriteLine("\n✅ SOLUTION 3: Integration Test Approach");
Console.WriteLine(@"
// Use RegionManager with actual implementation, stub only navigation behavior
var regionManager = new RegionManager();
var region = new Region { Name = ""MainRegion"" };
regionManager.Regions.Add(region);

// Inject test navigation service
var mockNavigationService = new Mock<IRegionNavigationService>();
region.NavigationService = mockNavigationService.Object;

// Now you can verify the concrete method on the navigation service
");

Console.WriteLine("\n=== Test Pattern Recommendation ===");
Console.WriteLine(@"
[Fact]
public void NavigateCommand_UsesRegionManager()
{
    // Arrange
    var viewModel = CreateViewModelWithTestEventAggregator();
    var mockRegion = new Mock<IRegion>();
    var mockRegionCollection = new Mock<IRegionCollection>();
    var mockNavigationService = new Mock<IRegionNavigationService>();

    // Setup navigation chain
    mockRegionCollection
        .Setup(rc => rc.ContainsRegionWithName(""MainRegion""))
        .Returns(true);
    mockRegionCollection
        .Setup(rc => rc[""MainRegion""])
        .Returns(mockRegion.Object);
    _mockRegionManager
        .Setup(rm => rm.Regions)
        .Returns(mockRegionCollection.Object);
    mockRegion
        .SetupGet(r => r.NavigationService)
        .Returns(mockNavigationService.Object);

    // Act
    viewModel.OpenBudgetAnalysisCommand.Execute();

    // Assert - Verify region access (proves RequestNavigate was called)
    mockRegionCollection.Verify(rc => rc[""MainRegion""], Times.AtLeastOnce);

    // OR verify Regions property was accessed
    _mockRegionManager.Verify(rm => rm.Regions, Times.AtLeastOnce);
}
");

Console.WriteLine("\n✅ Analysis complete - Use region access verification pattern");
