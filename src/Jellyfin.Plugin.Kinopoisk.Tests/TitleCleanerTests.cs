using Xunit;

namespace Jellyfin.Plugin.Kinopoisk.Tests
{
    public class TitleCleanerTests
    {
        [Theory]
        [InlineData("Deti.peremen.S01.2024.WEB-DL.1080p.ExKinoRay", "Deti peremen", 2024)]
        [InlineData("13.klinicheskaya.S01.2022.WEB-DL.1080p", "13 klinicheskaya", 2022)]
        [InlineData("Arcane.S01.1080p.NF.WEB-DL.DDP5.1.H.264-EniaHD", "Arcane", null)]
        [InlineData("Babylon Berlin.S01.BDRip(HEVC)1080p.xz", "Babylon Berlin", null)]
        [InlineData("Brassic S02 WEB-DL 1080p (2020)", "Brassic", 2020)]
        [InlineData("Carnival Row_ [tahiy]", "Carnival Row", null)]
        [InlineData("Disenchantment (WEB-DL 1080p)", "Disenchantment", null)]
        [InlineData("Gravity Falls (S01-02) WEB-DL HEVC 1080p", "Gravity Falls", null)]
        [InlineData("Holostyak.S08.WEB-DL.1080.25Kuzmich", "Holostyak", null)]
        [InlineData("Loki.2021.S01.HEVC.1080p.WEB-DLRip", "Loki", 2021)]
        [InlineData("Militsioner.s.rublevki.S01.XviD.SATRip.25Kuzmich", "Militsioner s rublevki", null)]
        [InlineData("Pacanki.s04e01. 2019 . WEB-DLRip", "Pacanki", 2019)]
        [InlineData("Pisheblok.2021.WEB-DL.(1080p).ExKinoRay", "Pisheblok", 2021)]
        // A range: the last year, as Kodi
        [InlineData("Rick and Morty se01-se03 2013-2017 hevc BDRip 1080p [Сыендук]", "Rick and Morty", 2017)]
        [InlineData("Scavengers Reign. Season 01. Full HD. By Wild_Cat", "Scavengers Reign", null)]
        [InlineData("Shogun_Сёгун.S01.1080p.WEBRip.H265.HEVC.RUS.ENG", "Shogun Сёгун", null)]
        [InlineData("The.Handmaid's.Tale.S03.WEB-DL.1080p.Rus.Eng.LostFilm", "The Handmaid's Tale", null)]
        [InlineData("Blade.Runner.2049.2017.BDRip.mkv", "Blade Runner 2049", 2017)]
        [InlineData("Charlotte's Web (2006)", "Charlotte's Web", 2006)]
        [InlineData("1917", "1917", null)]
        [InlineData("Окно в Париж", "Окно в Париж", null)]
        [InlineData("Mama.v.16", "Mama v 16", null)]
        public void ShouldClean(string name, string title, int? year)
        {
            var res = TitleCleaner.Clean(name);
            Assert.Equal(title, res.Title);
            Assert.Equal(year, res.Year);
        }
    }
}
