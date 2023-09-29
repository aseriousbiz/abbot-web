# Snapshot Testing

We use Snapshot Testing with [Verify](https://github.com/VerifyTests/Verify) to simplify some of our testing logic.

## How does it work?

Just call `Verify(...)` with some object (can be an anonymous object) after you've Arranged your test conditions and Acted.
Verify will handle the Assert phase by doing the following:

1. Generate a [JSONesque](https://github.com/VerifyTests/Verify/blob/main/docs/serializer-settings.md#not-valid-json) output serializing the provided object
2. Save that output to `[TestName].received.txt`
3. Compare that output to `[TestName].verified.txt` (which may not exist)
4. If they differ, fail the test and report the diff
5. If they are the same, pass the test.

The first time you run a new test, there will be no `.verified.txt`, so you'll get a failure.
You can review the new content to ensure it matches your expectations.
If it does, do one of the following:

1. In Rider, with the [Verify Support plugin](https://plugins.jetbrains.com/plugin/17240-verify-support), right click the failed test and select "Accept Received"
2. In Visual Studio, with Resharper and the [Verify Support plugin](https://plugins.jetbrains.com/plugin/17241-verify-support), right click the failed test and select "Accept Received"
3. At the terminal, run `dotnet verify review` or just `dotnet verify accept` (to accept without reviewing)
    - `dotnet tool restore` will make [Verify.Terminal](https://github.com/VerifyTests/Verify.Terminal) available

## What gets captured

The `Verify(...)` call captures the object you provide, **and** the following ambient state:

1. Microsoft.Extensions.Logging logs logged in a category that starts `Serious.`

You can provide any of the following objects in `Verify`:

1. The `BusTestHarness` on `TestEnvironment` (all messages published/sent/received will be added to the snapshot)
2. Any Entity Framework Model (_most_ navigation properties will be excluded, but you may need to update `VerifyConfig` if specific ones aren't being included).
3. Any DateTime value derived from a deterministic `IClock`. **Do not** snapshot timestamps derived from `DateTime.UtcNow`!
4. Simple POCO objects

You can provide an Anonymous Object to combine several objects into a single snapshot.
For example: `Verify(new { env.BusTestHarness, env.TestData.Organization })`.
Each test should only have **one** `Verify` call, unless absolutely necessary.
Do not compare objects with internal state.
Only public properties are serialized by default.

## Theories/Parameterized Tests

In order for Verify to produce snapshots for each iteration of a parameterized test, you need to tell it which parameters it should include in the file name.
You do this with the `.UseParameters` call, chained after `Verify(...)`.
For example:

```csharp
[Theory]
[InlineData("foo", 123, 456)]
[InlineData("bar", 789, 101)]
public async Task AParameterizedTest(string thingy, int x1, int x2) {
    await Verify(new object())
        .UseParameters(thingy, x1);
}
```

The above will produce a filename that includes `thingy=[thingy]` and `x1=[x1]`.
The `x2` value will not be used in the file name.
You don't need to put every parameter in the `UseParameters` call, only the ones that uniquely identify the test case.
Verify will fail if two tests end up producing the same file name.

## Pitfalls / Warnings

Avoid putting `env.BusTestHarness` in the `Verify` call if you are testing a consumer.
It's designed for testing that the correct messages were published.
If you're testing a request/response consumer, you should be getting the response _in your test_ and you can `Verify` that.
