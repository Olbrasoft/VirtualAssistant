using VirtualAssistant.Data.Dtos;

namespace VirtualAssistant.Service.Tests;

public class CreateAndDispatchTests
{
    [Fact]
    public void CreateAndDispatchRequest_DefaultValues_AreNull()
    {
        // Arrange & Act
        var request = new CreateAndDispatchRequest();

        // Assert
        Assert.Null(request.GithubIssueNumber);
        Assert.Null(request.Summary);
        Assert.Null(request.TargetAgent);
    }

    [Fact]
    public void CreateAndDispatchRequest_CanSetValues()
    {
        // Arrange
        var request = new CreateAndDispatchRequest
        {
            GithubIssueNumber = 225,
            Summary = "Fix button mapping",
            TargetAgent = "claude"
        };

        // Assert
        Assert.Equal(225, request.GithubIssueNumber);
        Assert.Equal("Fix button mapping", request.Summary);
        Assert.Equal("claude", request.TargetAgent);
    }

    [Fact]
    public void CreateAndDispatchResponse_DefaultValues()
    {
        // Arrange & Act
        var response = new CreateAndDispatchResponse();

        // Assert
        Assert.False(response.Success);
        Assert.Null(response.Action);
        Assert.Null(response.TaskId);
        Assert.Null(response.GithubIssueNumber);
        Assert.Null(response.DispatchStatus);
        Assert.Null(response.PreviousStatus);
        Assert.Null(response.Reason);
        Assert.Equal(string.Empty, response.Message);
        Assert.Null(response.Error);
    }

    [Fact]
    public void CreateAndDispatchResponse_SuccessCreated()
    {
        // Arrange & Act
        var response = new CreateAndDispatchResponse
        {
            Success = true,
            Action = "created",
            TaskId = 42,
            GithubIssueNumber = 225,
            DispatchStatus = "sent_to_claude",
            Message = "Task created and dispatched to Claude"
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("created", response.Action);
        Assert.Equal(42, response.TaskId);
        Assert.Equal(225, response.GithubIssueNumber);
        Assert.Equal("sent_to_claude", response.DispatchStatus);
        Assert.Null(response.PreviousStatus);
    }

    [Fact]
    public void CreateAndDispatchResponse_SuccessReopened()
    {
        // Arrange & Act
        var response = new CreateAndDispatchResponse
        {
            Success = true,
            Action = "reopened",
            TaskId = 42,
            GithubIssueNumber = 225,
            DispatchStatus = "sent_to_claude",
            PreviousStatus = "completed",
            Message = "Task reopened and dispatched to Claude"
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("reopened", response.Action);
        Assert.Equal("completed", response.PreviousStatus);
    }

    [Fact]
    public void CreateAndDispatchResponse_SuccessQueued()
    {
        // Arrange & Act
        var response = new CreateAndDispatchResponse
        {
            Success = true,
            Action = "created",
            TaskId = 42,
            GithubIssueNumber = 225,
            DispatchStatus = "queued",
            Reason = "agent_busy",
            Message = "Task created, Claude is busy - queued for later"
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("queued", response.DispatchStatus);
        Assert.Equal("agent_busy", response.Reason);
    }

    [Fact]
    public void CreateAndDispatchResponse_ErrorMissingField()
    {
        // Arrange & Act
        var response = new CreateAndDispatchResponse
        {
            Success = false,
            Error = "github_issue_number is required"
        };

        // Assert
        Assert.False(response.Success);
        Assert.Equal("github_issue_number is required", response.Error);
    }
}
