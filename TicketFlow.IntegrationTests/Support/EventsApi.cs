using System.Net.Http.Json;
using System.Text.Json;

namespace TicketFlow.IntegrationTests.Support;

// Atalhos para o fluxo de eventos, para os testes focarem no que verificam.
public static class EventsApi
{
    public static Task<HttpResponseMessage> CreateRawAsync(
        HttpClient client, string name = "Rock Festival 2027", string location = "Arena XYZ", DateTimeOffset? date = null) =>
        client.PostAsJsonAsync("/events", new
        {
            name,
            location,
            date = date ?? DateTimeOffset.UtcNow.AddDays(30)
        });

    public static async Task<Guid> CreateAsync(
        HttpClient client, string name = "Rock Festival 2027", DateTimeOffset? date = null)
    {
        var response = await CreateRawAsync(client, name, date: date);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> AddSectionRawAsync(
        HttpClient client, Guid eventId, string name = "Pista", int capacity = 10, decimal price = 150m) =>
        client.PostAsJsonAsync($"/events/{eventId}/sections", new { name, capacity, price });

    public static async Task AddSectionAsync(
        HttpClient client, Guid eventId, string name = "Pista", int capacity = 10, decimal price = 150m) =>
        (await AddSectionRawAsync(client, eventId, name, capacity, price)).EnsureSuccessStatusCode();

    public static Task<HttpResponseMessage> PostActionAsync(HttpClient client, Guid eventId, string action) =>
        client.PostAsync($"/events/{eventId}/{action}", content: null);

    // Cria um evento com um setor e o publica.
    public static async Task<Guid> CreatePublishedAsync(
        HttpClient organizerClient, string name = "Rock Festival 2027", DateTimeOffset? date = null, int capacity = 10)
    {
        var id = await CreateAsync(organizerClient, name, date);
        await AddSectionAsync(organizerClient, id, capacity: capacity);
        (await PostActionAsync(organizerClient, id, "publish")).EnsureSuccessStatusCode();
        return id;
    }

    public static async Task<string> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetProperty("code").GetString()!;
}
