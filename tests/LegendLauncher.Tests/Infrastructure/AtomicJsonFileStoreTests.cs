using System.Text.Json;
using System.Text.Json.Serialization;
using LegendLauncher.Infrastructure.Persistence;

namespace LegendLauncher.Tests.Infrastructure;

public sealed class AtomicJsonFileStoreTests
{
    [Fact]
    public async Task WriteAndReadAsync_RoundTripsDocumentWithoutTemporaryFiles()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.Combine("nested", "state.json");
        var store = new AtomicJsonFileStore<TestDocument>(filePath);

        await store.WriteAsync(new TestDocument("ready", 7));
        var result = await store.ReadAsync();

        Assert.Equal(new TestDocument("ready", 7), result);
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(filePath)!, "*.tmp"));
    }

    [Fact]
    public async Task UpdateAsync_CoordinatesDifferentStoreInstancesForTheSamePath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.Combine("counter.json");
        var first = new AtomicJsonFileStore<CounterDocument>(filePath);
        var second = new AtomicJsonFileStore<CounterDocument>(filePath);
        await first.WriteAsync(new CounterDocument(0));

        var updates = Enumerable.Range(0, 30)
            .Select(index => (index & 1) == 0 ? first : second)
            .Select(store => store.UpdateAsync(current => new CounterDocument(current!.Value + 1)));

        await Task.WhenAll(updates);

        Assert.Equal(30, (await first.ReadAsync())!.Value);
    }

    [Fact]
    public async Task WriteAsync_WhenSerializationFails_PreservesPreviousDocument()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = temporaryDirectory.Combine("state.json");
        var options = new JsonSerializerOptions();
        options.Converters.Add(new ConditionalDocumentConverter());
        var store = new AtomicJsonFileStore<ConditionalDocument>(filePath, options);
        await store.WriteAsync(new ConditionalDocument("valid"));

        await Assert.ThrowsAsync<JsonException>(
            () => store.WriteAsync(new ConditionalDocument("fail")));

        Assert.Equal(new ConditionalDocument("valid"), await store.ReadAsync());
        Assert.Empty(Directory.EnumerateFiles(temporaryDirectory.Path, "*.tmp"));
    }

    public sealed record TestDocument(string State, int Count);

    public sealed record CounterDocument(int Value);

    public sealed record ConditionalDocument(string Value);

    private sealed class ConditionalDocumentConverter : JsonConverter<ConditionalDocument>
    {
        public override ConditionalDocument Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
            new(reader.GetString() ?? string.Empty);

        public override void Write(
            Utf8JsonWriter writer,
            ConditionalDocument value,
            JsonSerializerOptions options)
        {
            if (value.Value == "fail")
            {
                throw new JsonException("Expected test failure.");
            }

            writer.WriteStringValue(value.Value);
        }
    }
}
