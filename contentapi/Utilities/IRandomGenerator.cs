namespace contentapi.Utilities;

public interface IRandomGenerator
{
    string GetAlphaSequence(int charCount);
    string GetRandomPassword();
}