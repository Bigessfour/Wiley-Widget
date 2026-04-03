using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using WileyWidget.Abstractions;
using Xunit;

namespace WileyWidget.LayerProof.Tests.Abstractions;

[Trait("Category", "LayerProof")]
[Trait("Category", "Abstractions")]
public sealed class AbstractionsContractProofTests
{
    [Fact]
    public void Result_Factories_PreserveSuccessAndFailureContracts()
    {
        var success = Result.Success();
        var failure = Result.Failure("boom");

        success.IsSuccess.Should().BeTrue();
        success.ErrorMessage.Should().BeNull();

        failure.IsSuccess.Should().BeFalse();
        failure.ErrorMessage.Should().Be("boom");
    }

    [Fact]
    public void GenericResult_Factories_PreservePayloadAndErrorContracts()
    {
        var success = Result<string>.Success("payload");
        var emptySuccess = Result<string>.Success();
        var failure = Result<string>.Failure("failed", new InvalidOperationException("details"));

        success.IsSuccess.Should().BeTrue();
        success.Data.Should().Be("payload");
        success.ErrorMessage.Should().BeNull();

        emptySuccess.IsSuccess.Should().BeTrue();
        emptySuccess.Data.Should().BeNull();

        failure.IsSuccess.Should().BeFalse();
        failure.Data.Should().BeNull();
        failure.ErrorMessage.Should().Contain("failed").And.Contain("details");
    }

    [Fact]
    public void ResourceLoadContracts_ExposeExpectedStateAndDiagnostics()
    {
        var result = new ResourceLoadResult
        {
            Success = true,
            LoadedCount = 3,
            ErrorCount = 1,
            RetryCount = 2,
            LoadTimeMs = 120,
            HasCriticalFailures = false,
        };
        result.LoadedPaths.Add("config/a.json");
        result.FailedPaths.Add("config/b.json");
        result.Diagnostics["attempts"] = 5;

        var exception = new ResourceLoadException("load failed", new System.Collections.Generic.List<string> { "config/b.json" }, true);

        result.ToString().Should().Contain("Success=True").And.Contain("Loaded=3");
        exception.FailedResources.Should().ContainSingle().Which.Should().Be("config/b.json");
        exception.IsCritical.Should().BeTrue();
    }

    [Fact]
    public void RegionValidationResult_ToString_SummarizesValidationState()
    {
        var validation = new RegionValidationResult
        {
            IsValid = true,
            TotalRegions = 4,
            ValidRegionsCount = 4,
        };

        validation.ToString().Should().Contain("4/4 valid").And.Contain("IsValid: True");
    }

    [Fact]
    public void AbstractionsInterfaces_ExposeExpectedContracts()
    {
        var asyncInitialize = typeof(IAsyncInitializable).GetMethod(nameof(IAsyncInitializable.InitializeAsync));
        var saveState = typeof(IApplicationStateService).GetMethod(nameof(IApplicationStateService.SaveStateAsync));
        var restoreState = typeof(IApplicationStateService).GetMethod(nameof(IApplicationStateService.RestoreStateAsync));
        var clearState = typeof(IApplicationStateService).GetMethod(nameof(IApplicationStateService.ClearStateAsync));
        var getOrCreate = typeof(ICacheService).GetMethods().Single(method => method.Name == nameof(ICacheService.GetOrCreateAsync));
        var registerAllViews = typeof(IViewRegistrationService).GetMethod(nameof(IViewRegistrationService.RegisterAllViews));

        asyncInitialize.Should().NotBeNull();
        asyncInitialize!.ReturnType.Should().Be(typeof(Task));
        asyncInitialize.GetParameters().Single().ParameterType.Should().Be(typeof(CancellationToken));

        saveState.Should().NotBeNull();
        saveState!.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().ContainInOrder(typeof(object), typeof(CancellationToken));

        restoreState.Should().NotBeNull();
        restoreState!.ReturnType.Should().Be(typeof(Task<object>));

        clearState.Should().NotBeNull();
        clearState!.GetParameters().Single().ParameterType.Should().Be(typeof(CancellationToken));

        getOrCreate.IsGenericMethod.Should().BeTrue();
        getOrCreate.ReturnType.Name.Should().Be("Task`1");

        registerAllViews.Should().NotBeNull();
        registerAllViews!.GetCustomAttribute<ObsoleteAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void ErrorHandlingInterfaces_RemainAligned()
    {
        var errorHandlerMethods = typeof(IErrorHandler).GetMethods().Select(DescribeSignature).OrderBy(signature => signature).ToArray();
        var exceptionHandlerMethods = typeof(IExceptionHandler).GetMethods().Select(DescribeSignature).OrderBy(signature => signature).ToArray();

        errorHandlerMethods.Should().Equal(exceptionHandlerMethods);
    }

    private static string DescribeSignature(MethodInfo method)
    {
        var parameterList = string.Join(",", method.GetParameters().Select(parameter => parameter.ParameterType.FullName));
        return $"{method.Name}:{method.ReturnType.FullName}({parameterList})";
    }
}
