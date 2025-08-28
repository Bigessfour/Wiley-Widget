using System.ComponentModel.DataAnnotations;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests;

/// <summary>
/// Comprehensive tests for the Widget model validation, business logic, and methods
/// </summary>
public class WidgetTests
{
    [Fact]
    public void Widget_Creation_WithValidData_Succeeds()
    {
        // Arrange & Act
        var widget = new Widget
        {
            Name = "Test Widget",
            Description = "A test widget",
            Price = 29.99m,
            Quantity = 10,
            Category = "Electronics",
            SKU = "TW-001"
        };

        // Assert
        Assert.Equal("Test Widget", widget.Name);
        Assert.Equal("A test widget", widget.Description);
        Assert.Equal(29.99m, widget.Price);
        Assert.Equal(10, widget.Quantity);
        Assert.Equal("Electronics", widget.Category);
        Assert.Equal("TW-001", widget.SKU);
        Assert.True(widget.IsActive); // Default value
        Assert.True(widget.CreatedDate <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData("", false)]           // Empty name
    [InlineData(null, false)]        // Null name
    [InlineData("Valid Name", true)] // Valid name
    [InlineData("A", true)]          // Minimum valid name
    public void Widget_Name_Validation(string name, bool shouldBeValid)
    {
        // Arrange
        var widget = new Widget { Name = name, Price = 10.00m };

        // Act
        var validationContext = new ValidationContext(widget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(widget, validationContext, validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.Contains(validationResults, r => r.ErrorMessage?.Contains("required") == true);
        }
    }

    [Theory]
    [InlineData(0, false)]      // Zero price
    [InlineData(-1, false)]     // Negative price
    [InlineData(0.01, true)]    // Minimum valid price
    [InlineData(1000.99, true)] // Valid price
    public void Widget_Price_Validation(decimal price, bool shouldBeValid)
    {
        // Arrange
        var widget = new Widget
        {
            Name = "Test Widget",
            Price = price
        };

        // Act
        var validationContext = new ValidationContext(widget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(widget, validationContext, validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.Contains(validationResults, r => r.ErrorMessage?.Contains("greater than 0") == true);
        }
    }

    [Theory]
    [InlineData(-1, false)]     // Negative quantity
    [InlineData(0, true)]      // Zero quantity
    [InlineData(100, true)]    // Valid quantity
    public void Widget_Quantity_Validation(int quantity, bool shouldBeValid)
    {
        // Arrange
        var widget = new Widget
        {
            Name = "Test Widget",
            Price = 10.00m,
            Quantity = quantity
        };

        // Act
        var validationContext = new ValidationContext(widget);
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(widget, validationContext, validationResults, true);

        // Assert
        Assert.Equal(shouldBeValid, isValid);
        if (!shouldBeValid)
        {
            Assert.Contains(validationResults, r => r.ErrorMessage?.Contains("cannot be negative") == true);
        }
    }

    [Fact]
    public void Widget_MarkAsModified_UpdatesModifiedDate()
    {
        // Arrange
        var widget = new Widget
        {
            Name = "Test Widget",
            Price = 10.00m
        };
        var originalModifiedDate = widget.ModifiedDate;

        // Act
        widget.MarkAsModified();

        // Assert
        Assert.NotNull(widget.ModifiedDate);
        Assert.True(widget.ModifiedDate > originalModifiedDate);
        Assert.True(widget.ModifiedDate <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(10.00, "$10.00")]
    [InlineData(99.99, "$99.99")]
    [InlineData(0.01, "$0.01")]
    [InlineData(1234.56, "$1,234.56")]
    public void Widget_FormattedPrice_ReturnsCorrectFormat(decimal price, string expected)
    {
        // Arrange
        var widget = new Widget
        {
            Name = "Test Widget",
            Price = price
        };

        // Act & Assert
        Assert.Equal(expected, widget.FormattedPrice);
    }

    [Theory]
    [InlineData("Widget Name", "", "Widget Name")]
    [InlineData("Widget Name", "SKU-001", "Widget Name (SKU-001)")]
    [InlineData("Widget Name", null, "Widget Name")]
    [InlineData("Widget Name", "   ", "Widget Name")]
    public void Widget_DisplayName_ReturnsCorrectFormat(string name, string sku, string expected)
    {
        // Arrange
        var widget = new Widget
        {
            Name = name,
            SKU = sku,
            Price = 10.00m
        };

        // Act & Assert
        Assert.Equal(expected, widget.DisplayName);
    }

    [Fact]
    public void Widget_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var widget = new Widget();

        // Assert
        Assert.Equal(string.Empty, widget.Name);
        Assert.Equal(string.Empty, widget.Description);
        Assert.Equal(0m, widget.Price);
        Assert.Equal(0, widget.Quantity);
        Assert.True(widget.IsActive);
        Assert.True(widget.CreatedDate <= DateTime.UtcNow);
        Assert.Null(widget.ModifiedDate);
        Assert.Equal(string.Empty, widget.Category);
        Assert.Equal(string.Empty, widget.SKU);
    }

    [Theory]
    [InlineData("Electronics")]
    [InlineData("Books")]
    [InlineData("Clothing")]
    [InlineData("Home & Garden")]
    public void Widget_Category_AcceptsVariousValues(string category)
    {
        // Arrange & Act
        var widget = new Widget
        {
            Name = "Test Widget",
            Price = 10.00m,
            Category = category
        };

        // Assert
        Assert.Equal(category, widget.Category);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Widget_IsActive_CanBeSet(bool isActive)
    {
        // Arrange & Act
        var widget = new Widget
        {
            Name = "Test Widget",
            Price = 10.00m,
            IsActive = isActive
        };

        // Assert
        Assert.Equal(isActive, widget.IsActive);
    }
}
