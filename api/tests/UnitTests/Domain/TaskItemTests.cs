using BallastLane.Domain.Entities;
using BallastLane.Domain.Enums;
using BallastLane.Domain.Exceptions;
using BallastLane.UnitTests.Support;
using Shouldly;
using Xunit;

namespace BallastLane.UnitTests.Domain;

public sealed class TaskItemTests
{
    [Fact]
    public void Create_WithValidDetails_StartsInTodoAndTrimsFields()
    {
        var dueDate = TestData.Today.AddDays(4);

        var task = TaskItem.Create(
            TestData.UserId,
            "  Draft the proposal  ",
            "  First draft for the client  ",
            dueDate,
            TestData.Today,
            TestData.Now);

        task.UserId.ShouldBe(TestData.UserId);
        task.Title.ShouldBe("Draft the proposal");
        task.Description.ShouldBe("First draft for the client");
        task.Status.ShouldBe(TaskItemStatus.Todo);
        task.DueDate.ShouldBe(dueDate);
        task.CreatedAt.ShouldBe(TestData.Now);
        task.UpdatedAt.ShouldBe(TestData.Now);
        task.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("  a  ")]
    public void Create_WithTitleShorterThanThreeCharacters_ThrowsDomainException(string title)
    {
        Should.Throw<DomainException>(() =>
        {
            TaskItem.Create(TestData.UserId, title, null, null, TestData.Today, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithTitleLongerThanOneHundredTwentyCharacters_ThrowsDomainException()
    {
        var title = new string('t', 121);

        Should.Throw<DomainException>(() =>
        {
            TaskItem.Create(TestData.UserId, title, null, null, TestData.Today, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithBlankDescription_StoresNull()
    {
        var task = TaskItem.Create(TestData.UserId, "Draft the proposal", "   ", null, TestData.Today, TestData.Now);

        task.Description.ShouldBeNull();
    }

    [Fact]
    public void Create_WithDescriptionLongerThanOneThousandCharacters_ThrowsDomainException()
    {
        var description = new string('d', 1001);

        Should.Throw<DomainException>(() =>
        {
            TaskItem.Create(TestData.UserId, "Draft the proposal", description, null, TestData.Today, TestData.Now);
        });
    }

    [Fact]
    public void Create_WithPastDueDate_ThrowsDomainException()
    {
        Should.Throw<DomainException>(() =>
        {
            TaskItem.Create(
                TestData.UserId,
                "Draft the proposal",
                null,
                TestData.Today.AddDays(-1),
                TestData.Today,
                TestData.Now);
        });
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Todo, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Done)]
    public void ChangeStatus_WithAllowedTransition_UpdatesStatusAndTimestamp(TaskItemStatus from, TaskItemStatus to)
    {
        var task = TestData.NewTask(status: from);
        var changedAt = TestData.Now.AddMinutes(5);

        task.ChangeStatus(to, changedAt);

        task.Status.ShouldBe(to);
        task.UpdatedAt.ShouldBe(changedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo, TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.InProgress)]
    public void ChangeStatus_WithRejectedTransition_ThrowsDomainException(TaskItemStatus from, TaskItemStatus to)
    {
        var task = TestData.NewTask(status: from);

        Should.Throw<DomainException>(() =>
        {
            task.ChangeStatus(to, TestData.Now.AddMinutes(5));
        });

        task.Status.ShouldBe(from);
    }

    [Fact]
    public void UpdateDetails_WithNewValues_KeepsStatusAndBumpsUpdatedAt()
    {
        var task = TestData.NewTask(status: TaskItemStatus.InProgress, title: "Old title");
        var dueDate = TestData.Today.AddDays(3);
        var updatedAt = TestData.Now.AddHours(1);

        task.UpdateDetails("  New title  ", "  Notes  ", dueDate, TestData.Today, updatedAt);

        task.Title.ShouldBe("New title");
        task.Description.ShouldBe("Notes");
        task.DueDate.ShouldBe(dueDate);
        task.Status.ShouldBe(TaskItemStatus.InProgress);
        task.CreatedAt.ShouldBe(TestData.Now);
        task.UpdatedAt.ShouldBe(updatedAt);
    }

    [Fact]
    public void UpdateDetails_KeepingExistingPastDueDate_IsAllowed()
    {
        var pastDueDate = TestData.Today.AddDays(-5);
        var task = TestData.NewTask(dueDate: pastDueDate);

        task.UpdateDetails("Draft the proposal", null, pastDueDate, TestData.Today, TestData.Now.AddHours(1));

        task.DueDate.ShouldBe(pastDueDate);
        task.UpdatedAt.ShouldBe(TestData.Now.AddHours(1));
    }

    [Fact]
    public void UpdateDetails_WithNewPastDueDate_ThrowsDomainException()
    {
        var task = TestData.NewTask(dueDate: TestData.Today.AddDays(-5));

        Should.Throw<DomainException>(() =>
        {
            task.UpdateDetails(
                "Draft the proposal",
                null,
                TestData.Today.AddDays(-1),
                TestData.Today,
                TestData.Now.AddHours(1));
        });
    }

    [Fact]
    public void IsOwnedBy_WithOwnerAndOtherUser_ReflectsOwnership()
    {
        var task = TestData.NewTask();

        task.IsOwnedBy(TestData.UserId).ShouldBeTrue();
        task.IsOwnedBy(Guid.NewGuid()).ShouldBeFalse();
    }
}
