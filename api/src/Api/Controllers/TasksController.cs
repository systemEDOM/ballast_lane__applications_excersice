using BallastLane.Application.Dtos;
using BallastLane.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BallastLane.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TasksController : ApiControllerBase
{
    private readonly TaskService _taskService;

    public TasksController(TaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return UnauthorizedProblem();
        }

        var result = await _taskService.ListAsync(userId, status, cancellationToken);

        return FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return UnauthorizedProblem();
        }

        var result = await _taskService.GetAsync(userId, id, cancellationToken);

        return FromResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(
        [FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return UnauthorizedProblem();
        }

        var result = await _taskService.CreateAsync(userId, request, cancellationToken);

        if (result.IsFailure)
        {
            return FromResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskDto>> Update(
        Guid id,
        [FromBody] UpdateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return UnauthorizedProblem();
        }

        var result = await _taskService.UpdateAsync(userId, id, request, cancellationToken);

        return FromResult(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<TaskDto>> ChangeStatus(
        Guid id,
        [FromBody] ChangeTaskStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return UnauthorizedProblem();
        }

        var result = await _taskService.ChangeStatusAsync(userId, id, request, cancellationToken);

        return FromResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return UnauthorizedProblem();
        }

        var result = await _taskService.DeleteAsync(userId, id, cancellationToken);

        return FromResult(result);
    }
}
