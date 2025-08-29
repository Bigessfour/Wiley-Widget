using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the Enterprise model validation, business logic, and calculated properties
/// </summary>
public class EnterpriseTests
{
    [Fact]
    public void Enterprise_Creation_WithValidData_Succeeds()
    {
        // Arrange & Act
        var enterprise = new Enterprise
        {
            Name = "City Water Department",
            CurrentRate = 2.50m,
            MonthlyExpenses = 15000.00m,
            CitizenCount = 50000
        };

        // Assert
        Assert.Equal("City Water Department", enterprise.Name);
        Assert.Equal(2.50m, enterprise.CurrentRate);
        Assert.Equal(15000.00m, enterprise.MonthlyExpenses);
        Assert.Equal(50000, enterprise.CitizenCount);
        Assert.Equal(125000.00m, enterprise.MonthlyRevenue); // 2.50 * 50000
        Assert.Equal(110000.00m, enterprise.MonthlyBalance); // 125000 - 15000
    }

    [Theory]
    [InlineData("", false)]              // Empty name
    [InlineData(null, false)]           // Null name
    [InlineData("Valid Name", true)]    // Valid name
    [InlineData("A", true)]             // Minimum valid name
    [InlineData("This is a very long enterprise name that exceeds the maximum allowed length of one hundred characters for testing validation purposes", false)] // Too long
    public void Enterprise_Name_Validation(string name, bool shouldBeValid)
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = name,
            CurrentRate = 1.00m,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid, $"Expected valid but got validation errors: {string.Join(", ", validationResults.Select(v => v.ErrorMessage))}");
        }
        else
        {
            Assert.False(isValid, "Expected validation to fail but it passed");
            Assert.NotEmpty(validationResults);
        }
    }

    [Theory]
    [InlineData(0, false)]        // Zero rate
    [InlineData(-1, false)]      // Negative rate
    [InlineData(0.01, true)]     // Minimum valid rate
    [InlineData(1000, true)]     // High rate
    [InlineData(10000, false)]   // Too high rate
    public void Enterprise_CurrentRate_Validation(decimal rate, bool shouldBeValid)
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise",
            CurrentRate = rate,
            MonthlyExpenses = 1000.00m,
            CitizenCount = 1000
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        if (shouldBeValid)
        {
            Assert.True(isValid, $"Expected valid but got validation errors: {string.Join(", ", validationResults.Select(v => v.ErrorMessage))}");
        }
        else
        {
            Assert.False(isValid, "Expected validation to fail but it passed");
            Assert.NotEmpty(validationResults);
        }
    }

    [Theory]
    [InlineData(1.00, 1000, 0, 1000)]     // Rate=1.00, Citizens=1000, Expenses=0, Expected Balance=1000
    [InlineData(2.00, 500, 1000, 0)]     // Rate=2.00, Citizens=500, Expenses=1000, Expected Balance=0
    [InlineData(1.50, 1000, 500, 1000)]  // Rate=1.50, Citizens=1000, Expenses=500, Expected Balance=1000
    [InlineData(0.50, 1000, 1000, -500)] // Rate=0.50, Citizens=1000, Expenses=1000, Expected Balance=-500
    public void Enterprise_CalculatedProperties_WorkCorrectly(decimal currentRate, int citizenCount, decimal expenses, decimal expectedBalance)
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "Test Enterprise",
            CurrentRate = currentRate,
            MonthlyExpenses = expenses,
            CitizenCount = citizenCount
        };

        // Calculate expected revenue based on CitizenCount * CurrentRate
        var expectedRevenue = enterprise.CitizenCount * enterprise.CurrentRate;

        // Act & Assert
        Assert.Equal(expectedRevenue, enterprise.MonthlyRevenue);
        Assert.Equal(expenses, enterprise.MonthlyExpenses);
        Assert.Equal(expectedBalance, enterprise.MonthlyBalance);
    }

    [Fact]
    public void Enterprise_MonthlyRevenue_CalculatesCorrectly()
    {
        // Arrange
        var testCases = new[]
        {
            (rate: 1.00m, citizens: 1000, expected: 1000.00m),
            (rate: 2.50m, citizens: 50000, expected: 125000.00m),
            (rate: 0.50m, citizens: 2000, expected: 1000.00m),
            (rate: 10.00m, citizens: 100, expected: 1000.00m)
        };

        foreach (var (rate, citizens, expected) in testCases)
        {
            // Act
            var enterprise = new Enterprise
            {
                Name = "Test Enterprise",
                CurrentRate = rate,
                MonthlyExpenses = 0,
                CitizenCount = citizens
            };

            // Assert
            Assert.Equal(expected, enterprise.MonthlyRevenue);
        }
    }

    [Fact]
    public void Enterprise_Validation_HandlesMultipleErrors()
    {
        // Arrange
        var enterprise = new Enterprise
        {
            Name = "", // Invalid: empty
            CurrentRate = -1, // Invalid: negative
            MonthlyExpenses = -1000, // Invalid: negative
            CitizenCount = -100 // Invalid: negative
        };

        // Act
        var validationContext = new ValidationContext(enterprise);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(enterprise, validationContext, validationResults, true);

        // Assert
        Assert.False(isValid);
        Assert.True(validationResults.Count >= 4, $"Expected at least 4 validation errors, got {validationResults.Count}");
    }

    [Fact]
    public void Enterprise_DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var enterprise = new Enterprise();

        // Assert
        Assert.Equal(string.Empty, enterprise.Name);
        Assert.Equal(0, enterprise.CurrentRate);
        Assert.Equal(0, enterprise.MonthlyExpenses);
        Assert.Equal(0, enterprise.CitizenCount);
        Assert.Equal(0, enterprise.MonthlyRevenue);
        Assert.Equal(0, enterprise.MonthlyBalance);
    }
}
