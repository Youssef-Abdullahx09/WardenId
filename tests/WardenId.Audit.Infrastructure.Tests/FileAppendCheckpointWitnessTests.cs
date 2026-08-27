using Microsoft.Extensions.Options;
using WardenId.Audit.Infrastructure.Witnessing;

namespace WardenId.Audit.Infrastructure.Tests;

public class FileAppendCheckpointWitnessTests
{
    [Fact]
    public async Task WitnessAsync_AppendsToConfiguredFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"warden-audit-witness-{Guid.NewGuid()}.txt");
        try
        {
            var sut = new FileAppendCheckpointWitness(Options.Create(new AuditWitnessOptions { AppendOnlyFilePath = path }));

            await sut.WitnessAsync("note-one\n");
            await sut.WitnessAsync("note-two\n");

            var contents = await File.ReadAllTextAsync(path);
            Assert.Equal("note-one\nnote-two\n", contents);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task WitnessAsync_NoPathConfigured_Throws()
    {
        var sut = new FileAppendCheckpointWitness(Options.Create(new AuditWitnessOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.WitnessAsync("note"));
    }
}
