using System.Text;

namespace contentapi.Utilities;

public class RandomGenerator : IRandomGenerator
{
    protected Random random;
    protected readonly object randomLock = new object();

    public RandomGenerator()
    {
        random = new Random();
    }

    public string GetAlphaSequence(int charCount)
    {
        var sb = new StringBuilder();

        lock(randomLock)
        {
            for(int i = 0; i < charCount; ++i)
                sb.Append((char)('a' + (random.Next() % 26)));
        }

        return sb.ToString();
    }
}