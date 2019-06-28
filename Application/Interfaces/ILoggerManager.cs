﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces
{
    /// <summary>
    /// Interface for logging of different type info
    /// </summary>
    public interface ILoggerManager
    {
        void Error(string message);
        void Error(string message, Exception exception);
        void Warn(string message);
        void Debug(string message);
        void Info(string message);
    }
}
