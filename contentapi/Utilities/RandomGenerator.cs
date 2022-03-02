using System.Text;

namespace contentapi.Utilities;

public class RandomGenerator : IRandomGenerator
{
    protected Random random;
    protected readonly object randomLock = new object();

    /// <summary>
    /// This field is MOSTLY just for testing and debugging purposes! You generally WOULDN'T want
    /// to change this, as it limits the available letters in GetAlphaSequence.
    /// </summary>
    public int AlphaSequenceAvailableAlphabet {get;set;} = 26;

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
                sb.Append((char)('a' + (random.Next() % AlphaSequenceAvailableAlphabet)));
        }

        return sb.ToString();
    }
}