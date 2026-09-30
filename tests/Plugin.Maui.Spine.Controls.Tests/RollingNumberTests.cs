using System.Globalization;
using Plugin.Maui.Spine.Controls;
using SkiaSharp;
using Xunit;

namespace Plugin.Maui.Spine.Controls.Tests;

/// <summary>Which characters of an <see cref="AnimatedLabel"/> in RollingNumber mode roll, and which way.</summary>
public class RollingNumberTests
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    private static (string? From, string? To)[] Pair(string from, string to, bool fromEnd = false) =>
        [.. RollingNumber.Pair(from, to, fromEnd).Select(c => (c.From, c.To))];

    [Fact]
    public void Only_the_changed_digit_rolls()
    {
        var columns = RollingNumber.Pair("41", "42");

        Assert.Equal([("4", "4"), ("1", "2")], Pair("41", "42"));
        Assert.Equal([false, true], columns.Select(c => c.Rolls));
    }

    [Fact]
    public void A_new_digit_arrives_at_the_end()
    {
        Assert.Equal([("9", "1"), ("9", "0"), (null, "0")], Pair("99", "100"));
    }

    [Fact]
    public void A_lost_digit_leaves_at_the_end()
    {
        Assert.Equal([("1", "9"), ("0", "9"), ("0", null)], Pair("100", "99"));
        Assert.Equal([("1", "1"), ("0", null)], Pair("10", "1"));
    }

    [Fact]
    public void Text_before_the_number_stays_put()
    {
        var columns = Pair("Score 9", "Score 10");

        Assert.Equal(("S", "S"), columns[0]);
        Assert.Equal((" ", " "), columns[5]);
        Assert.Equal([("9", "1"), (null, "0")], columns[6..]);
    }

    [Fact]
    public void Text_after_the_number_does_not_roll()
    {
        Assert.Equal([("9", "1"), (null, "0"), (" ", " "), ("s", "s")], Pair("9 s", "10 s"));
    }

    [Fact]
    public void A_separator_between_digits_does_not_roll()
    {
        var columns = RollingNumber.Pair("12:59", "13:00");

        Assert.Equal([("1", "1"), ("2", "3"), (":", ":"), ("5", "0"), ("9", "0")], Pair("12:59", "13:00"));
        Assert.False(columns[2].Rolls);
    }

    [Fact]
    public void Each_group_of_digits_grows_on_its_own()
    {
        Assert.Equal([("9", "1"), (null, "0"), (":", ":"), ("5", "0"), ("9", "0")], Pair("9:59", "10:00"));
        Assert.Equal([("9", "1"), (null, "0"), (".", "."), ("7", "5"), ("5", null)], Pair("9.75", "10.5"));
    }

    [Fact]
    public void Texts_built_differently_are_paired_from_the_left()
    {
        Assert.Equal([("5", "5"), (null, " "), (null, "p"), (null, "t"), (null, "s")], Pair("5", "5 pts"));
        Assert.Equal([("-", "3"), ("5", null)], Pair("-5", "3"));
    }

    [Fact]
    public void Aligned_to_the_end_a_new_digit_arrives_at_the_front()
    {
        Assert.Equal([(null, "1"), ("9", "0"), ("9", "0")], Pair("99", "100", fromEnd: true));
        Assert.Equal([(null, "1"), ("9", "0"), (":", ":"), ("5", "0"), ("9", "0")], Pair("9:59", "10:00", fromEnd: true));
        Assert.Equal([("1", null), ("0", "1")], Pair("10", "1", fromEnd: true));
    }

    [Fact]
    public void Columns_point_back_at_their_characters()
    {
        var columns = RollingNumber.Pair("9", "10");

        Assert.Equal((0, 0), (columns[0].FromIndex, columns[0].ToIndex));
        Assert.Equal((-1, 1), (columns[1].FromIndex, columns[1].ToIndex));
    }

    [Fact]
    public void A_character_made_of_several_chars_is_one_column()
    {
        Assert.Equal([("👍🏽", "👍🏽"), ("1", "2")], Pair("👍🏽1", "👍🏽2"));
    }

    [Theory]
    [InlineData("9", "10", true)]
    [InlineData("10", "9", false)]
    [InlineData("-5", "3", true)]
    [InlineData("-5", "-7", false)]
    [InlineData("−5", "−7", false)]
    [InlineData("Score 9", "Score 10", true)]
    [InlineData("12:59", "13:00", true)]
    [InlineData("0:10", "0:09", false)]
    [InlineData("-0:10", "-0:09", true)]
    [InlineData("1 999", "2 000", true)]
    [InlineData("9.75", "10.5", true)]
    [InlineData("10.5", "9.75", false)]
    [InlineData("abc", "abd", true)]
    [InlineData("5", "5 pts", true)]
    public void The_roll_follows_the_number(string from, string to, bool increases)
    {
        Assert.Equal(increases, RollingNumber.Increases(from, to, English));
    }

    [Fact]
    public void A_decimal_comma_is_read_with_the_culture()
    {
        Assert.False(RollingNumber.Increases("1,5", "1,25", Swedish));
        Assert.True(RollingNumber.Increases("1,5", "1,75", Swedish));
    }

    [Theory]
    [InlineData("111", "888")]
    [InlineData("1:11", "0:48")]
    [InlineData("Score 17", "Score 40")]
    public void Digits_are_tabular(string narrow, string wide)
    {
        using var font = new SKFont(SKTypeface.Default, 40f);

        Assert.Equal(RollingNumber.Measure(wide, font), RollingNumber.Measure(narrow, font));
    }

    [Fact]
    public void Text_without_digits_keeps_its_own_width()
    {
        using var font = new SKFont(SKTypeface.Default, 40f);

        Assert.Equal(font.MeasureText("pts"), RollingNumber.Measure("pts", font), 3);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 30f)]
    [InlineData(1f, 60f)]
    public void The_text_is_placed_by_its_alignment(float align, float left)
    {
        Assert.Equal(left, RollingNumber.Left(100f, 40f, align));
    }

    [Fact]
    public void Text_that_is_too_wide_starts_at_the_start()
    {
        Assert.Equal(0f, RollingNumber.Left(30f, 40f, 1f));
    }

    [Fact]
    public void The_easing_starts_at_rest_and_ends_there()
    {
        Assert.Equal(0f, RollingNumber.Ease(0f));
        Assert.Equal(1f, RollingNumber.Ease(1f));
        Assert.True(RollingNumber.Ease(0.5f) > 0.5f);
    }
}
