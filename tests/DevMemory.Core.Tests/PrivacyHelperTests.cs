using DevMemory.Core.Utilities;

namespace DevMemory.Core.Tests;

public sealed class PrivacyHelperTests
{
    [Fact]
    public void StripPrivateTags_Replaces_Single_Block()
    {
        var input  = "Public content. <private>SECRET=abc</private> End.";
        var result = PrivacyHelper.StripPrivateTags(input);

        Assert.Equal("Public content. [REDACTED] End.", result);
    }

    [Fact]
    public void StripPrivateTags_Replaces_Multiple_Blocks()
    {
        var input  = "<private>A</private> middle <private>B</private>";
        var result = PrivacyHelper.StripPrivateTags(input);

        Assert.Equal("[REDACTED] middle [REDACTED]", result);
    }

    [Fact]
    public void StripPrivateTags_Is_Case_Insensitive()
    {
        var input  = "<PRIVATE>secret</PRIVATE>";
        var result = PrivacyHelper.StripPrivateTags(input);

        Assert.Equal("[REDACTED]", result);
    }

    [Fact]
    public void StripPrivateTags_Handles_Multiline_Content()
    {
        var input = "Before\n<private>\nline1\nline2\n</private>\nAfter";
        var result = PrivacyHelper.StripPrivateTags(input);

        Assert.Equal("Before\n[REDACTED]\nAfter", result);
    }

    [Fact]
    public void StripPrivateTags_Returns_Original_When_No_Tags()
    {
        var input  = "No private tags here.";
        var result = PrivacyHelper.StripPrivateTags(input);

        Assert.Equal(input, result);
    }

    [Fact]
    public void StripPrivateTags_Does_Not_Contain_Secret_After_Strip()
    {
        var input  = "Cfg: <private>PASSWORD=hunter2</private>.";
        var result = PrivacyHelper.StripPrivateTags(input);

        Assert.DoesNotContain("PASSWORD", result);
        Assert.DoesNotContain("hunter2", result);
    }
}
