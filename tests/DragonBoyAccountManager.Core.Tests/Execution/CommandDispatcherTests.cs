using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Tests.Execution;

public class CommandDispatcherTests
{
    private sealed record TestCommand(string Text) : IGameCommand
    {
        public string Name => "TestCommand";
    }

    private sealed record UnregisteredCommand : IGameCommand
    {
        public string Name => "UnregisteredCommand";
    }

    private sealed class TestCommandHandler : IGameCommandHandler<TestCommand>
    {
        public Task<Result> HandleAsync(TestCommand command, CancellationToken ct = default)
        {
            return Task.FromResult(Result.Success($"Processed: {command.Text}"));
        }
    }

    private sealed class TestTrackingMiddleware(List<string> trace) : ICommandMiddleware
    {
        public async Task<Result> InvokeAsync(IGameCommand command, CommandExecutionDelegate next, CancellationToken ct)
        {
            trace.Add($"Pre:{command.GetType().Name}");
            var result = await next(command, ct);
            trace.Add($"Post:{command.GetType().Name}");
            return result;
        }
    }

    [Fact]
    public async Task DispatchAsync_WithRegisteredHandler_ExecutesSuccessfully()
    {
        var dispatcher = new CommandDispatcher();
        dispatcher.RegisterHandler(new TestCommandHandler());

        var result = await dispatcher.DispatchAsync(new TestCommand("DBAM"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Processed: DBAM", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_WithUnregisteredCommand_ReturnsFailure()
    {
        var dispatcher = new CommandDispatcher();

        var result = await dispatcher.DispatchAsync(new UnregisteredCommand());

        Assert.False(result.IsSuccess);
        Assert.Contains("Không tìm thấy handler", result.Message);
    }

    [Fact]
    public async Task DispatchAsync_WithMiddlewares_ExecutesInPipelineOrder()
    {
        var dispatcher = new CommandDispatcher();
        dispatcher.RegisterHandler(new TestCommandHandler());

        var trace = new List<string>();
        dispatcher.AddMiddleware(new TestTrackingMiddleware(trace));

        var result = await dispatcher.DispatchAsync(new TestCommand("Pipeline"));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, trace.Count);
        Assert.Equal("Pre:TestCommand", trace[0]);
        Assert.Equal("Post:TestCommand", trace[1]);
    }
}
