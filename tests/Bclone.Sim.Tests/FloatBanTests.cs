using System.Reflection;
using Bclone.Sim.Core;
using Xunit;
using Xunit.Abstractions;

namespace Bclone.Sim.Tests;

/// <summary>
/// D2, enforced instead of merely written down: no floating point on the sim's public API.
/// </summary>
/// <remarks>
/// <para>
/// <b>⭐⭐ WHY THIS EXISTS NOW, AND NOT EARLIER OR LATER.</b> The determinism contract is
/// <em>same seed + same inputs ⇒ byte-identical state</em>, and integer-only sim state (D2) is how
/// it is currently guaranteed. `gridless` slice 1 introduces <c>Fixed</c> — a fractional
/// number type — and <b>the obvious way to build one is to give it a `FromDouble`/`ToDouble` pair
/// for convenience.</b> That single method would put float rounding back into sim state, and
/// nothing in the build would have objected.
/// </para>
/// <para>
/// <b>⭐ And it is what makes the pinned replay hash in <c>FixedTests</c> mean anything.</b>
/// Nothing runnable in-process can prove a hash is stable on somebody else's machine. What makes
/// it stable is that every operation is integer and there is no <c>double</c> anywhere near the
/// type. *That is an argument about the shape of the API, which is exactly what this asserts.*
/// </para>
/// <para>
/// <b>⛔⛔ THE ANALYZER CANNOT DO THIS JOB, AND THAT WAS MEASURED RATHER THAN ASSUMED (D317).</b>
/// <c>BannedSymbols.txt</c> said floats were "NOT enforceable here". Adding
/// <c>T:System.Double</c> to it *does* compile and *does* fire — but only on <b>static member
/// access</b>: against the live codebase it flagged seven sites, every one a
/// <c>double.IsNaN</c>/<c>double.IsInfinity</c> validation call. A probe measured what it misses —
/// a <c>double</c> field, a <c>double</c> parameter, a <c>double</c> return and <c>x * 1.5</c>
/// arithmetic all passed straight through. **It would have banned the seven places that defend
/// against doubles and none of the places that could introduce one.** The full measurement is in
/// <c>BannedSymbols.txt</c>.
/// </para>
/// <para>
/// <b>⚠️ SO STATE THIS GUARD'S BLIND SPOT RATHER THAN OVERSELLING IT.</b> It sees <em>public
/// signatures</em>. A <c>private double _accumulator</c> on a system, or a <c>(int)(cost * 1.5)</c>
/// inside a method body, is invisible to it — and sim arithmetic lives in method bodies.
/// <see cref="TheBlindSpotIsPublicSignaturesOnly"/> is the honest half: it does not claim the
/// guard is complete, it <em>measures where it stops</em>. *A guard whose limits are written down
/// is worth more than one that is assumed total.*
/// </para>
/// </remarks>
public sealed class FloatBanTests
{
    private readonly ITestOutputHelper _output;

    public FloatBanTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The two places a <c>double</c> is legitimate, named individually.
    /// </summary>
    /// <remarks>
    /// <b>⭐ Named rather than pattern-matched, and both are the SAME exemption.</b> Playback speed
    /// is wall-clock pacing: it decides how many ticks to run, never what happens inside one.
    /// <see cref="FixedTimestepDriver"/> is the boundary itself, so the whole type is exempt;
    /// <c>SimConfig.TargetTicksPerSecond</c> is the one config key that feeds it.
    /// </remarks>
    private static readonly string[] Allowed =
    {
        "Bclone.Sim.Core.FixedTimestepDriver",
        "Bclone.Sim.Config.SimConfig.TargetTicksPerSecond",
    };

    [Fact]
    public void NoPublicSimApiTakesOrReturnsFloatingPoint()
    {
        var offenders = new List<string>();
        var exemptionsUsed = new HashSet<string>(StringComparer.Ordinal);
        int typesWalked = 0;
        int membersWalked = 0;

        foreach (Type type in typeof(SimWorld).Assembly.GetExportedTypes())
        {
            typesWalked++;

            foreach (MemberInfo member in Surface(type))
            {
                membersWalked++;

                if (!TouchesFloatingPoint(member))
                {
                    continue;
                }

                string full = $"{type.FullName}.{member.Name}";
                string? exemption = ExemptionFor(type, full);

                if (exemption is not null)
                {
                    exemptionsUsed.Add(exemption);
                    continue;
                }

                offenders.Add($"{type.Name}.{member.Name}");
            }
        }

        _output.WriteLine(
            $"{typesWalked} exported types, {membersWalked} public members walked; "
            + $"{offenders.Count} offenders, {exemptionsUsed.Count} of {Allowed.Length} "
            + "exemptions used.");

        // ⭐ ANTI-VACUITY, AND IT IS THE HALF THAT ROTS FIRST (D7). A walk that found nothing
        // because it walked nothing passes silently, and an exemption left behind by a rename
        // reads as protection for a site that no longer exists. Both are checked before the
        // claim itself, because both would make the claim meaningless.
        Assert.True(typesWalked > 50, $"Only {typesWalked} exported types — the walk found nothing to check.");

        foreach (string exemption in Allowed)
        {
            Assert.True(
                exemptionsUsed.Contains(exemption),
                $"The exemption for '{exemption}' matched nothing. Either it was renamed and this "
                + "list is stale, or its doubles are gone and the exemption should be deleted — "
                + "a dead exemption reads as protection for a site that is not there.");
        }

        Assert.True(
            offenders.Count == 0,
            "Floating point reached the sim's public API, which breaks D2's integer-only state and "
            + "with it 'same seed => byte-identical state'. Use int/long, or Fixed (Q32.32) for "
            + "fractional math. Offenders: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// ⚠️ What this guard CANNOT see — asserted, so the gap is a measured fact and not a hope.
    /// </summary>
    /// <remarks>
    /// <b>⭐⭐ This test passes by finding the guard blind, and that is the point.</b> A private
    /// field is where a float would actually be introduced, and reflection over the public surface
    /// cannot reach it. Writing that down as an assertion means the next person reads a stated
    /// limit instead of trusting a guard that looks total. *If a future change ever makes the
    /// public walk catch private state, this test fails and the remarks above get rewritten —
    /// which is the correct outcome, not a regression.*
    /// </remarks>
    [Fact]
    public void TheBlindSpotIsPublicSignaturesOnly()
    {
        Type driver = typeof(FixedTimestepDriver);

        FieldInfo[] privateDoubles = Array.FindAll(
            driver.GetFields(BindingFlags.NonPublic | BindingFlags.Instance),
            field => field.FieldType == typeof(double));

        _output.WriteLine(
            $"{driver.Name} holds {privateDoubles.Length} private double field(s) that the public "
            + "walk above cannot see: "
            + string.Join(", ", Array.ConvertAll(privateDoubles, f => f.Name)));

        Assert.True(
            privateDoubles.Length > 0,
            "The type chosen to demonstrate the blind spot no longer has a private double, so this "
            + "test is not demonstrating anything. Pick another, or delete it and say why.");
    }

    /// <summary>Every public member whose signature a caller can see.</summary>
    /// <remarks>
    /// ⚠️ Property accessors are skipped — <c>get_Foo</c>/<c>set_Foo</c> would make one property
    /// need three exemptions. <b>Operators are deliberately kept:</b> <c>op_Addition</c> is exactly
    /// the shape a fractional type would smuggle a <c>double</c> in through.
    /// </remarks>
    private static IEnumerable<MemberInfo> Surface(Type type)
    {
        const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (MethodInfo method in type.GetMethods(Flags))
        {
            if (method.IsSpecialName
                && (method.Name.StartsWith("get_", StringComparison.Ordinal)
                    || method.Name.StartsWith("set_", StringComparison.Ordinal)))
            {
                continue;
            }

            yield return method;
        }

        foreach (PropertyInfo property in type.GetProperties(Flags))
        {
            yield return property;
        }

        foreach (FieldInfo field in type.GetFields(Flags))
        {
            yield return field;
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(Flags))
        {
            yield return constructor;
        }
    }

    private static bool TouchesFloatingPoint(MemberInfo member)
    {
        switch (member)
        {
            case MethodInfo method:
                if (IsFloaty(method.ReturnType))
                {
                    return true;
                }

                return AnyParameter(method.GetParameters());

            case ConstructorInfo constructor:
                return AnyParameter(constructor.GetParameters());

            case PropertyInfo property:
                return IsFloaty(property.PropertyType);

            case FieldInfo field:
                return IsFloaty(field.FieldType);

            default:
                return false;
        }

        static bool AnyParameter(ParameterInfo[] parameters)
        {
            foreach (ParameterInfo parameter in parameters)
            {
                if (IsFloaty(parameter.ParameterType))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Is a <c>double</c> or <c>float</c> anywhere inside this type?
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Recursive on purpose.</b> A bare <c>double</c> is the easy case and nobody would write
    /// it by accident. <c>double[]</c>, <c>double?</c>, <c>IReadOnlyList&lt;double&gt;</c> and
    /// <c>(int, double)</c> are the ones that would slip through a plain equality check — and a
    /// fractional type's API is exactly where a collection of them would appear.
    /// </remarks>
    private static bool IsFloaty(Type type, int depth = 0)
    {
        // Generics can nest arbitrarily and a malformed type could in principle cycle; the sim's
        // real signatures are one or two deep, so this bound is slack and still terminates.
        if (depth > 6)
        {
            return false;
        }

        if (type == typeof(double) || type == typeof(float))
        {
            return true;
        }

        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            Type? element = type.GetElementType();
            return element is not null && IsFloaty(element, depth + 1);
        }

        Type? underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return IsFloaty(underlying, depth + 1);
        }

        if (type.IsGenericType)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                if (IsFloaty(argument, depth + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string? ExemptionFor(Type type, string fullMemberName)
    {
        foreach (string allowed in Allowed)
        {
            if (type.FullName == allowed || fullMemberName == allowed)
            {
                return allowed;
            }
        }

        return null;
    }
}
