using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class TransliterationTests
    {
        [Theory]
        [InlineData("Илья Куликов", "Ilya Kulikov")]
        [InlineData("Ирина Старшенбаум", "Irina Starshenbaum")]
        public void ShouldTransliterateToLatin(string text, string expected)
        {
            Assert.Equal(expected, text.TransliterateToLatin());
        }

        [Theory]
        [InlineData("Deti peremen", "дети перемен")]
        [InlineData("Holostyak", "холостяк")]
        [InlineData("Militsioner s rublevki", "милиционер с рублевки")]
        [InlineData("13 klinicheskaya", "13 клиническая")]
        [InlineData("Pervaya raketka", "первая ракетка")]
        [InlineData("Podslushano v Rybinske", "подслушано в рыбинске")]
        [InlineData("Pacanki", "пацанки")]
        [InlineData("Mama v 16", "мама в 16")]
        [InlineData("Ukrytie", "укрытие")]
        [InlineData("Okno v Parizh", "окно в париж")]
        [InlineData("Moy laskovyy i nezhnyy zver", "мой ласковый и нежный звер")]
        [InlineData("Eto my", "это мы")]
        public void ShouldTransliterateLatinToCyrillic(string text, string expected)
        {
            Assert.Equal(expected, text.TransliterateLatinToCyrillic());
        }

        [Theory]
        [InlineData("пищеблок", "Pisheblok")]
        [InlineData("Кровь на асфальте", "Krov na asfalte")]
        [InlineData("Ёлки", "Yolki")]
        [InlineData("Ирония судьбы, или С лёгким паром!", "Ironiya sudby ili S lyogkim parom")]
        public void TitleKeysOfTransliterationShouldMatch(string russian, string latin)
        {
            Assert.Equal(russian.ToTitleKey(), latin.TransliterateLatinToCyrillic().ToTitleKey());
        }
    }
}
