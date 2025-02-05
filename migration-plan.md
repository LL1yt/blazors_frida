# Python.NET to Standalone Python Process Migration Plan

0.1 mark what has been done with details and improve the plan if necessary.
0.2 Prioritize atomic transitions of individual features while maintaining full system observability through each migration phase.
0.3 implementing functionality step by step

## 1. Architecture Overview

### Current Architecture

```
[Blazor App] -> [Python.NET] -> [Python Frida Modules]
```

### Target Architecture

```
[Blazor App] <-> [gRPC] <-> [Python Service] -> [Python Frida Modules]
```

## 2. Migration Components

The main thing is that the new functionality should work. old implementations can be simply deleted if they interfere with the launch of the new implementation

### 2.1 Feature Flag System

- Implement feature flags for each memory operation type
- Configuration in appsettings.json for granular control
- Runtime toggle capability via admin interface

### 2.2 Python Microservice

- Standalone Python process exposing gRPC endpoints
- Protocol Buffers schema for all operations
- Health check endpoints for monitoring
- Automatic process recovery
- Structured logging with correlation IDs

### 2.3 Pattern Scanner Implementation

- Goal: Enable finding dynamic values (e.g., "health") based on signatures
- Details:
  - Python-based pattern generation (from known signatures or binary sections)
  - Block-based game memory scanning logic: reading memory blocks, signature matching
  - Store potential addresses in intermediate data structure
- Reasoning: Signature scanning is a classic technique used in tools like Cheat Engine. Python integration enables rapid pattern search code development.

### 2.4 Value Freeze and Modification System

- Goal: Implement "Freeze/Unfreeze" functionality similar to Cheat Engine
- Details:
  - After finding value addresses (e.g., player "health"), users can lock them
  - Implement FreezeValue(address, newValue), UnfreezeValue(address) methods
  - Background thread/task for periodic memory value rewriting when frozen
  - Support one-time modification (WriteOnce) without locking
- Reasoning: Freeze functionality is essential for maintaining constant memory values, often needed for "immortality" and other game tricks

### 2.5 Scan Results Caching

- Goal: Avoid repeated full scans on each launch using "offset caching"
- Details:
  - Save offsets relative to module base in SQLite when addresses are found
  - Load saved offsets on next launch and verify validity (via signature or Frida hooks)
  - Trigger re-scan if signature mismatch
- Reasoning: Offset caching significantly speeds up subsequent launches by avoiding repeated memory section searches

### 2.6 IPC Layer

```protobuf
service MemoryScanner {
    rpc AttachToProcess (AttachRequest) returns (AttachResponse);
    rpc ReadMemory (ReadRequest) returns (ReadResponse);
    rpc WriteMemory (WriteRequest) returns (WriteResponse);
    rpc Detach (DetachRequest) returns (DetachResponse);
}

message AttachRequest {
    string process_name = 1;
    string correlation_id = 2;
}
// ... other message definitions
```

### 2.7 State Management

#### 2.7.1 SQLite Database Structure

- Goal: Store scan configurations, locked addresses, and application settings
- Details:
  - Tables for scan profiles, frozen addresses, and application settings
  - Version control for database schema
  - State persistence between application sessions
- Reasoning: SQLite provides lightweight embedded storage perfect for local configuration persistence

#### 2.7.2 Core State Management

- Versioned state payloads
- Checkpointing system for process state
- State synchronization protocol
- Rollback capability

#### 2.7.3 Profile System

- Goal: Enable users to save and switch between different configurations
- Details:
  - Each profile (e.g., per game) can have its own address sets, patterns, frozen values
  - Load profile settings on startup (offsets, update frequency, selected process, etc.)
- Reasoning: Profiles help organize settings when working with multiple games or hack types

### 2.8 Modern UI Components

#### 2.8.1 Component Library Integration

- Goal: Create modern, responsive interface
- Details:
  - Integrate Radzen/Blazorise via NuGet
  - Select required components (tables, dialogs, panels)
  - Create base page /Pages/Index.razor or /Pages/Dashboard.razor with navigation
- Reasoning: UI libraries provide ready themes, form validation, and responsive layouts

#### 2.8.2 Address Management Interface

- Goal: Provide intuitive address viewing and control
- Details:
  - Implement Addresses.razor with DataGrid for address list, descriptions, values
  - Add "Attach to Process" button with process selector
  - Create real-time value visualization (e.g., health progress bar)
- Reasoning: Visual real-time value monitoring helps track memory changes

#### 2.8.3 Hex Navigation

- Goal: Enhanced memory content viewing
- Details:
  - Create HexViewer.razor for hex memory display
  - Enable offset clicking for detailed value inspection
- Reasoning: Hex viewer aids debugging and understanding memory content

### 2.9 IL2CPP Integration

#### 2.9.1 Core IL2CPP Support

- Goal: Enable working with IL2CPP-based Unity games
- Details:
  - Integrate with frida-il2cpp-bridge
  - Implement method hooking via Frida
  - Support Il2CppDumper for game analysis
  - Enable class/method discovery
- Reasoning: IL2CPP support is crucial for modern Unity game analysis

#### 2.9.2 Unity Game Modding

- Goal: Facilitate Unity game modification
- Details:
  - Support BepInEx for IL2CPP
  - Enable mod creation and management
- Reasoning: Modding support extends functionality beyond memory manipulation

## 3. Implementation Phases

### Phase 1: Infrastructure Setup (Week 1) [COMPLETED]

1. ✅ Set up feature flag system

   - Implemented feature flags in appsettings.json for granular control of migration
   - Created FeatureFlagService for managing flags
   - Added flags for each operation type with traffic percentages
   - Configured runtime toggle capability

2. ✅ Create Python service scaffold

   - Utilizing existing Python gRPC server
   - Enhanced with proper error handling and logging
   - Added correlation IDs for request tracking
   - Implemented OpenTelemetry integration

3. ✅ Implement gRPC contracts

   - Defined memory scanner service contracts in memory_scanner.proto
   - Added health service contracts in health.proto
   - Implemented versioned state management
   - Added proper error responses and status codes

4. ✅ Add health monitoring
   - Created GrpcHealthCheck service in C#
   - Implemented HealthServicer in Python
   - Added health check endpoint at /health
   - Configured health status management
   - Added grpcio-health-checking package

### Phase 2: Core Service Implementation (Week 2) [IN PROGRESS]

1. ✅ Port Frida functionality to standalone service

   - Implemented reader.py for memory reading operations
   - Created writer.py for memory writing operations
   - Added scanner.py for memory scanning functionality
   - Developed process_list.py for process management
   - Integrated Frida core functionality in frida_module.py

2. ✅ Implement state management [100% Complete]

   - ✅ Created state_manager.py for state persistence
   - ✅ Added versioned state payloads
   - ✅ Implemented checkpointing system
   - ✅ Added state cleanup and rollback mechanisms

3. ✅ Add logging and telemetry [100% Complete]

   - ✅ Implemented structured logging with correlation IDs
   - ✅ Added OpenTelemetry integration
   - ✅ Created GrpcHealthCheck service
   - ✅ Added performance metrics
     - Session count tracking via active_sessions_counter
     - Operation latencies with operation_duration histogram
     - Operation counts with operation_counter
     - Error tracking with error_counter
     - Implemented metrics collection in memory operations (read/write/scan)
     - Added proper error attribution and status codes
     - Integrated with OpenTelemetry console exporter

4. 🔄 Create C# gRPC client [90% Complete]

   - ✅ Implemented service adapters
   - ✅ Added error handling
   - ✅ Created retry policies
   - 🔄 Finalizing connection management

5. ⏳ Implement Pattern Scanner [0% Complete]

   - Create signature generation module in Python
   - Implement block-based memory scanning
   - Add pattern matching algorithms
   - Integrate with existing scanner.py

6. ⏳ Add Value Freeze System [0% Complete]

   - Implement FreezeValue and UnfreezeValue methods
   - Create background value update thread
   - Add one-time modification support
   - Integrate with writer.py

7. ⏳ Implement Scan Caching [0% Complete]

   - Set up SQLite tables for offset storage
   - Add offset calculation relative to module base
   - Implement signature validation on load
   - Create cache invalidation logic

8. ⏳ Setup IL2CPP Support [0% Complete]
   - Integrate frida-il2cpp-bridge
   - Add Il2CppDumper support
   - Implement Unity game class discovery
   - Create BepInEx integration foundation

### Phase 3: Hybrid Mode (Week 3)

1. Implement feature flag routing

   - Add routing logic in MemoryScannerCoordinator
   - Implement graceful fallback mechanisms
   - Add monitoring for routing decisions
   - Configure traffic splitting rules

2. Implement Modern UI Components [0% Complete]

   - Set up Radzen/Blazorise integration
   - Create base dashboard layout
   - Implement address management grid
   - Add hex viewer component
   - Create real-time value visualization
   - Add dark/light theme support

3. Add Profile Management [0% Complete]

   - Implement profile CRUD operations
   - Create profile switching UI
   - Add profile import/export
   - Set up profile auto-save

4. Implement Python Automation [0% Complete]

   - Create signature_gen.py for pattern generation
   - Add Python script for signature analysis
   - Implement binary dump processing
   - Set up Python-C# interop for script execution

5. Enhance Frida Integration [0% Complete]

   - Set up Frida hooks for validation
   - Implement dynamic method interception
   - Add IL2CPP class discovery
   - Create BepInEx mod templates

6. The main thing is that the new functionality should work. old implementations can be simply deleted if they interfere with the launch of the new implementation

   - Identify critical paths in Python.NET implementation
   - Create backup of current stable state
   - Prepare rollback scripts
   - Test new implementation in isolation

7. Deploy monitoring
   - Set up Prometheus metrics
   - Configure OpenTelemetry collectors
   - Create monitoring dashboards
   - Set up alerting rules

### Phase 4: Gradual Migration (Weeks 4-6)

1. Migrate process attachment (20% traffic)

   - Monitor attachment success rates
   - Track performance metrics
   - Validate process state consistency
   - Gradually increase traffic percentage

2. Migrate memory reading (30% traffic)

   - Ensure read accuracy
   - Compare performance with baseline
   - Monitor error rates
   - Scale traffic based on metrics

3. Migrate memory writing (30% traffic)

   - Validate write operations
   - Track state consistency
   - Monitor system stability
   - Adjust traffic based on performance

4. Migrate process detachment (20% traffic)
   - Ensure clean detachment
   - Monitor resource cleanup
   - Track process stability
   - Complete traffic migration

### Phase 5: Cleanup (Week 7)

1. Remove Python.NET code
2. Performance optimization

## 4. Testing Strategy

### 4.1 Monitoring

- Response time metrics
- Error rates
- Memory usage
- Process health
- Feature flag status

### 4.2 Rollback Triggers

- Error rate > 1%
- Response time > 200ms
- Memory leak detection
- Process crash count > 2/hour

## 5. Debugging Infrastructure

### 5.1 Logging

- Correlation IDs across processes
- Structured logging format
- Log aggregation system
- Log level control via config

### 5.2 Telemetry

- OpenTelemetry integration
- Distributed tracing
- Metric collection
- Performance profiling

### 5.3 Debugging Tools

- Remote debugging capability
- Memory dump analysis
- State inspection endpoints
- Traffic replay system

## 6. Post-Migration Development Roadmap

### 6.1 Core Memory Scanner Enhancement

1. Advanced Memory Analysis

   - Pointer path resolution system
   - Multi-level offset chaining
   - 2D coordinate extraction subsystems (it may be necessary to clarify with the user what is meant here)
   - Pattern scanning improvements

2. Modern UI Implementation
   - Integrate Radzen/Blazorise components
   - Responsive design for all screen sizes
   - Dark/light theme support
   - Real-time memory view updates

### 6.2 Advanced Frida Integration

1. Dynamic Instrumentation

   - Function hooking and interception
   - Runtime code modification
   - Memory access validation
   - Anti-debugging detection

2. Telemetry Collection
   - Function call tracing
   - Performance profiling
   - Memory access patterns
   - System call monitoring

### 6.3 Architecture Expansion

1. Modular Subsystems

   - Pointer map generation
   - Memory region analysis
   - Process manipulation
   - State persistence improvements

2. Testing Infrastructure (first we will implement the simplest testing, and we will see what happens next)
   - Unit test framework
   - Integration test suite
   - Performance benchmarks
   - Automated UI testing

### 6.4 Feature Roadmap

1. Phase 1: Core Functionality (Month 1-2)

   - Enhanced memory scanning
   - Basic pointer chain support
   - Improved UI components
   - SQLite state persistence

2. Phase 2: Advanced Features (Month 3-4)

   - Complex pointer path resolution
   - Multi-level offset chaining
   - Advanced pattern matching
   - Extended Frida integration

3. Phase 3: Performance & Scale (Month 5-6)
   - Optimization of core operations
   - Batch processing support
   - Advanced caching mechanisms
   - Distributed scanning capability
