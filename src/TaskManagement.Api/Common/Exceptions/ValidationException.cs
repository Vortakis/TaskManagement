﻿namespace TaskManagement.Api.Common.Exceptions
{
    public class ValidationException : Exception
    {
        public ValidationException(string message) : base(message) { }

    }
}
