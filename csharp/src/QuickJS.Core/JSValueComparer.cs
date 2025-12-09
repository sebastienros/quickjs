// Licensed under the MIT License.

using System.Collections.Generic;

namespace QuickJS;

/// <summary>
/// Equality comparer for JSValue based on Tag and value.
/// </summary>
internal sealed class JSValueComparer : IEqualityComparer<JSValue>
{
    public bool Equals(JSValue x, JSValue y) => x.Equals(y);
    public int GetHashCode(JSValue obj) => obj.GetHashCode();
}
