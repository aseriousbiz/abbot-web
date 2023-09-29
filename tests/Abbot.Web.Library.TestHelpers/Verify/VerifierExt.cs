using System.Runtime.CompilerServices;

// TODO: global using static?
public static class VerifierExt
{
    public static SettingsTask Verify<TResult>(
        Func<Task<TResult>> target,
        VerifySettings? settings = null,
        [CallerFilePath] string sourceFile = "") =>
        new SettingsTask(new(), async (settings) => await Verifier.Verify(await target(), settings, sourceFile));
}
