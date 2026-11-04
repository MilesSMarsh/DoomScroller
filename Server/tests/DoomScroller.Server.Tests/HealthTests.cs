using System.Net.Http.Json;
using System.Text.Json;
using DoomScroller.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class HealthTests
{
    sealed class FakeProbe(bool ok) : IDatabaseProbe
    {
        public Task<bool> IsReachableAsync(CancellationToken ct) => Task.FromResult(ok);
    }

    static HttpClient Client(bool dbOk) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureServices(s => s.Replace(ServiceDescriptor.Singleton<IDatabaseProbe>(new FakeProbe(dbOk))))).CreateClient();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Health_reports_version_utc_time_and_db_state(bool dbOk)
    {
        var res = await Client(dbOk).GetAsync("/health");
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(json.GetProperty("version").GetString()));
        var t = json.GetProperty("serverTimeUtc").GetDateTime();
        Assert.True(Math.Abs((DateTime.UtcNow - t.ToUniversalTime()).TotalSeconds) < 30);
        Assert.Equal(dbOk, json.GetProperty("databaseReachable").GetBoolean());
    }
}
