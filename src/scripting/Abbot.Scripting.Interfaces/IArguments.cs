using System;
using System.Collections.Generic;

namespace Serious.Abbot.Scripting;

/// <summary>
/// Represents the arguments to the skill parsed into a collection of tokens.
/// </summary>
public interface IArguments : IReadOnlyList<IArgument>, IArgument
{
    /// <summary>
    /// Deconstructs the arguments into two arguments.
    /// If there are less than two arguments, this will return <see cref="IMissingArgument"/> for the missing
    /// arguments. If there are more than two arguments, the last argument will be a concatenation of the
    /// remaining arguments.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    void Deconstruct(out IArgument first, out IArgument second);

    /// <summary>
    /// Deconstructs the arguments into three arguments.
    /// If there are less than three arguments, this will return <see cref="IMissingArgument"/> for the missing
    /// arguments. If there are more than three arguments, the last argument will be a concatenation of the
    /// remaining arguments.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <param name="third">The third argument.</param>
    void Deconstruct(out IArgument first, out IArgument second, out IArgument third);

    /// <summary>
    /// Deconstructs the arguments into four arguments.
    /// If there are less than four arguments, this will return <see cref="IMissingArgument"/> for the missing
    /// arguments. If there are more than four arguments, the last argument will be a concatenation of the
    /// remaining arguments.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <param name="third">The third argument.</param>
    /// <param name="fourth">The fourth argument.</param>
    void Deconstruct(out IArgument first, out IArgument second, out IArgument third, out IArgument fourth);

    /// <summary>
    /// Deconstructs the arguments into five arguments.
    /// If there are less than five arguments, this will return <see cref="IMissingArgument"/> for the missing
    /// arguments. If there are more than five arguments, the last argument will be a concatenation of the
    /// remaining arguments.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <param name="third">The third argument.</param>
    /// <param name="fourth">The fourth argument.</param>
    /// <param name="fifth">The fifth argument.</param>
    void Deconstruct(
        out IArgument first,
        out IArgument second,
        out IArgument third,
        out IArgument fourth,
        out IArgument fifth);

    /// <summary>
    /// Deconstructs the arguments into six arguments.
    /// If there are less than six arguments, this will return <see cref="IMissingArgument"/> for the missing
    /// arguments. If there are more than five arguments, the last argument will be a concatenation of the
    /// remaining arguments.
    /// </summary>
    /// <param name="first">The first argument.</param>
    /// <param name="second">The second argument.</param>
    /// <param name="third">The third argument.</param>
    /// <param name="fourth">The fourth argument.</param>
    /// <param name="fifth">The fifth argument.</param>
    /// <param name="sixth">The sixth</param>
    void Deconstruct(
        out IArgument first,
        out IArgument second,
        out IArgument third,
        out IArgument fourth,
        out IArgument fifth,
        out IArgument sixth);

    /// <summary>
    /// Pops the first argument from the collection as the skill name, and returns the rest of the arguments as
    /// an <see cref="IArguments" /> collection.
    /// </summary>
    (string skill, IArguments) Pop();

    /// <summary>
    /// Retrieves the first argument from the collection that matches the condition, and returns that argument and
    /// the rest of the arguments as an <see cref="IArguments" /> collection. If the condition is not meant,
    /// <see cref="IMissingArgument" /> will be returned.
    /// </summary>
    (IArgument argument, IArguments) FindAndRemove(Predicate<IArgument> condition);

    /// <summary>
    /// Skips the specified number of arguments and returns the rest as an <see cref="IArguments"/> collection.
    /// </summary>
    /// <param name="count">The number of elements to skip.</param>
    IArguments Skip(int count);

    /// <summary>
    /// Indexes a range of arguments into a new <see cref="IArguments"/> collection.
    /// </summary>
    /// <param name="range">The range to grab.</param>
    /// <returns>The resulting <see cref="IArguments"/>.</returns>
    IArguments this[Range range] { get; }

    /// <summary>
    /// Slices the arguments into a new <see cref="IArguments"/> collection.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The number of elements to slice.</param>
    /// <returns>The resulting <see cref="IArguments"/>.</returns>
    IArguments Slice(int start, int length);
}
