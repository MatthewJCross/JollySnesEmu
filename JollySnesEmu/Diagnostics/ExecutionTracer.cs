using System;
using System.IO;
using System.Text;

namespace JollySnesEmu.Diagnostics
{
    public sealed class ExecutionTracer : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new();
        private readonly int _maximumInstructions;

        private int _instructionCount;
        private bool _disposed;

        public bool Enabled { get; set; } = true;

        public int InstructionCount => _instructionCount;

        public bool LimitReached => _instructionCount >= _maximumInstructions;

        public ExecutionTracer(string fileName, int maximumInstructions = 1_000_000)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("A log filename is required.", nameof(fileName));
            }

            if (maximumInstructions < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumInstructions));
            }

            _maximumInstructions = maximumInstructions;

            string logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");

            Directory.CreateDirectory(logDirectory);

            string filePath = Path.Combine(logDirectory, fileName);

            _writer = new StreamWriter(filePath, false, Encoding.UTF8)
            {
                AutoFlush = false
            };

            _writer.WriteLine($"Execution trace started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            _writer.WriteLine($"Maximum instructions: {_maximumInstructions:N0}");
            _writer.WriteLine();
        }

        public void Write(string message)
        {
            if (!Enabled || _disposed || LimitReached)
            {
                return;
            }

            lock (_lock)
            {
                if (_disposed || LimitReached)
                {
                    return;
                }

                _writer.WriteLine(message);

                _instructionCount++;

                if (_instructionCount % 1000 == 0)
                {
                    _writer.Flush();
                }
            }
        }

        public void WriteHeader(string message)
        {
            if (_disposed)
            {
                return;
            }

            lock (_lock)
            {
                if (!_disposed)
                {
                    _writer.WriteLine();
                    _writer.WriteLine($"========== {message} ==========");
                    _writer.Flush();
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    _writer.Flush();
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _writer.WriteLine();
                _writer.WriteLine($"Trace finished: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                _writer.WriteLine($"Instructions recorded: {_instructionCount:N0}");

                if (LimitReached)
                {
                    _writer.WriteLine("Instruction limit reached.");
                }

                _writer.Flush();
                _writer.Dispose();

                _disposed = true;
            }
        }
    }
}
