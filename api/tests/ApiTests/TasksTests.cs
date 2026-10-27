using System.Net;
using System.Net.Http.Json;
using BallastLane.Application.Dtos;
using Shouldly;
using Xunit;

namespace BallastLane.ApiTests;

public sealed class TasksTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public TasksTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/tasks");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ForDemoUser_ReturnsSeededTasks()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();

        using var response = await client.GetAsync("/api/tasks");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tasks = await response.ReadAsync<List<TaskDto>>();
        tasks.ShouldNotBeNull();
        tasks!.Count.ShouldBeGreaterThanOrEqualTo(6);
        tasks.ShouldAllBe(task => task.Id != Guid.Empty);
        tasks.ShouldAllBe(task => task.Title.Length >= 3);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreatedWithLocationAndTaskRetrievable()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        var title = $"Draft the proposal {Guid.NewGuid():N}";
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));

        using var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest(title, "First pass for the client.", dueDate),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        var created = await response.ReadAsync<TaskDto>();
        created.ShouldNotBeNull();
        created!.Title.ShouldBe(title);
        created.Description.ShouldBe("First pass for the client.");
        created.Status.ShouldBe("Todo");
        created.DueDate.ShouldBe(dueDate);
        response.Headers.Location!.ToString().ShouldContain(created.Id.ToString());

        using var fetched = await client.GetAsync(response.Headers.Location);
        fetched.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reloaded = await fetched.ReadAsync<TaskDto>();
        reloaded.ShouldNotBeNull();
        reloaded!.Id.ShouldBe(created.Id);
        reloaded.Title.ShouldBe(title);
    }

    [Fact]
    public async Task Create_WithInvalidTitle_ReturnsValidationProblem()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();

        using var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest("ab", null, null),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.ReadJsonAsync();
        problem.ReadCode().ShouldBe("validation_failed");
        problem.HasErrorFor("title").ShouldBeTrue();
    }

    [Fact]
    public async Task Create_WithPastDueDate_ReturnsValidationProblem()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        using var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest($"Draft the proposal {Guid.NewGuid():N}", null, yesterday),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.ReadJsonAsync();
        problem.ReadCode().ShouldBe("validation_failed");
        problem.HasErrorFor("dueDate").ShouldBeTrue();
    }

    [Fact]
    public async Task Update_WithValidRequest_ChangesTitle()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        var task = await CreateTaskAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"/api/tasks/{task.Id}",
            new UpdateTaskRequest("Updated proposal title", "Revised scope.", null),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = await response.ReadAsync<TaskDto>();
        updated.ShouldNotBeNull();
        updated!.Id.ShouldBe(task.Id);
        updated.Title.ShouldBe("Updated proposal title");
        updated.Description.ShouldBe("Revised scope.");
        updated.UpdatedAt.ShouldBeGreaterThanOrEqualTo(task.UpdatedAt);
    }

    [Fact]
    public async Task Update_WithUnknownId_ReturnsNotFound()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();

        using var response = await client.PutAsJsonAsync(
            $"/api/tasks/{Guid.NewGuid()}",
            new UpdateTaskRequest("Updated proposal title", null, null),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.ReadJsonAsync();
        problem.ReadCode().ShouldBe("not_found");
    }

    [Fact]
    public async Task ChangeStatus_FromTodoToInProgress_ThenBackToTodoIsRejected()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        var task = await CreateTaskAsync(client);

        using var forward = await client.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new ChangeTaskStatusRequest("InProgress"),
            ApiTestHelpers.JsonOptions);

        forward.StatusCode.ShouldBe(HttpStatusCode.OK);

        var moved = await forward.ReadAsync<TaskDto>();
        moved.ShouldNotBeNull();
        moved!.Status.ShouldBe("InProgress");

        using var backward = await client.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new ChangeTaskStatusRequest("Todo"),
            ApiTestHelpers.JsonOptions);

        backward.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await backward.ReadJsonAsync();
        problem.ReadCode().ShouldBe("validation_failed");
    }

    [Fact]
    public async Task Delete_ExistingTask_ReturnsNoContentThenNotFound()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        var task = await CreateTaskAsync(client);

        using var deleted = await client.DeleteAsync($"/api/tasks/{task.Id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var fetched = await client.GetAsync($"/api/tasks/{task.Id}");
        fetched.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ForTaskOwnedByAnotherUser_ReturnsNotFound()
    {
        using var owner = _factory.CreateClient();
        var ownerEmail = ApiTestHelpers.UniqueEmail("owner");
        var register = await owner.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest(ownerEmail, "Other Owner", "Password123"),
            ApiTestHelpers.JsonOptions);

        register.StatusCode.ShouldBe(HttpStatusCode.Created);

        var ownerAuth = await register.ReadAsync<AuthResultDto>();
        ownerAuth.ShouldNotBeNull();
        owner.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ownerAuth!.AccessToken);

        var foreignTask = await CreateTaskAsync(owner);

        using var demo = await _factory.CreateAuthenticatedClientAsync();

        using var response = await demo.GetAsync($"/api/tasks/{foreignTask.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var ownView = await owner.GetAsync($"/api/tasks/{foreignTask.Id}");
        ownView.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task List_WithStatusFilter_ReturnsOnlyMatchingTasks()
    {
        using var client = await _factory.CreateAuthenticatedClientAsync();
        var task = await CreateTaskAsync(client);

        using var movedToDone = await client.PatchAsJsonAsync(
            $"/api/tasks/{task.Id}/status",
            new ChangeTaskStatusRequest("Done"),
            ApiTestHelpers.JsonOptions);
        movedToDone.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await client.GetAsync("/api/tasks?status=Done");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var tasks = await response.ReadAsync<List<TaskDto>>();
        tasks.ShouldNotBeNull();
        tasks!.ShouldNotBeEmpty();
        tasks.ShouldAllBe(item => item.Status == "Done");
        tasks.ShouldContain(item => item.Id == task.Id);
    }

    private static async Task<TaskDto> CreateTaskAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/tasks",
            new CreateTaskRequest($"Draft the proposal {Guid.NewGuid():N}", null, null),
            ApiTestHelpers.JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var task = await response.ReadAsync<TaskDto>();
        task.ShouldNotBeNull();

        return task!;
    }
}
