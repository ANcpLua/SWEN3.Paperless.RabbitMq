# Analyzer Catalog - SWEN3.Paperless.RabbitMq

**Philosophy:** "Zero Tolerance for Real Bugs, Zero Care About Style"
**Strategy:** CA + VSTHRD + MA + RCS (bugs only) - Modern .NET 10/C# 14 analyzer stack
**Last Updated:** November 22, 2025

## Core Principle

We care about:
- Production deadlocks (VSTHRD002, MA0042)
- Crashes (VSTHRD100, CA2000)
- Security holes (CA2100, CA3001, CA5350)
- Resource leaks (CA2000, CA1816)
- Dead code (CA1852)

We don't care about:
- Style opinions (CA1711 disabled; MA0053 suggestion in main, none in tests; MA0048 warning in main, none in tests)
- Formatting (RCS0001-RCS0063 - disabled entirely)
- Naming conventions (CA1715, CA1716)
- Whether you use `var` or explicit types (IDE0008)
- Brace styles, expression bodies, XML docs everywhere

## Selected Analyzers

| Package | Version | Purpose | Justification |
|---------|---------|---------|---------------|
| **Built-in .NET Analyzers** | 10.0.x (SDK) | Performance, reliability, security (CA rules) | Maintained by Microsoft, .NET 10 GA quality |
| **Meziantou.Analyzer** | 2.0.256+ | Modern C# 14, perf, security, culture-sensitive APIs | Best-in-class for modern C#, actively maintained |
| **Roslynator.Analyzers** | 4.14.1+ | Code simplification, readability hints | Complements Meziantou (refactoring suggestions) |
| **MS.VisualStudio.Threading.Analyzers** | 17.14.15+ | Async/await correctness, ConfigureAwait, deadlock prevention | Critical for SSE + RabbitMQ async patterns |

## Disabled Style Analyzers (Not Real Bugs)

| Rule ID | Description | Reason |
|---------|-------------|--------|
| **CA1711** | Identifiers should not have incorrect suffix | Disabled - `ISseStream` is not `System.IO.Stream`, suffix is intentional |
| **MA0048** | File name must match type name | Disabled - Style opinion, not a bug |
| **MA0053** | Make class sealed | Disabled - Premature optimization, style opinion |
| **RCS0001-RCS0063** | All Roslynator formatting rules | Disabled entirely - Use built-in formatter, avoid IDE lag |
| **IDE0008** | Use explicit type vs var | Disabled - Style preference, not a bug |
| **CA1715** | Identifiers should have correct prefix | Disabled - Naming opinion, not a bug |
| **CA1716** | Identifiers should not match keywords | Disabled - Too noisy, context makes it clear |

## Excluded Analyzers

| Package | Reason for Exclusion |
|---------|----------------------|
| **ErrorProne.NET.CoreAnalyzers** | Beta quality (0.8.1-beta.1), overlaps with MA/VSTHRD on async/nullability, adds noise without value |
| **SonarLint (IDE)** | Legacy rules, conflicts with modern C# 14 syntax (records, file-scoped namespaces), rely on MSBuild analyzers instead |
| **StyleCop** | Style-focused, conflicts with modern C# idioms, not worth the noise |

---

## Overlap Resolution

### ConfigureAwait Enforcement

**Decision:** Keep **VSTHRD111 as sole owner**
**Disabled:** CA2007, MA0004, RCS1090

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **VSTHRD111** | VS.Threading | Use .ConfigureAwait(bool) | ✅ **KEEP** | Sole owner (detects Task.Delay, WhenAll, most comprehensive) |
| CA2007 | NetAnalyzers | Do not directly await a Task | ❌ DISABLE | Redundant (VSTHRD111 covers it) |
| MA0004 | Meziantou | Use ConfigureAwait(false) | ❌ DISABLE | Redundant (would double-fire with VSTHRD111) |
| RCS1090 | Roslynator | Add/remove ConfigureAwait(false) | ❌ DISABLE | Redundant (VSTHRD111 covers it) |

---

### CancellationToken Propagation

**Decision:** Keep **MA0032/MA0040/MA0079/MA0080 as sole owners**
**Disabled:** CA2016, RCS1229

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **MA0032** | Meziantou | Use an overload with a CancellationToken | ✅ **KEEP** | Detects missing CT overloads |
| **MA0040** | Meziantou | Forward the CancellationToken to methods | ✅ **KEEP** | Ensures CT propagation in call chains |
| **MA0079** | Meziantou | Forward CT using .WithCancellation() | ✅ **KEEP** | IAsyncEnumerable-specific |
| **MA0080** | Meziantou | Use CT using .WithCancellation() | ✅ **KEEP** | IAsyncEnumerable-specific |
| CA2016 | NetAnalyzers | Forward the CancellationToken | ❌ DISABLE | Redundant (MA rules more comprehensive) |
| RCS1229 | Roslynator | Use async/await when necessary | ❌ DISABLE | Redundant (MA rules cover CT usage) |

---

### Async Void / Naming

**Decision:** Keep **VSTHRD100/VSTHRD101/VSTHRD200**
**Disabled:** RCS1046/RCS1047 (Roslynator async naming)

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **VSTHRD100** | VS.Threading | Avoid async void methods | ✅ **KEEP** | Catches async void outside event handlers |
| **VSTHRD101** | VS.Threading | Avoid unsupported async delegates | ✅ **KEEP** | Prevents async void in LINQ/Task.Run |
| **VSTHRD200** | VS.Threading | Use Async naming convention | ✅ **KEEP** | Enforces XxxAsync suffix |
| RCS1046 | Roslynator | Method name should end with 'Async' | ❌ DISABLE | Redundant (VSTHRD200 covers it) |
| RCS1047 | Roslynator | Non-async method should not end with 'Async' | ❌ DISABLE | Redundant (VSTHRD200 covers it) |

---

### Await Before Dispose

**Decision:** Keep **MA0100/MA0129**
**Disabled:** VSTHRD114

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **MA0100** | Meziantou | Await task before disposing of resources | ✅ **KEEP** | Catches `using (var x = Task.Run(...))` |
| **MA0129** | Meziantou | Await task in using statement | ✅ **KEEP** | Detects incomplete awaits in using |
| VSTHRD114 | VS.Threading | Avoid returning null from Task method | ⚠️ KEEP | Different scenario (null Task returns) |
| EPC31 | ErrorProne | Do not return null for Task-like types | ❌ N/A | Not installed (ErrorProne excluded) |

---

### String Comparison

**Decision:** Keep **MA0001 as sole owner**
**Disabled:** CA1307, CA1309, CA1310, RCS1155

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **MA0001** | Meziantou | StringComparison is missing | ✅ **KEEP** | Sole owner (most comprehensive, detects more patterns) |
| CA1307 | NetAnalyzers | Specify StringComparison for clarity | ❌ DISABLE | Redundant (MA0001 covers it) |
| CA1309 | NetAnalyzers | Use ordinal StringComparison | ❌ DISABLE | Redundant (MA0001 covers it) |
| CA1310 | NetAnalyzers | Specify StringComparison for correctness | ❌ DISABLE | Redundant (MA0001 covers it) |
| RCS1155 | Roslynator | Use StringComparison when comparing strings | ❌ DISABLE | Redundant (MA0001 covers it) |

---

### LINQ Optimization

**Decision:** Keep **MA0020/MA0031 as sole owners**
**Disabled:** CA1826, CA1827, RCS1077, RCS1080

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **MA0020** | Meziantou | Use direct methods instead of LINQ | ✅ **KEEP** | Sole owner (List<T>.Find vs FirstOrDefault, etc.) |
| **MA0031** | Meziantou | Optimize Enumerable.Count() usage | ✅ **KEEP** | Sole owner (.Count property vs .Count() method) |
| CA1826 | NetAnalyzers | Do not use Enumerable on indexable collections | ❌ DISABLE | Redundant (MA0020 covers it) |
| CA1827 | NetAnalyzers | Do not use Count() when Any() can be used | ❌ DISABLE | Redundant (MA0031 covers it) |
| RCS1077 | Roslynator | Optimize LINQ method call | ❌ DISABLE | Redundant (MA0020/MA0031 cover it) |
| RCS1080 | Roslynator | Use Count/Length instead of Any | ❌ DISABLE | Redundant (MA0031 covers it) |

---

### StringBuilder Optimization

**Decision:** Keep **MA0028 as sole owner**
**Disabled:** CA1830, CA1834, RCS1197

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **MA0028** | Meziantou | Optimize StringBuilder usage | ✅ **KEEP** | Sole owner (Append(char) vs Append(string), more patterns) |
| CA1830 | NetAnalyzers | Prefer StringBuilder.Append(char) | ❌ DISABLE | Redundant (MA0028 covers it) |
| CA1834 | NetAnalyzers | Use StringBuilder.Append(char) | ❌ DISABLE | Redundant (MA0028 covers it) |
| RCS1197 | Roslynator | Optimize StringBuilder.Append/AppendLine | ❌ DISABLE | Redundant (MA0028 covers it) |

---

### Array Allocations

**Decision:** Keep **MA0005**
**Disabled:** CA1825

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **MA0005** | Meziantou | Use Array.Empty<T>() | ✅ **KEEP** | Modern C# 12+ collection expressions support |
| CA1825 | NetAnalyzers | Avoid zero-length array allocations | ❌ DISABLE | Redundant (MA0005 covers it) |

---

### Async Call in Sync Context

**Decision:** Keep **MA0042**
**Disabled:** CA1849

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **MA0042** | Meziantou | Do not use blocking calls in async method | ✅ **KEEP** | Fewer false positives, supports CreateAsyncScope |
| CA1849 | NetAnalyzers | Call async methods when in async method | ❌ DISABLE | Redundant (MA0042 covers it) |

---

### Observe Async Results

**Decision:** Keep **VSTHRD110** + **MA0134**
**Disabled:** None

| Rule ID | Library | Description | Status | Reason |
|---------|---------|-------------|--------|--------|
| **VSTHRD110** | VS.Threading | Observe result of async calls | ✅ **KEEP** | Catches fire-and-forget Task |
| **MA0134** | Meziantou | Observe result of async calls | ✅ **KEEP** | Complements VSTHRD110 (different patterns) |
| EPC13 | ErrorProne | Suspiciously unobserved result | ❌ N/A | Not installed (ErrorProne excluded) |

---

## Roslynator Formatting Rules (All Disabled)

**Decision:** Disable **ALL** Roslynator formatting rules (RCS0001-RCS0063)
**Reason:** Use built-in .NET formatter only (avoid IDE lag, conflicts with .editorconfig)

| Rule ID Pattern | Count | Status | Reason |
|----------------|-------|--------|--------|
| RCS0001-RCS0063 | ~60 rules | ❌ DISABLED | Use .editorconfig + built-in formatter instead |

Examples:
- RCS0001: Add blank line after embedded statement → **DISABLED**
- RCS0027: Place new line after/before binary operator → **DISABLED**
- RCS0046: Use spaces instead of tab → **DISABLED**

---

## Critical Rules to Keep (High Signal)

### Performance (CA18xx)

| Rule ID | Description | Severity |
|---------|-------------|----------|
| CA1848 | Use LoggerMessage delegates | ⚠️ Warning |
| CA1851 | Possible multiple enumerations | ⚠️ Warning |
| CA1852 | Seal internal types | ⚠️ Warning |
| CA1853 | Unnecessary Dictionary.ContainsKey | ⚠️ Warning |
| CA1854 | Prefer TryGetValue | ⚠️ Warning |
| CA1860 | Avoid Enumerable.Any() | ⚠️ Warning |
| CA1861 | Avoid constant arrays as arguments | ⚠️ Warning |
| CA1869 | Cache JsonSerializerOptions | ⚠️ Warning |
| CA1870 | Use cached SearchValues instance | ⚠️ Warning |
| CA1871 | Do not pass nullable struct to ArgumentNullException.ThrowIfNull | ⚠️ Warning |
| CA1872 | Prefer Convert.ToHexString alternatives | ⚠️ Warning |
| CA1873 | Avoid potentially expensive logging | ⚠️ Warning |
| CA1874 | Use Regex.IsMatch | ⚠️ Warning |
| CA1875 | Use Regex.Count | ⚠️ Warning |
| CA1877 | Use Path.Combine or Path.Join overloads | ⚠️ Warning |

### Reliability (CA20xx)

| Rule ID | Description | Severity |
|---------|-------------|----------|
| CA2000 | Dispose objects before losing scope | ⚠️ Warning |
| CA2008 | Do not create tasks without TaskScheduler | ⚠️ Warning |
| CA2012 | Use ValueTasks correctly | ⚠️ Warning |
| CA2013 | Do not use ReferenceEquals with value types | ⚠️ Warning |
| CA2014 | Do not use stackalloc in loops | ⚠️ Warning |

### Security (CA3xxx)

| Rule ID | Description | Severity |
|---------|-------------|----------|
| CA3001 | SQL injection vulnerabilities | ⚠️ Warning |
| CA3002 | XSS vulnerabilities | ⚠️ Warning |
| CA3006 | Process command injection | ⚠️ Warning |
| CA3012 | Regex injection vulnerabilities | ⚠️ Warning |

---

## Test-Specific Overrides

**File Pattern:** `SWEN3.Paperless.RabbitMq.Tests/**/*.cs` (no leading `**/` in .editorconfig)

| Rule ID | Reason for Disable |
|---------|-------------------|
| VSTHRD111 | No SynchronizationContext in xUnit/NUnit |
| VSTHRD002 | ERROR in prod (sync-over-async = deadlock); disabled in tests |
| VSTHRD003 | ERROR in prod (awaiting foreign tasks); disabled in tests |
| MA0004 | ConfigureAwait unnecessary in tests |
| MA0032 | CT forwarding not needed in tests |
| MA0040 | CT propagation not critical in tests |
| MA0134 | Allow fire-and-forget in test setup |
| MA0002 | String comparers not needed in tests |
| MA0074 | Avoid `this.` prefix unnecessary in tests |
| MA0076 | Enum.GetValues<T>() unnecessary in tests |
| MA0006 | Use String.Equals unnecessary in tests |
| MA0079 | IAsyncEnumerable CT not needed in tests |
| IDE0058 | Allow expression-only statements |
| MA0048 | Allow flexible test file naming |
| CA1852 | Tests don't need sealing |
| CA1816 | Tests don't need GC.SuppressFinalize |
| MA0053 | Tests don't need sealing |
| CA1707 | Allow underscores in test names |

---

## Migration Notes

### From No Analyzers → Clean Trinity

1. ✅ **Added:** Directory.Build.props with 3 analyzer packages
2. ✅ **Added:** .editorconfig with overlap suppressions
3. ✅ **Disabled:** SonarLint "Sonar way" profile in Rider
4. ✅ **Excluded:** ErrorProne.NET (beta, overlaps, noise)

### Why No ErrorProne.NET?

| Overlap | ErrorProne Rule | Replaced By |
|---------|----------------|-------------|
| ConfigureAwait | EPC14, EPC15 | VSTHRD111 + MA0004 |
| Async void | EPC27 | VSTHRD100 |
| Null Task returns | EPC31 | VSTHRD114 |
| Observe results | EPC13 | VSTHRD110 + MA0134 |
| Quality | Beta (0.8.1-beta.1) | GA-quality alternatives |

---

## Maintenance

### Checking for Updates

```bash
# Check analyzer versions (uses .slnx by default)
dotnet list package --outdated

# Update to latest
dotnet add package Meziantou.Analyzer
dotnet add package Roslynator.Analyzers
dotnet add package Microsoft.VisualStudio.Threading.Analyzers
```

**Note:** Project uses `.slnx` (modern XML solution format) as primary, with legacy `.sln` maintained for compatibility.

**IDE Compatibility (.slnx support):**
- ✅ **Visual Studio 2022** v17.13+ (or v17.10+ with preview feature enabled)
- ✅ **Rider** 2024.2.6+ (October 2024)
- ✅ **VS Code** with C# Dev Kit
- ✅ **.NET CLI** (.NET SDK 9.0.200+)
- ❌ **Visual Studio 2019** and earlier (requires `.sln`)
- ❌ **Rider** < 2024.2.6 (requires `.sln`)
- ❌ **Many CI/CD tools** and third-party plugins (requires `.sln`)

### Performance Profiling

```bash
# Enable analyzer performance reporting
dotnet build /bl /p:ReportAnalyzer=true
```

### Adding New Rules

1. Search for the rule ID in this catalog
2. If duplicate exists, disable the weaker one
3. Update .editorconfig with severity
4. Document the decision in this file

---

## FAQ

**Q: Why disable MA0004 but keep VSTHRD111?**
A: VSTHRD111 is the sole owner for ConfigureAwait enforcement. Keeping both would cause double-firing. VSTHRD111 is more comprehensive (catches Task.Delay, WhenAll, etc.).

**Q: Why keep both VSTHRD110 and MA0134?**
A: They catch different patterns. VSTHRD110 focuses on JoinableTaskFactory, MA0134 catches general fire-and-forget.

**Q: Why disable all Roslynator formatting rules?**
A: Prevents IDE lag and conflicts. Use .editorconfig + built-in .NET formatter instead.

**Q: When should I add ErrorProne.NET?**
A: Only if you need stricter nullability/concurrency checks and can tolerate beta-quality diagnostics. Not recommended for this project.

---

## References

- [Meziantou.Analyzer Rules](https://github.com/meziantou/Meziantou.Analyzer/tree/main/docs)
- [Roslynator Analyzers](https://josefpihrt.github.io/docs/roslynator/analyzers/)
- [VS.Threading Rules](https://microsoft.github.io/vs-threading/analyzers/) (Primary) / [GitHub Docs](https://github.com/microsoft/vs-threading/tree/main/doc/analyzers) (Secondary)
- [.NET Code Analysis (CA rules)](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)
