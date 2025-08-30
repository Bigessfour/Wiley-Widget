using System.ComponentModel.DataAnnotations;
using AutoFixture;
using AutoFixture.Xunit2;
using FluentAssertions;
using Xunit;
using WileyWidget.Models;

namespace WileyWidget.Tests.EntityValidationTests;

/// <summary>
/// Custom AutoData attribute that uses our configured fixture with circular reference handling
/// </summary>
public class CustomAutoDataAttribute : AutoDataAttribute
{
    public CustomAutoDataAttribute()
        : base(() =>
        {
            var fixture = new Fixture();

            // Handle circular references by omitting them instead of throwing
            fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => fixture.Behaviors.Remove(b));
            fixture.Behaviors.Add(new OmitOnRecursionBehavior());

            // Customize AutoFixture for better test data generation
            fixture.Customize<Enterprise>(composer => composer
                .With(e => e.Name, () => new string(Enumerable.Range(0, 50).Select(_ => fixture.Create<char>()).ToArray()).Substring(0, 50))
                .With(e => e.CurrentRate, () => Math.Round(fixture.Create<decimal>() % 1000, 2))
                .With(e => e.CitizenCount, () => Math.Abs(fixture.Create<int>()) % 10000)
                .With(e => e.MonthlyExpenses, () => Math.Round(Math.Abs(fixture.Create<decimal>()) % 100000, 2)));

            fixture.Customize<Widget>(composer => composer
                .With(w => w.Name, () => new string(Enumerable.Range(0, 100).Select(_ => fixture.Create<char>()).ToArray()).Substring(0, 100))
                .With(w => w.Category, () => new string(Enumerable.Range(0, 50).Select(_ => fixture.Create<char>()).ToArray()).Substring(0, 50))
                .With(w => w.Price, () => Math.Round(Math.Abs(fixture.Create<decimal>()) % 10000, 2)));

            return fixture;
        })
    {
    }
}

/// <summary>
/// Comprehensive entity validation tests using industry best practices
/// </summary>
public class EntityValidationTests
{
    private readonly Fixture _fixture;

    public EntityValidationTests()
    {
        _fixture = new Fixture();

        // Handle circular references by omitting them instead of throwing
        _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => _fixture.Behaviors.Remove(b));
        _fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        // Customize AutoFixture for better test data generation
        _fixture.Customize<Enterprise>(composer => composer
            .With(e => e.Name, () => _fixture.Create<string>().Substring(0, 50)) // Reasonable name length
            .With(e => e.CurrentRate, () => Math.Round(_fixture.Create<decimal>() % 1000, 2)) // Positive rates
            .With(e => e.CitizenCount, () => Math.Abs(_fixture.Create<int>()) % 10000) // Positive citizen count
            .With(e => e.MonthlyExpenses, () => Math.Round(Math.Abs(_fixture.Create<decimal>()) % 100000, 2)));

        _fixture.Customize<Widget>(composer => composer
            .With(w => w.Name, () => _fixture.Create<string>().Substring(0, 100))
            .With(w => w.Category, () => _fixture.Create<string>().Substring(0, 50))
            .With(w => w.Price, () => Math.Round(Math.Abs(_fixture.Create<decimal>()) % 10000, 2)));
    }

    #region Enterprise Entity Tests

    [Theory]
    [CustomAutoData]
    public void Enterprise_WithValidData_ShouldPassValidation(Enterprise enterprise)
    {
        // Arrange - AutoFixture generates valid data based on our customizations

        // Act
        var validationResults = ValidateModel(enterprise);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void Enterprise_WithEmptyName_ShouldFailValidation()
    {
        // Arrange
        var enterprise = _fixture.Build<Enterprise>()
            .With(e => e.Name, string.Empty)
            .Create();

        // Act
        var validationResults = ValidateModel(enterprise);

        // Assert
        validationResults.Should().Contain(r => r.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Enterprise_WithInvalidCurrentRate_ShouldFailValidation(decimal invalidRate)
    {
        // Arrange
        var enterprise = _fixture.Build<Enterprise>()
            .With(e => e.CurrentRate, invalidRate)
            .Create();

        // Act
        var validationResults = ValidateModel(enterprise);

        // Assert
        validationResults.Should().Contain(r => r.ErrorMessage.Contains("rate", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Enterprise_WithInvalidCitizenCount_ShouldFailValidation(int invalidCount)
    {
        // Arrange
        var enterprise = _fixture.Build<Enterprise>()
            .With(e => e.CitizenCount, invalidCount)
            .Create();

        // Act
        var validationResults = ValidateModel(enterprise);

        // Assert
        validationResults.Should().Contain(r => r.ErrorMessage.Contains("at least 1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Enterprise_WithExcessiveNameLength_ShouldFailValidation()
    {
        // Arrange
        var longName = new string('A', 201); // Assuming max length is 200
        var enterprise = _fixture.Build<Enterprise>()
            .With(e => e.Name, longName)
            .Create();

        // Act
        var validationResults = ValidateModel(enterprise);

        // Assert
        validationResults.Should().Contain(r => r.ErrorMessage.Contains("exceed", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Widget Entity Tests

    [Theory]
    [CustomAutoData]
    public void Widget_WithValidData_ShouldPassValidation(Widget widget)
    {
        // Arrange - AutoFixture generates valid data

        // Act
        var validationResults = ValidateModel(widget);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void Widget_WithNegativePrice_ShouldFailValidation()
    {
        // Arrange
        var widget = _fixture.Build<Widget>()
            .With(w => w.Price, -100m)
            .Create();

        // Act
        var validationResults = ValidateModel(widget);

        // Assert
        validationResults.Should().Contain(r => r.ErrorMessage.Contains("price", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Widget_WithEmptyName_ShouldFailValidation()
    {
        // Arrange
        var widget = _fixture.Build<Widget>()
            .With(w => w.Name, string.Empty)
            .Create();

        // Act
        var validationResults = ValidateModel(widget);

        // Assert
        validationResults.Should().Contain(r => r.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Helper Methods

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    #endregion
}
