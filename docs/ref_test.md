# План рефакторинга тестовой инфраструктуры gRPC

## 1. Очистка базовых классов

Удалить дублирование в IntegrationTestBase:

```csharp
- Удалить _channels (уже есть в BaseGrpcService)
- Удалить _channelLock (уже есть в BaseGrpcService)
- Удалить Propagator (уже есть в BaseGrpcService)
- Перенести специфичные настройки канала в конфигурацию
```

## 2. Создание тестовой конфигурации

```csharp
// Создать TestGrpcConfiguration.cs:
- Настройки каналов
- Таймауты
- Порты
- Retry политики
```

## 3. Реорганизация тестовых сервисов

### a. Создать интерфейсы для тестовых сервисов:

```csharp
public interface ITestHealthService
{
    Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default);
}

public interface ITestMemoryService
{
    Task<byte[]> ReadMemoryAsync(string sessionId, ulong address, int size);
    Task WriteMemoryAsync(string sessionId, ulong address, byte[] data);
}

// и т.д. для других сервисов
```

### b. Реализовать тестовые сервисы:

```csharp
- TestProcessService
- TestMemoryService
- TestScannerService
- TestStateService
- TestFreezeService
```

## 4. Создание фабрики тестовых сервисов

```csharp
public class TestServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPythonProcessManager _processManager;
    private readonly TestGrpcConfiguration _configuration;

    public TestServiceFactory(TestGrpcConfiguration configuration)
    {
        _configuration = configuration;
        _loggerFactory = CreateLoggerFactory();
        _processManager = CreateProcessManager();
    }

    public ITestHealthService CreateHealthService() => new TestHealthService(_loggerFactory.CreateLogger<TestHealthService>(), _processManager);
    public ITestMemoryService CreateMemoryService() => new TestMemoryService(_loggerFactory.CreateLogger<TestMemoryService>(), _processManager);
    // и т.д.
}
```

## 5. Обновление существующих тестов

### a. Создать базовый класс для тестов с сервисами:

```csharp
public abstract class ServiceTestBase : IntegrationTestBase
{
    protected readonly TestServiceFactory ServiceFactory;
    protected readonly ITestHealthService HealthService;
    protected readonly ITestMemoryService MemoryService;
    // и т.д.

    protected ServiceTestBase()
    {
        ServiceFactory = new TestServiceFactory(new TestGrpcConfiguration());
        HealthService = ServiceFactory.CreateHealthService();
        MemoryService = ServiceFactory.CreateMemoryService();
        // и т.д.
    }
}
```

### b. Обновить существующие тесты для использования новой структуры

## 6. Создание моков для тестирования

```csharp
public class MockTestServiceFactory : TestServiceFactory
{
    public MockTestServiceFactory(TestGrpcConfiguration configuration) : base(configuration)
    {
    }

    public override ITestHealthService CreateHealthService() => new MockHealthService();
    // и т.д.
}
```

## 7. Добавление тестовых утилит

### a. Создать TestContext для управления состоянием тестов:

```csharp
public class TestContext : IAsyncDisposable
{
    public string SessionId { get; }
    public TestServiceFactory ServiceFactory { get; }
    public ITestHealthService HealthService { get; }
    // и т.д.
}
```

### b. Создать TestHelper для часто используемых операций:

```csharp
public static class TestHelper
{
    public static async Task<TestContext> CreateTestContextAsync()
    {
        // Инициализация контекста теста
    }

    public static async Task WithTestContextAsync(Func<TestContext, Task> testAction)
    {
        // Выполнение теста с контекстом
    }
}
```

## 8. Обновление конфигурации тестов

### a. Создать appsettings.Test.json:

```json
{
  "GrpcTest": {
    "Server": {
      "Port": 50051,
      "Host": "127.0.0.1"
    },
    "Client": {
      "MaxRetries": 3,
      "TimeoutSeconds": 5,
      "MaxMessageSize": 52428800
    }
  }
}
```

## 9. Добавление интеграции с OpenTelemetry

```csharp
public static class TestTelemetryExtensions
{
    public static IServiceCollection AddTestTelemetry(this IServiceCollection services)
    {
        // Настройка телеметрии для тестов
    }
}
```

## 10. Создание тестовых фикстур

```csharp
public class GrpcTestFixture : IAsyncLifetime
{
    public TestServiceFactory ServiceFactory { get; }
    public TestContext TestContext { get; private set; }

    public async Task InitializeAsync()
    {
        // Инициализация фикстуры
    }

    public async Task DisposeAsync()
    {
        // Очистка ресурсов
    }
}
```

## 11. Порядок выполнения рефакторинга:

1. Создать новые файлы конфигурации
2. Реализовать базовые интерфейсы и классы
3. Создать фабрику сервисов
4. Обновить существующие тесты по одному
5. Добавить новые тестовые утилиты
6. Интегрировать телеметрию
7. Обновить CI/CD пайплайны

## 12. Проверки после рефакторинга:

- Все тесты проходят
- Логирование работает корректно
- Телеметрия собирается
- Ресурсы освобождаются правильно
- Конфигурация загружается корректно
- Моки работают как ожидается

## Результаты рефакторинга:

- Единообразие кода между тестами и основным приложением
- Лучшая поддержка и расширяемость
- Более надежные тесты
- Лучшая изоляция тестов
- Более простое добавление новых тестов
- Лучший контроль над ресурсами
