using System;

namespace WileyWidget.Abstractions
{
    /// <summary>
    /// Represents the result of an operation, including success/failure status and optional error messages.
    /// Used for error handling without throwing exceptions.
    /// </summary>
    public class Result
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; protected set; }

        /// <summary>
        /// Gets the error message, if any.
        /// </summary>
        public string? ErrorMessage { get; protected set; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static Result Success()
        {
            return new Result { IsSuccess = true, ErrorMessage = null };
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result Failure(string errorMessage)
        {
            return new Result { IsSuccess = false, ErrorMessage = errorMessage };
        }
    }

    /// <summary>
    /// Represents the result of an operation with a return value, including success/failure status and optional error messages.
    /// Used for error handling without throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The type of the result data.</typeparam>
    public class Result<T> where T : class
    {
        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// Gets the result data, if successful. May be null even on success if the operation returned no data.
        /// </summary>
        public T? Data { get; private set; }

        /// <summary>
        /// Gets the error message, if any.
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Creates a successful result with data.
        /// </summary>
        public static Result<T> Success(T data)
        {
            return new Result<T> { IsSuccess = true, Data = data, ErrorMessage = null };
        }

        /// <summary>
        /// Creates a successful result with no data.
        /// </summary>
        public static Result<T> Success()
        {
            return new Result<T> { IsSuccess = true, Data = null, ErrorMessage = null };
        }

        /// <summary>
        /// Creates a failed result with an error message.
        /// </summary>
        public static Result<T> Failure(string errorMessage)
        {
            return new Result<T> { IsSuccess = false, Data = null, ErrorMessage = errorMessage };
        }

        /// <summary>
        /// Creates a failed result with an error message and exception details.
        /// </summary>
        public static Result<T> Failure(string errorMessage, Exception exception)
        {
            var fullMessage = $"{errorMessage}: {exception.Message}";
            return new Result<T> { IsSuccess = false, Data = null, ErrorMessage = fullMessage };
        }
    }
}
