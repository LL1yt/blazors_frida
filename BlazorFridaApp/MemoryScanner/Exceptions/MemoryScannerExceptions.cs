using System;

namespace BlazorFridaApp.MemoryScanner.Exceptions;

public class ProcessAccessException : BlazorFridaException
{
    public ProcessAccessException(string message) : base(message, "BF100")
    {
    }

    public ProcessAccessException(string message, Exception innerException) : base(message, "BF100", innerException)
    {
    }
}

public class MemoryScanException : BlazorFridaException
{
    public MemoryScanException(string message) : base(message, "BF200")
    {
    }

    public MemoryScanException(string message, Exception innerException) : base(message, "BF200", innerException)
    {
    }
}

public class InvalidScanParametersException : BlazorFridaException
{
    public InvalidScanParametersException(string message) : base(message, "BF201")
    {
    }
}

public class MemoryWriteException : BlazorFridaException
{
    public MemoryWriteException(string message) : base(message, "BF300")
    {
    }

    public MemoryWriteException(string message, Exception innerException) : base(message, "BF300", innerException)
    {
    }
}
