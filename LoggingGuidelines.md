# Serilog Logging Guidelines for SynQPanel

This document defines the logging standards for the SynQPanel application using Serilog.

---

## Logger Setup

Each class should declare its own logger instance to include the class name in log output:

```csharp
private static readonly ILogger Logger = Log.ForContext<ClassName>();

```

This ensures that the [SourceContext] property in logs includes the class name, making it easier to identify the source of log entries.

For static classes, use:
private static readonly ILogger Logger = Log.ForContext(typeof(ClassName));


## Log Levels

### Debug
**Purpose:** Detailed diagnostic information useful during development and troubleshooting.

**When to use:**
- Detailed protocol/communication messages (e.g., sending/receiving data packets)
- Internal state changes and method flow
- Performance measurements and timing information
- Cache operations (hits/misses)
- Resource allocation/deallocation details

**Examples:**
```csharp
Logger.Debug("Display device {DeviceId}: Sent update packet", deviceId);
Logger.Debug("Disposing resources for {Component}", GetType().Name);
Logger.Debug("Font cache miss for {FontFamily}", fontFamily);
Logger.Debug("Render completed in {ElapsedMs}ms for profile {ProfileId}", elapsed, profileId);
```

### Information
**Purpose:** General application flow and significant business events.

**When to use:**
- Application startup and shutdown
- Successful device or add-on initialization
- Major configuration changes
- Successful completion of significant operations
- Connection established or closed events

**Examples:**
```csharp
Logger.Information("SynQPanel starting");
Logger.Information("Display device {DeviceId} initialized", deviceId);
Logger.Information("{Component} stopped", GetType().Name);
Logger.Information("Add-on {AddonName} loaded successfully", addonName);
Logger.Information("Service started on port {Port}", port);
```

### Warning
**Purpose:** Abnormal or unexpected events that don't prevent the application from functioning.

**When to use:**
- Missing optional resources or devices
- Fallback behavior activated
- Performance degradation detected
- Non-critical configuration issues
- Temporary failures that will be retried

**Examples:**
```csharp
Logger.Warning("Display device {DeviceId} not detected", deviceId);
Logger.Warning("Fallback font applied for {RequestedFont}", fontFamily);
Logger.Warning("Telemetry source unavailable, retrying in {Seconds}s", retryDelay);
Logger.Warning("Configuration value missing for {Key}", configKey);
```

### Error
**Purpose:** Error events that are handled but indicate a failure of an operation.

**When to use:**
- Handled exceptions
- Failed operations that won't be retried
- Data validation failures
- Resource access failures
- Add-on or plugin execution failures

**Examples:**
```csharp
Logger.Error(ex, "Display device {DeviceId} communication error", deviceId);
Logger.Error(ex, "Failed to load add-on {AddonName}", addonName);
Logger.Error("Invalid configuration value: {Value}", value);
Logger.Error(ex, "Operation failed during {Operation}", operationName);
```

### Fatal
**Purpose:** Critical errors that will cause the application to terminate.

**When to use:**
- Unhandled exceptions
- Critical resource exhaustion
- Unrecoverable application state

**Examples:**
```csharp
Logger.Fatal(exception, "Unhandled exception. Application terminating.");
```

## Best Practices

### 1. Use Structured Logging
Always use message templates with parameters instead of string interpolation:

**Good:**
```csharp
Logger.Information("Device {DeviceId} connected at {Timestamp}", deviceId, DateTime.Now);
```

**Bad:**
```csharp
Logger.Information($"Device {deviceId} connected at {DateTime.Now}");
```

### 2. Include Relevant Context
Always include relevant identifiers and context in log messages:
- Device IDs
- Add-on or plugin names
- Task names
- Port numbers
- File paths
- Operation names

### 3. Log Exceptions Properly
Always pass the exception as the first parameter:
```csharp
try
{
    // operation
}
catch (Exception ex)
{
    Logger.Error(ex, "Failed to perform operation for device {DeviceId}", deviceId);
}
```

### 4. Avoid Logging Sensitive Information
Never log:
- Passwords or API keys
- Personal user information
- Private file system paths containing usernames
- Sensitive configuration secrets
- Sensitive business data

### 5. Performance Considerations
- Debug logs are intended for development and troubleshooting
- Avoid excessive logging in tight loops
- Be mindful of large object serialization in logs

Note: Debug-level logging is disabled by default in Release builds unless explicitly enabled.

### 6. Consistency
- Use consistent message formats for similar operations
- Reuse parameter names across the application
- Follow the same patterns for similar scenarios

## Common Patterns

### Task Lifecycle
```csharp
Logger.Debug("{Component} initializing", componentName);
Logger.Information("{Component} started", componentName);
Logger.Information("{Component} stopping", componentName);
Logger.Information("{Component} stopped", componentName);
```

### Device Communication
```csharp
Logger.Debug("Sending {MessageType} to device {DeviceId}", messageType, deviceId);
Logger.Debug("Received {ResponseType} from device {DeviceId}", responseType, deviceId);
Logger.Information("Device {DeviceId} connected successfully", deviceId);
Logger.Warning("Device {DeviceId} not responding, attempt {Attempt}/{MaxAttempts}", deviceId, attempt, maxAttempts);
Logger.Error(ex, "Device {DeviceId} communication failed", deviceId);
```

### Add-on / Plugin Operations
```csharp
Logger.Debug("Loading add-on from {Path}", addonPath);
Logger.Information("Add-on {AddonName} version {Version} loaded", name, version);
Logger.Warning("Add-on {AddonName} is using deprecated API", name);
Logger.Error(ex, "Add-on {AddonName} failed during {Operation}", name, operation);
```

### Performance Logging
```csharp
Logger.Debug("Operation {OperationName} completed in {ElapsedMs}ms", operationName, elapsed);
Logger.Warning(
    "Operation {OperationName} exceeded expected time: {ElapsedMs}ms (expected < {ExpectedMs}ms)",
    operationName,
    elapsed,
    expected
);
```