# DI Container TargetInvocationException Troubleshooting Guide

## Overview

This guide provides comprehensive solutions for diagnosing and resolving `System.Reflection.TargetInvocationException` in DI container scenarios with Prism WPF applications. These exceptions often hide the real cause deep in the InnerException chain.

## Common Causes

### 1. Missing Service Registrations

**Symptoms**: `ResolutionFailedException` wrapped in `TargetInvocationException`
**Common in**: ViewModel constructors, module initialization

### 2. Circular Dependencies

**Symptoms**: StackOverflowException or ResolutionFailedException
**Common in**: Services with mutual dependencies

### 3. Invalid Constructor Parameters

**Symptoms**: Type resolution failures
**Common in**: Complex object graphs with optional dependencies

### 4. Module Initialization Failures

**Symptoms**: `ModuleInitializeException` during startup
**Common in**: Prism module OnInitialized methods

## Diagnostic Tools

### 1. Enhanced Global Exception Handling

The application includes comprehensive exception unwrapping:

```csharp
private static Exception UnwrapTargetInvocationException(Exception? exception)
{
    var current = exception;
    var seen = new HashSet<Exception>();

    // Unwrap TargetInvocationException chain
    while (current is TargetInvocationException tie && tie.InnerException != null && !seen.Contains(tie))
    {
        seen.Add(tie);
        current = tie.InnerException;
    }

    return current;
}
```

### 2. Container Resolution Diagnostics

In DEBUG mode, the application automatically tests critical service resolutions:

```csharp
var diagnosticReport = ContainerDiagnostics.TestContainerResolutions(containerRegistry, testMode);
```

### 3. Exception Chain Logging

All exceptions are logged with full chain analysis.

## Resolution Strategies

### Strategy 1: Check Service Registrations

**Problem**: Services not registered in `RegisterTypes()`

**Solution**:

1. Check that all required interfaces are registered
2. Verify registration order (dependencies first)
3. Use container diagnostics to identify missing registrations

### Strategy 2: Resolve Circular Dependencies

**Problem**: Services have circular constructor dependencies

**Solutions**:

1. Use property injection instead of constructor injection
2. Introduce a third service to break the cycle
3. Use lazy resolution

### Strategy 3: Handle Optional Dependencies

**Problem**: Services fail when optional dependencies are missing

**Solution**: use named or conditional registrations to handle optional services.

### Strategy 4: Debug Module Initialization

**Problem**: Modules fail during initialization

**Solution**: ensure module dependencies are declared and services available before module OnInitialized runs.

## Testing Container Registrations

Manual and automated tests can validate container registrations. Use ContainerDiagnostics.TestContainerResolutions and runtime validation in ValidateCriticalServices.

## Syncfusion and AI Integration Issues

### Syncfusion License Issues

See application startup code for license resolution logic; ensure environment variables or configuration keys are present.

---

This document renames and replaces references to legacy container names to maintain neutral, container-agnostic guidance.
