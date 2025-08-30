using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for BudgetInteraction model validation and business logic
/// </summary>
public class BudgetInteractionTests
{
    [Fact]
    public void BudgetInteraction_Creation_WithValidData_Succeeds()
    {
        // Arrange & Act
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Shared maintenance costs",
            MonthlyAmount = 500.00m,
            IsCost = true,
            Notes = "Quarterly maintenance sharing"
        };

        // Assert
        Assert.Equal(1, interaction.PrimaryEnterpriseId);
        Assert.Equal("SharedCost", interaction.InteractionType);
        Assert.Equal("Shared maintenance costs", interaction.Description);
        Assert.Equal(500.00m, interaction.MonthlyAmount);
        Assert.True(interaction.IsCost);
        Assert.Equal("Quarterly maintenance sharing", interaction.Notes);
    }

    [Theory]
    [InlineData("", false)]           // Empty type
    [InlineData(null, false)]        // Null type
    [InlineData("SharedCost", true)] // Valid type
    [InlineData("Dependency", true)] // Valid type
    [InlineData("Transfer", true)]   // Valid type
    public void BudgetInteraction_InteractionType_Validation(string type, bool shouldBeValid)
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = type,
            Description = "Test interaction",
            MonthlyAmount = 100.00m,
            IsCost = true
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.Contains(validationResults, r => r.ErrorMessage.Contains("required"));
        }
    }

    [Theory]
    [InlineData("", false)]                    // Empty description
    [InlineData(null, false)]                 // Null description
    [InlineData("Valid description", true)]   // Valid description
    [InlineData("A", true)]                   // Minimum valid description
    public void BudgetInteraction_Description_Validation(string description, bool shouldBeValid)
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = description,
            MonthlyAmount = 100.00m,
            IsCost = true
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.Contains(validationResults, r => r.ErrorMessage.Contains("required"));
        }
    }

    [Theory]
    [InlineData(0, false)]      // Zero amount (invalid - no free rides)
    [InlineData(0.01, true)]    // Minimum positive amount
    [InlineData(1000.99, true)] // Valid amount
    [InlineData(-1, false)]     // Negative amount
    public void BudgetInteraction_MonthlyAmount_Validation(decimal amount, bool shouldBeValid)
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Test interaction",
            MonthlyAmount = amount,
            IsCost = true
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.Contains(validationResults, r => r.ErrorMessage.Contains("Monthly amount"));
        }
    }

    [Theory]
    [InlineData("SharedCost", true)]
    [InlineData("Dependency", true)]
    [InlineData("Transfer", true)]
    [InlineData("Subsidy", true)]
    public void BudgetInteraction_AcceptsVariousInteractionTypes(string type, bool expected)
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = type,
            Description = "Test interaction",
            MonthlyAmount = 100.00m,
            IsCost = true
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public void BudgetInteraction_IsCost_DefaultsToTrue()
    {
        // Arrange & Act
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Test interaction",
            MonthlyAmount = 100.00m
        };

        // Assert
        Assert.True(interaction.IsCost);
    }

    [Fact]
    public void BudgetInteraction_IsCost_CanBeSetToFalse()
    {
        // Arrange & Act
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "Transfer",
            Description = "Revenue transfer",
            MonthlyAmount = 200.00m,
            IsCost = false
        };

        // Assert
        Assert.False(interaction.IsCost);
    }

    [Theory]
    [InlineData("Normal length notes", true)]
    [InlineData("", true)] // Empty notes allowed
    [InlineData(null, true)] // Null notes allowed
    public void BudgetInteraction_Notes_Validation(string notes, bool shouldBeValid)
    {
        // Arrange
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            InteractionType = "SharedCost",
            Description = "Test interaction",
            MonthlyAmount = 100.00m,
            IsCost = true,
            Notes = notes
        };

        // Act
        var validationContext = new ValidationContext(interaction);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(interaction, validationContext, validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
    }

    [Fact]
    public void BudgetInteraction_SecondaryEnterpriseId_CanBeNull()
    {
        // Arrange & Act
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            SecondaryEnterpriseId = null,
            InteractionType = "SharedCost",
            Description = "Enterprise-specific cost",
            MonthlyAmount = 100.00m,
            IsCost = true
        };

        // Assert
        Assert.Null(interaction.SecondaryEnterpriseId);
    }

    [Fact]
    public void BudgetInteraction_SecondaryEnterpriseId_CanBeSet()
    {
        // Arrange & Act
        var interaction = new BudgetInteraction
        {
            PrimaryEnterpriseId = 1,
            SecondaryEnterpriseId = 2,
            InteractionType = "Dependency",
            Description = "Inter-enterprise dependency",
            MonthlyAmount = 150.00m,
            IsCost = true
        };

        // Assert
        Assert.Equal(2, interaction.SecondaryEnterpriseId);
    }
}
