// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;

namespace QuickJS;

/// <summary>
/// Represents a single frame in a JavaScript stack trace.
/// </summary>
public sealed class JSStackFrame
{
    /// <summary>
    /// Gets the function name at this stack frame, or null if anonymous.
    /// </summary>
    public string? FunctionName { get; }

    /// <summary>
    /// Gets the source location of this stack frame.
    /// </summary>
    public SourceLocation Location { get; }

    /// <summary>
    /// Gets a value indicating whether this frame is an eval context.
    /// </summary>
    public bool IsEval { get; }

    /// <summary>
    /// Gets a value indicating whether this frame is a native function call.
    /// </summary>
    public bool IsNative { get; }

    /// <summary>
    /// Creates a new stack frame.
    /// </summary>
    /// <param name="functionName">The function name, or null for anonymous functions.</param>
    /// <param name="location">The source location.</param>
    /// <param name="isEval">Whether this is an eval context.</param>
    /// <param name="isNative">Whether this is a native function.</param>
    public JSStackFrame(string? functionName, SourceLocation location, bool isEval = false, bool isNative = false)
    {
        FunctionName = functionName;
        Location = location;
        IsEval = isEval;
        IsNative = isNative;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("    at ");

        if (!string.IsNullOrEmpty(FunctionName))
        {
            sb.Append(FunctionName);
        }
        else
        {
            sb.Append("<anonymous>");
        }

        if (IsNative)
        {
            sb.Append(" [native code]");
        }
        else if (!Location.IsEmpty)
        {
            sb.Append(" (");
            sb.Append(Location);
            sb.Append(')');
        }

        return sb.ToString();
    }
}

/// <summary>
/// Represents a JavaScript stack trace, consisting of multiple stack frames.
/// </summary>
public sealed class JSStackTrace
{
    private readonly List<JSStackFrame> _frames;

    /// <summary>
    /// Creates a new empty stack trace.
    /// </summary>
    public JSStackTrace()
    {
        _frames = new List<JSStackFrame>();
    }

    /// <summary>
    /// Creates a new stack trace with the specified frames.
    /// </summary>
    /// <param name="frames">The stack frames.</param>
    public JSStackTrace(IEnumerable<JSStackFrame> frames)
    {
        _frames = new List<JSStackFrame>(frames);
    }

    /// <summary>
    /// Gets the stack frames in this trace.
    /// </summary>
    public IReadOnlyList<JSStackFrame> Frames => _frames;

    /// <summary>
    /// Gets the number of frames in this trace.
    /// </summary>
    public int Count => _frames.Count;

    /// <summary>
    /// Gets the frame at the specified index.
    /// </summary>
    public JSStackFrame this[int index] => _frames[index];

    /// <summary>
    /// Adds a frame to the stack trace.
    /// </summary>
    /// <param name="frame">The frame to add.</param>
    internal void AddFrame(JSStackFrame frame) => _frames.Add(frame);

    /// <summary>
    /// Gets an empty stack trace.
    /// </summary>
    public static JSStackTrace Empty { get; } = new JSStackTrace();

    /// <inheritdoc />
    public override string ToString()
    {
        if (_frames.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var frame in _frames)
        {
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(frame);
        }
        return sb.ToString();
    }
}
