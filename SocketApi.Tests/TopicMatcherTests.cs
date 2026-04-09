namespace SocketApi.Tests;

public class TopicMatcherTests
{
    [Theory]
    [InlineData("sensors/temperature", "sensors/temperature", true)]
    [InlineData("sensors/temperature", "sensors/humidity", false)]
    [InlineData("sensors/*", "sensors/temperature", true)]
    [InlineData("sensors/*", "sensors/humidity", true)]
    [InlineData("sensors/*", "sensors/a/b", false)]
    [InlineData("sensors/#", "sensors/temperature", true)]
    [InlineData("sensors/#", "sensors/a/b", true)]
    [InlineData("sensors/#", "sensors/a/b/c", true)]
    [InlineData("#", "anything", true)]
    [InlineData("#", "a/b/c/d", true)]
    [InlineData("a/*/c", "a/b/c", true)]
    [InlineData("a/*/c", "a/x/c", true)]
    [InlineData("a/*/c", "a/b/d", false)]
    [InlineData("a/*/c", "a/b/c/d", false)]
    [InlineData("a/#", "a", true)]
    [InlineData("a/#", "a/b", true)]
    [InlineData("a/#", "a/b/c", true)]
    [InlineData("a/#", "b", false)]
    [InlineData("exact", "exact", true)]
    [InlineData("exact", "other", false)]
    [InlineData("a/b/c", "a/b/c", true)]
    [InlineData("a/b/c", "a/b", false)]
    [InlineData("a/b", "a/b/c", false)]
    public void Matches_ReturnsExpectedResult(string pattern, string topic, bool expected)
    {
        Assert.Equal(expected, TopicMatcher.Matches(pattern, topic));
    }
}
