using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Xunit;

public class ClientsApiTests : IClassFixture<TestWebAppFactory>
{
    private readonly HttpClient _http;
    public ClientsApiTests(TestWebAppFactory f) => _http = f.CreateClient();

    [Fact]
    public async Task Create_Get_Update_Delete_roundtrip()
    {
        // create
        var createReq = new {
            firstName="Ana", lastName="Silva", email="ana1@example.com",
            citizenCardNumber="12345678", phone="+351911111111", birthDate="1990-01-01"
        };
        var create = await _http.PostAsJsonAsync("/api/v1/clients", createReq);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await create.Content.ReadFromJsonAsync<dynamic>();
        string id = created.id;

        // get
        var get = await _http.GetAsync($"/api/v1/clients/{id}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);

        // update
        var updateReq = new {
            firstName="Ana", lastName="Santos", email="ana1@example.com",
            citizenCardNumber="12345678", phone="+351922222222", birthDate="1990-01-01"
        };
        var put = await _http.PutAsJsonAsync($"/api/v1/clients/{id}", updateReq);
        put.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // duplicate email -> 409
        var dup = await _http.PostAsJsonAsync("/api/v1/clients", createReq);
        dup.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        // delete -> 204
        var del = await _http.DeleteAsync($"/api/v1/clients/{id}");
        del.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
