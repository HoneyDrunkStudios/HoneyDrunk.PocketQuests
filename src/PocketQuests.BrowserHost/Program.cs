using PocketQuests.Tests.Integration;

if (args.Length != 1)
    throw new ArgumentException("Supply the ignored browser fixture directory.");
var directory = Path.GetFullPath(args[0]);
Directory.SetCurrentDirectory(AppContext.BaseDirectory);
await SqlApiTests.RunBrowserFixture(directory);
