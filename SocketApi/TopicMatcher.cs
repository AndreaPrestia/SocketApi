namespace SocketApi;

public static class TopicMatcher
{
    public static bool Matches(string pattern, string topic)
    {
        if (pattern == "#") return true;
        if (pattern == topic) return true;

        var patternParts = pattern.Split('/');
        var topicParts = topic.Split('/');

        return MatchParts(patternParts, 0, topicParts, 0);
    }

    private static bool MatchParts(string[] pattern, int pi, string[] topic, int ti)
    {
        while (pi < pattern.Length && ti < topic.Length)
        {
            if (pattern[pi] == "#")
                return true; // # matches everything remaining

            if (pattern[pi] == "*" || pattern[pi] == topic[ti])
            {
                pi++;
                ti++;
                continue;
            }

            return false;
        }

        // Both exhausted = match. Pattern exhausted but topic remains = no match.
        // Topic exhausted but pattern has trailing "#" = match.
        if (pi < pattern.Length && pattern[pi] == "#")
            return true;

        return pi == pattern.Length && ti == topic.Length;
    }
}
