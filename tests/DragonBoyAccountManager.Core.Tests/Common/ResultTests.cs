using DragonBoyAccountManager.Core.Common.Results;

namespace DragonBoyAccountManager.Core.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success("Thành công");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("Thành công", result.Message);
    }

    [Fact]
    public void Failure_CreatesFailureResult()
    {
        var result = Result.Failure("Thất bại");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal("Thất bại", result.Message);
    }

    [Fact]
    public void GenericResult_Success_HoldsValue()
    {
        var result = Result.Success(42, "OK");

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal("OK", result.Message);
    }

    [Fact]
    public void GenericResult_Failure_ThrowsOnValueAccess()
    {
        var result = Result.Failure<string>("Error");

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }
}
