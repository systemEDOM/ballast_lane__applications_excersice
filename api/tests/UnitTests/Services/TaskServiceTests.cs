using BallastLane.Application.Abstractions;
using BallastLane.Application.Common;
using BallastLane.Application.Dtos;
using BallastLane.Application.Services;
using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.UnitTests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BallastLane.UnitTests.Services;

public sealed class TaskServiceTests
{
    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly FakeClock _clock = new(TestData.Now);
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _sut = new TaskService(_tasks, _clock);
    }

    [Fact]
    public async Task ListAsync_WithoutStatus_ReturnsAllTasksMapped()
    {
        var dueDate = TestData.Today.AddDays(4);
        var inProgress = TestData.NewTask(
            status: TaskItemStatus.InProgress,
            title: "Draft the proposal",
            description: "First draft",
            dueDate: dueDate);
        var done = TestData.NewTask(status: TaskItemStatus.Done, title: "Send the invoice");

        _tasks.ListByUserAsync(TestData.UserId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskItem>>(new[] { inProgress, done }));

        var result = await _sut.ListAsync(TestData.UserId, null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Id.ShouldBe(inProgress.Id);
        result.Value[0].Title.ShouldBe("Draft the proposal");
        result.Value[0].Description.ShouldBe("First draft");
        result.Value[0].Status.ShouldBe("InProgress");
        result.Value[0].DueDate.ShouldBe(dueDate);
        result.Value[1].Status.ShouldBe("Done");
    }

    [Fact]
    public async Task ListAsync_WithStatusName_PassesParsedStatusToRepository()
    {
        var task = TestData.NewTask(status: TaskItemStatus.InProgress);

        _tasks.ListByUserAsync(TestData.UserId, TaskItemStatus.InProgress, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskItem>>(new[] { task }));

        var result = await _sut.ListAsync(TestData.UserId, "InProgress", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Status.ShouldBe("InProgress");
        _ = _tasks.Received(1)
            .ListByUserAsync(TestData.UserId, TaskItemStatus.InProgress, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_WithStatusNameInDifferentCase_PassesParsedStatusToRepository()
    {
        _tasks.ListByUserAsync(TestData.UserId, TaskItemStatus.Done, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskItem>>(Array.Empty<TaskItem>()));

        var result = await _sut.ListAsync(TestData.UserId, "  done  ", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        _ = _tasks.Received(1)
            .ListByUserAsync(TestData.UserId, TaskItemStatus.Done, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_WithUnknownStatus_ReturnsValidationError()
    {
        var result = await _sut.ListAsync(TestData.UserId, "Blocked", CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!.ContainsKey("status").ShouldBeTrue();
        result.Error!.FieldErrors!["status"].ShouldContain("Status must be one of: Todo, InProgress, Done.");
        _ = _tasks.DidNotReceive()
            .ListByUserAsync(Arg.Any<Guid>(), Arg.Any<TaskItemStatus?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WithOwnedTask_ReturnsTask()
    {
        var task = TestData.NewTask(
            status: TaskItemStatus.InProgress,
            title: "Draft the proposal",
            description: "First draft",
            dueDate: TestData.Today.AddDays(4));

        _tasks.FindByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TaskItem?>(task));

        var result = await _sut.GetAsync(TestData.UserId, task.Id, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(task.Id);
        result.Value.Title.ShouldBe("Draft the proposal");
        result.Value.Description.ShouldBe("First draft");
        result.Value.Status.ShouldBe("InProgress");
        result.Value.DueDate.ShouldBe(TestData.Today.AddDays(4));
        result.Value.CreatedAt.ShouldBe(TestData.Now);
    }

    [Fact]
    public async Task GetAsync_WithTaskOwnedByAnotherUser_ReturnsNotFound()
    {
        var task = TestData.NewTask(userId: Guid.NewGuid());

        _tasks.FindByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TaskItem?>(task));

        var result = await _sut.GetAsync(TestData.UserId, task.Id, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error!.Message.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_ReturnsCreatedTask()
    {
        var dueDate = TestData.Today.AddDays(4);
        TaskItem? added = null;

        _tasks.AddAsync(Arg.Do<TaskItem>(task => added = task), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var result = await _sut.CreateAsync(
            TestData.UserId,
            new CreateTaskRequest("  Draft the proposal  ", "  First draft  ", dueDate),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBe("Draft the proposal");
        result.Value.Description.ShouldBe("First draft");
        result.Value.Status.ShouldBe("Todo");
        result.Value.DueDate.ShouldBe(dueDate);
        result.Value.CreatedAt.ShouldBe(TestData.Now);
        added.ShouldNotBeNull();
        added!.UserId.ShouldBe(TestData.UserId);
        _ = _tasks.Received(1).AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithTooShortTitle_ReturnsValidationError()
    {
        var result = await _sut.CreateAsync(
            TestData.UserId,
            new CreateTaskRequest("ab", null, null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!.ContainsKey("title").ShouldBeTrue();
        result.Error!.FieldErrors!["title"].ShouldContain("Title must be between 3 and 120 characters.");
        _ = _tasks.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithPastDueDate_ReturnsValidationError()
    {
        var result = await _sut.CreateAsync(
            TestData.UserId,
            new CreateTaskRequest("Draft the proposal", null, TestData.Today.AddDays(-1)),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!.ContainsKey("dueDate").ShouldBeTrue();
        result.Error!.FieldErrors!["dueDate"].ShouldContain("Due date cannot be in the past.");
        _ = _tasks.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WithValidRequest_ReturnsUpdatedTask()
    {
        var task = TestData.NewTask(status: TaskItemStatus.InProgress, title: "Old title");
        var dueDate = TestData.Today.AddDays(2);
        var updatedAt = TestData.Now.AddHours(1);

        _clock.UtcNow = updatedAt;
        _tasks.FindByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TaskItem?>(task));
        _tasks.UpdateAsync(task, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var result = await _sut.UpdateAsync(
            TestData.UserId,
            task.Id,
            new UpdateTaskRequest("  New title  ", "  Notes  ", dueDate),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Title.ShouldBe("New title");
        result.Value.Description.ShouldBe("Notes");
        result.Value.DueDate.ShouldBe(dueDate);
        result.Value.Status.ShouldBe("InProgress");
        result.Value.UpdatedAt.ShouldBe(updatedAt);
        _ = _tasks.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WhenRepositoryReportsMissingTask_ReturnsNotFound()
    {
        var task = TestData.NewTask();

        _tasks.FindByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TaskItem?>(task));
        _tasks.UpdateAsync(task, Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        var result = await _sut.UpdateAsync(
            TestData.UserId,
            task.Id,
            new UpdateTaskRequest("Draft the proposal", null, null),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error!.Message.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task ChangeStatusAsync_WithAllowedTransition_ReturnsUpdatedTask()
    {
        var task = TestData.NewTask();
        var changedAt = TestData.Now.AddMinutes(30);

        _clock.UtcNow = changedAt;
        _tasks.FindByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TaskItem?>(task));
        _tasks.UpdateAsync(task, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var result = await _sut.ChangeStatusAsync(
            TestData.UserId,
            task.Id,
            new ChangeTaskStatusRequest("inprogress"),
            CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("InProgress");
        result.Value.UpdatedAt.ShouldBe(changedAt);
        _ = _tasks.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_WithRejectedTransition_ReturnsValidationError()
    {
        var task = TestData.NewTask(status: TaskItemStatus.Done);

        _tasks.FindByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TaskItem?>(task));

        var result = await _sut.ChangeStatusAsync(
            TestData.UserId,
            task.Id,
            new ChangeTaskStatusRequest("Todo"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!.ContainsKey("status").ShouldBeTrue();
        result.Error!.FieldErrors!["status"].ShouldContain("A task cannot move from Done to Todo.");
        _ = _tasks.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeStatusAsync_WithUnknownStatus_ReturnsValidationError()
    {
        var task = TestData.NewTask();

        _tasks.FindByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TaskItem?>(task));

        var result = await _sut.ChangeStatusAsync(
            TestData.UserId,
            task.Id,
            new ChangeTaskStatusRequest("Blocked"),
            CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Validation);
        result.Error!.FieldErrors!["status"].ShouldContain("Status must be one of: Todo, InProgress, Done.");
        _ = _tasks.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WithOwnedTask_ReturnsSuccess()
    {
        var taskId = Guid.NewGuid();

        _tasks.DeleteAsync(taskId, TestData.UserId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var result = await _sut.DeleteAsync(TestData.UserId, taskId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNull();
        _ = _tasks.Received(1).DeleteAsync(taskId, TestData.UserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenRepositoryReportsMissingTask_ReturnsNotFound()
    {
        var taskId = Guid.NewGuid();

        _tasks.DeleteAsync(taskId, TestData.UserId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        var result = await _sut.DeleteAsync(TestData.UserId, taskId, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.NotFound);
        result.Error!.Message.ShouldBe("Task not found.");
    }
}
