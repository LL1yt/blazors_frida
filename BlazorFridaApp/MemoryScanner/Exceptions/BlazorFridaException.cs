using System;

namespace BlazorFridaApp.MemoryScanner.Exceptions;

/// <summary>
/// Base exception class for all BlazorFrida application exceptions
/// </summary>
public class BlazorFridaException : Exception
{
    public string ErrorCode { get; }

    public BlazorFridaException(string message) : base(message)
    {
        ErrorCode = "BF001";
    }

    public BlazorFridaException(string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = "BF001";
    }

    public BlazorFridaException(string message, string errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public BlazorFridaException(string message, string errorCode, Exception innerException) : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
