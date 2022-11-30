using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using contentapi.Utilities;
using Xunit;

namespace contentapi.test;

public class RandomGeneratorTest : UnitTestBase
{
    protected RandomGenerator generator;

    public RandomGeneratorTest()
    {
        generator = GetService<RandomGenerator>();
    }

    [Fact]
    public void GetAlphaSequence_AllLetters()
    {
        HashSet<string> letters = new HashSet<string>();

        for(int i = 0; i < 10000; ++i)
        {
            letters.Add(generator.GetAlphaSequence(1));

            if(letters.Count == 26)
            {
                if(Regex.IsMatch(string.Concat(letters), "^[a-z]+$"))
                    return;
                else
                    throw new InvalidOperationException($"AlphaSequence returned letters outside the non lowercase set!");
            }
        }

        Assert.True(false, "The alphasequence didn't contain all letters of the alphabet!");
    }

    [Fact]
    public void GetAlphaSequence_DifferentServiceRandom()
    {
        var generator2 = GetService<RandomGenerator>();

        var set1 = new List<string>();
        var set2 = new List<string>();

        //The chance of an actual collision in a set of just 20 is so ridiculously small...
        for(var i = 0; i < 20; ++i)
        {
            set1.Add(generator.GetAlphaSequence(5));
            set2.Add(generator2.GetAlphaSequence(5));
        }

        foreach(var i1 in set1)
            Assert.DoesNotContain(i1, set2);
    }

    [Fact]
    public void GetRandomPassword_CharCount()
    {
        var result = generator.GetRandomPassword();
        Assert.Equal(16, result.Length);
        Assert.DoesNotContain("=", result);
    }

    [Fact]
    public void GetRandomPassword_Different()
    {
        var result = generator.GetRandomPassword();
        var result2 = generator.GetRandomPassword();
        Assert.NotEqual(result, result2);
    }
}