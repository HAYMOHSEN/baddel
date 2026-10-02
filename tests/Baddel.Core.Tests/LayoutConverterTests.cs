using Baddel.Core;
using Xunit;

namespace Baddel.Core.Tests;

public class LayoutConverterTests
{
    private static readonly LayoutConverter Converter = new(LayoutMap.Standard);
    private static readonly ConversionOptions Office = new() { FixAutoCapitalization = true };

    [Theory]
    [InlineData("hgsghl ugd;l", "السلام عليكم")]
    [InlineData("hgsbl ugd;l", "السلام عليكم")]
    [InlineData("lvpfh", "مرحبا")]
    [InlineData("Hpl]", "أحمد")]
    [InlineData("Hkh", "أنا")]
    [InlineData("Ygn", "إلى")]
    [InlineData("hgsbl ugd;l ,vplm hggi ,fv;hji", "السلام عليكم ورحمة الله وبركاته")]
    [InlineData(";dt phg;?", "كيف حالك؟")]
    [InlineData("b Huvt", "لا أعرف")]
    [InlineData("Yk ahx hggi", "إن شاء الله")]
    [InlineData("Nldk", "آمين")]
    [InlineData("hg'bf td hg[hlum", "الطلاب في الجامعة")]
    [InlineData("a;vh [.dbK Hvh; y]h>", "شكرا جزيلا، أراك غدا.")]
    [InlineData("ig dl;k; Yvshg hglgt?", "هل يمكنك إرسال الملف؟")]
    [InlineData("Gk hbljphk wuf", "لأن الامتحان صعب")]
    [InlineData("lsjatn", "مستشفى")]
    [InlineData("`ifj Ygn hgl;jfm )hgr]dlm(", "ذهبت إلى المكتبة (القديمة)")]
    public void Arabic_typed_on_an_English_layout_is_fixed(string typed, string expected)
    {
        ConversionResult result = Converter.Convert(typed);
        Assert.Equal(ConversionDirection.LatinToArabic, result.Direction);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData("HGSBL UGD;L", "السلام عليكم")]
    [InlineData("LVPFH", "مرحبا")]
    [InlineData("HGSBL UGD;L ,VPLM HGGI ,FV;HJI", "السلام عليكم ورحمة الله وبركاته")]
    public void Caps_lock_is_undone(string typed, string expected) =>
        Assert.Equal(expected, Converter.Convert(typed).Text);

    [Theory]
    [InlineData("Hgsbl ugd;l", "السلام عليكم")]
    [InlineData("Lvpfh", "مرحبا")]
    [InlineData("B Huvt", "لا أعرف")]
    [InlineData("Ig dl;k; Yvshg hglgt?", "هل يمكنك إرسال الملف؟")]
    [InlineData("A;vh [.dbK Hvh; y]h>", "شكرا جزيلا، أراك غدا.")]
    [InlineData("Hpl] ,lpl]", "أحمد ومحمد")]
    [InlineData("Lsjatn", "مستشفى")]
    public void Office_auto_capitalisation_is_undone(string typed, string expected) =>
        Assert.Equal(expected, Converter.Convert(typed, Office).Text);

    [Theory]
    [InlineData("اثممخ صخقمي", "hello world")]
    [InlineData("أثممخ ًخقمي", "Hello World")]
    [InlineData("مهلاف", "light")]
    [InlineData("اهلامهلاف", "highlight")]
    [InlineData("شلاخعف", "about")]
    [InlineData("سهلا", "sigh")]
    [InlineData("فاخعلاف", "thought")]
    [InlineData("ثىخعلا", "enough")]
    [InlineData("مشعلا", "laugh")]
    [InlineData("يشعلافثق", "daughter")]
    [InlineData("ثهلافغ", "eighty")]
    [InlineData("صثهلا", "weigh")]
    [InlineData("لاخخن", "book")]
    [InlineData("لاشلاغ", "baby")]
    [InlineData("لاقهلاف ىهلاف", "bright night")]
    [InlineData("شمفاخعلا فاث صثشفاثق هس قخعلا", "although the weather is rough")]
    [InlineData("لأخخي ةخقىهىلو ؛قخبز أشىه!", "Good morning, Prof. Hani!")]
    [InlineData(")اثممخ(", "(hello)")]
    [InlineData("[هىث", "Fine")]
    [InlineData("]ثشق ٍهق", "Dear Sir")]
    [InlineData("/خىيخى", "London")]
    [InlineData("صصصزلخخلمثزؤخةظسثشقؤا", "www.google.com/search")]
    [InlineData("فاث ىثهلالاخعق لاخعلاف ش لاهل لاخسف", "the neighbour bought a big ghost")]
    [InlineData("يخعلاف", "doubt")]
    [InlineData("يثلاف", "debt")]
    [InlineData("خلافشهى", "obtain")]
    [InlineData("حعلامهؤ", "public")]
    [InlineData("حخسسهلامث", "possible")]
    [InlineData("اهلاصشغ", "highway")]
    [InlineData("؛هففسلاعقلا", "Pittsburgh")]
    [InlineData("لأاشىش", "Ghana")]
    [InlineData("سحشلاثففه", "spaghetti")]
    [InlineData("÷ ؤشىطف لاثمهثرث هفطس 5حة", "I can't believe it's 5pm")]
    [InlineData("}شمم ةث شف 079-555-1234", "Call me at 079-555-1234")]
    [InlineData("ثةشهم: فثسف@ثءشةحمثزؤخة", "email: test@example.com")]
    public void English_typed_on_the_Arabic_layout_is_fixed(string typed, string expected)
    {
        ConversionResult result = Converter.Convert(typed);
        Assert.Equal(ConversionDirection.ArabicToLatin, result.Direction);
        Assert.Equal(expected, result.Text);
    }

    [Theory]
    [InlineData("مرحبا يا أحمد ;dt phg;", "مرحبا يا أحمد كيف حالك")]
    [InlineData("قال: ;dt phg;?", "قال: كيف حالك؟")]
    [InlineData("hgsbl ugd;l", "السلام عليكم")]
    [InlineData("I sent the file غثس", "I sent the file yes")]
    [InlineData("سلام , ًاشف هس فاهس", "سلام , What is this")]
    public void Without_a_selection_only_the_latest_wrong_words_change(string line, string expected) =>
        Assert.Equal(expected, Converter.ConvertLatestRun(line).Text);

    [Fact]
    public void Presentation_forms_and_Arabic_digits_are_understood()
    {
        Assert.Equal("b", Converter.Convert("\uFEFB").Text);
        Assert.Equal("123", Converter.Convert("١٢٣").Text);
    }

    [Theory]
    [InlineData("2024")]
    [InlineData("   ")]
    [InlineData("")]
    public void Text_without_letters_is_left_alone(string text) =>
        Assert.False(Converter.Convert(text).Changed);

    [Fact]
    public void Standard_map_covers_the_Arabic_alphabet() =>
        Assert.True(LayoutMap.Standard.ArabicLetterCount >= 30);
}

public class EnglishLexiconTests
{
    [Theory]
    [InlineData("light")]
    [InlineData("Thought")]
    [InlineData("neighbour")]
    [InlineData("highway")]
    public void Knows_common_gh_words(string word) => Assert.True(EnglishLexicon.Shared.Contains(word));

    [Theory]
    [InlineData("hugh")]
    [InlineData("ghee")]
    [InlineData("about")]
    public void Prefers_b_spellings_where_both_exist(string word) => Assert.False(EnglishLexicon.Shared.Contains(word));
}
